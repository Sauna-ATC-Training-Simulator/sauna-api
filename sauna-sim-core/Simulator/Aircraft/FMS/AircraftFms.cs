using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AviationCalcUtilNet.Aviation;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Magnetic;
using AviationCalcUtilNet.MathTools;
using AviationCalcUtilNet.Units;
using FsdConnectorNet;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fixes;
using FsdConnectorNet.Args;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using AviationCalcUtilNet.Atmos;
using System.Numerics;
using AviationCalcUtilNet.Atmos.Grib;
using SaunaSim.Core.Simulator.Aircraft.Performance;

namespace SaunaSim.Core.Simulator.Aircraft.FMS
{
    public class AircraftFms
    {
        private SimAircraft _parentAircraft;
        // private int _cruiseAlt;
        private IRouteLeg _activeLeg;
        private List<IRouteLeg> _routeLegs;
        private readonly object _routeLegsLock;
        private bool _suspended;
        private static readonly Velocity MIN_GS_DIFF = Velocity.FromKnots(10);
        private Velocity _lastGs = (Velocity) 0;
        private bool _wpEvtTriggered = false;
        private readonly MagneticTileManager _magTileMgr;

        // Fms Values
        private Length _xTk;
        private Length _aTk;
        private Bearing _requiredTrueCourse;
        private Length _turnRadius;
        private Length _vTk;
        private Angle _requiredFpa;
        private McpSpeedUnitsType _spdUnits;
        private int _selSpd;

        public EventHandler<WaypointPassedEventArgs> WaypointPassed;

        public AircraftFms(SimAircraft parentAircraft, MagneticTileManager mgr)
        {
            _parentAircraft = parentAircraft;
            _routeLegsLock = new object();
            _xTk = Length.FromMeters(-1);
            _aTk = Length.FromMeters(-1);
            _requiredTrueCourse = null;
            _vTk = Length.FromMeters(-1);
            _requiredFpa = Angle.FromRadians(0);
            _turnRadius = Length.FromMeters(-1);
            _magTileMgr = mgr;

            lock (_routeLegsLock)
            {
                _routeLegs = new List<IRouteLeg>();
            }

            _suspended = false;

            PhaseType = FmsPhaseType.CRUISE;

            // PERF INIT
            PerfInit = new PerfInit()
            {
                ClimbKias = _parentAircraft.PerformanceData.Climb_KIAS,
                ClimbMach = (int)(_parentAircraft.PerformanceData.Climb_Mach * 100),
                CruiseKias = _parentAircraft.PerformanceData.Cruise_KIAS,
                CruiseMach = (int)(_parentAircraft.PerformanceData.Cruise_Mach * 100),
                DescentKias = _parentAircraft.PerformanceData.Descent_KIAS,
                DescentMach = (int)(_parentAircraft.PerformanceData.Descent_Mach * 100),
                CruiseAlt = (int)(_parentAircraft.FlightPlan != null ? _parentAircraft.FlightPlan.Value.cruiseLevel : 0),
                LimitAlt = 10000,
                LimitSpeed = 250,

                // TODO: Change to be based on Departure/Arrival airport
                TransitionAlt = 18000,
                TransitionLevel = 18000
            };
        }

        // PERF INIT
        public PerfInit PerfInit { get; set; }

        // DEP ARR
        public FmsDeparture Dep { get; set; }
        public FmsArrival Arr { get; set; }

        public Length AlongTrackDistance => _aTk;

        public Length CrossTrackDistance => _xTk;

        public Bearing RequiredTrueCourse => _requiredTrueCourse;

        public Length TurnRadius => _turnRadius;

        public Length VerticalTrackDistance => _vTk;

        public Angle RequiredFpa => _requiredFpa;

        public FmsPhaseType PhaseType { get; set; }
        public McpSpeedUnitsType FmsSpeedUnits => _spdUnits;
        public int FmsSpeedValue => _selSpd;

        public bool Suspended
        {
            get => _suspended;
            set => _suspended = value;
        }

        public int CruiseAltitude => PerfInit.CruiseAlt;

        private string _depIcao;
        private Airport _depArpt;
        public Airport DepartureAirport
        {
            get
            {
                string curDepIcao = _parentAircraft.FlightPlan?.origin.ToUpper();
                if (curDepIcao != _depIcao)
                {
                    _depArpt = DataHandler.GetAirportByIdentifier(curDepIcao);
                    _depIcao = curDepIcao;
                }
                return _depArpt;
            }
            set
            {
                _depArpt = value;
                _depIcao = value.Identifier.ToString().ToUpper();
                if (_parentAircraft.FlightPlan.HasValue)
                {
                    FlightPlan fp = _parentAircraft.FlightPlan.Value;
                    fp.origin = _depIcao;
                    _parentAircraft.FlightPlan = fp;
                }
            }
        }

        public Length DepartureAirportElevation => _depArpt == null ? new Length(0) : _depArpt.Elevation;

        private string _arrIcao;
        private Airport _arrArpt;
        public Airport ArrivalAirport
        {
            get
            {
                string curArrIcao = _parentAircraft.FlightPlan?.destination.ToUpper();
                if (curArrIcao != _arrIcao)
                {
                    _arrArpt = DataHandler.GetAirportByIdentifier(curArrIcao);
                    _arrIcao = curArrIcao;
                }
                return _arrArpt;
            }
            set
            {
                _arrArpt = value;
                _arrIcao = value.Identifier.ToString().ToUpper();
                if (_parentAircraft.FlightPlan.HasValue)
                {
                    FlightPlan fp = _parentAircraft.FlightPlan.Value;
                    fp.destination = _arrIcao;
                    _parentAircraft.FlightPlan = fp;
                }
            }
        }
        public Length ArrivalAirportElevation => _arrArpt == null ? new Length(0) : _arrArpt.Elevation;

        public IRouteLeg ActiveLeg
        {
            get => _activeLeg;
        }

        public List<IRouteLeg> GetRouteLegs()
        {
            lock (_routeLegsLock)
            {
                return _routeLegs.ToList();
            }
        }

        public void InsertAtIndex(IRouteLeg routeLeg, int index)
        {
            lock (_routeLegsLock)
            {
                try
                {
                    // If inserting as the first item, add discontinuity
                    _routeLegs.Insert(index, routeLeg);
                    if (index == 0)
                    {
                        _routeLegs.Insert(1, new DiscoLeg(Bearing.FromDegrees(0)));
                    }
                    RecalculatePerformance();
                }
                catch (ArgumentOutOfRangeException ex) { }
            }
        }

        public void AddRouteLeg(IRouteLeg routeLeg)
        {
            lock (_routeLegsLock)
            {
                _routeLegs.Add(routeLeg);
                RecalculatePerformance();
            }
        }

        public IRouteLeg ActivateNextLeg()
        {
            lock (_routeLegsLock)
            {
                if (_routeLegs.Count > 0)
                {
                    _activeLeg = _routeLegs[0];
                    _wpEvtTriggered = false;
                    _routeLegs.RemoveAt(0);
                    RecalculatePerformance();
                }
            }

            return _activeLeg;
        }

        public IRouteLeg GetLegToPoint(IRoutePoint routePoint)
        {
            lock (_routeLegsLock)
            {
                if (_activeLeg != null && _activeLeg.EndPoint.Point.Equals(routePoint))
                {
                    return _activeLeg;
                }

                foreach (IRouteLeg leg in _routeLegs)
                {
                    if (leg.EndPoint.Point.Equals(routePoint))
                    {
                        return leg;
                    }
                }
            }

            return null;
        }

        public void ActivateDirectTo(IRoutePoint routePoint, Bearing course = null)
        {
            lock (_routeLegsLock)
            {
                int index = -1;
                FmsPoint point = null;

                if (_activeLeg != null && _activeLeg.StartPoint != null && _activeLeg.StartPoint.Point.Equals(routePoint))
                {
                    point = _activeLeg.StartPoint;
                    _routeLegs.Insert(0, _activeLeg);
                    index = 0;
                }
                else if (_activeLeg != null && _activeLeg.EndPoint != null && _activeLeg.EndPoint.Point.Equals(routePoint))
                {
                    point = _activeLeg.EndPoint;
                    index = 0;
                }
                else
                {
                    foreach (IRouteLeg leg in _routeLegs)
                    {
                        if (leg.StartPoint != null && leg.StartPoint.Point.Equals(routePoint))
                        {
                            index = _routeLegs.IndexOf(leg);
                            point = leg.StartPoint;
                            break;
                        }
                        else if (leg.EndPoint != null && leg.EndPoint.Point.Equals(routePoint))
                        {
                            index = _routeLegs.IndexOf(leg) + 1;
                            point = leg.EndPoint;
                            break;
                        }
                    }
                }

                if (point == null)
                {
                    point = new FmsPoint(routePoint, RoutePointTypeEnum.FLY_BY);
                }

                // Create direct leg
                IRouteLeg dtoLeg = new DirectToFixLeg(point);

                if (course != null)
                {
                    dtoLeg = new CourseToFixLeg(point, BearingTypeEnum.MAGNETIC, course, _magTileMgr);
                }

                _activeLeg = dtoLeg;
                _wpEvtTriggered = false;

                if (index >= 0)
                {
                    // Remove everything before index
                    _routeLegs.RemoveRange(0, index);
                }
                else
                {
                    _routeLegs.Insert(0, new DiscoLeg(dtoLeg.FinalTrueCourse));
                }

                RecalculatePerformance();
            }
        }

        public bool AddHold(IRoutePoint rp, Bearing magCourse, HoldTurnDirectionEnum turnDir, HoldLegLengthTypeEnum legLengthType, double legLength)
        {
            lock (_routeLegsLock)
            {
                int index = -1;
                FmsPoint point = null;

                if (_activeLeg != null && _activeLeg.EndPoint != null && _activeLeg.EndPoint.Point.Equals(rp))
                {
                    index = 0;
                    point = _activeLeg.EndPoint;
                }
                else
                {
                    foreach (IRouteLeg leg in _routeLegs)
                    {
                        if (leg.EndPoint != null && leg.EndPoint.Point.Equals(rp))
                        {
                            index = _routeLegs.IndexOf(leg) + 1;
                            point = leg.EndPoint;
                            break;
                        }
                    }
                }

                if (index >= 0)
                {

                    point.PointType = RoutePointTypeEnum.FLY_OVER;

                    // Create hold leg
                    IRouteLeg holdLeg = new HoldToManualLeg(point, BearingTypeEnum.MAGNETIC, magCourse, turnDir, legLengthType, legLength, _magTileMgr);

                    // Add leg
                    _routeLegs.Insert(index, holdLeg);
                    RecalculatePerformance();
                    return true;
                }
            }
            return false;
        }

        public IRouteLeg GetFirstLeg()
        {
            lock (_routeLegsLock)
            {
                if (_routeLegs.Count < 1)
                {
                    return null;
                }

                return _routeLegs[0];
            }
        }

        public void RemoveFirstLeg()
        {
            lock (_routeLegsLock)
            {
                if (_routeLegs.Count >= 1)
                {
                    _routeLegs.RemoveAt(0);

                    RecalculatePerformance();
                }
            }
        }

        public bool ShouldActivateLnav(int intervalMs)
        {
            if (ActiveLeg != null)
            {
                return ActiveLeg.ShouldActivateLeg(_parentAircraft, intervalMs);
            }

            IRouteLeg leg = GetFirstLeg();

            return leg?.ShouldActivateLeg(_parentAircraft, intervalMs) ?? false;
        }

        public (Bearing requiredTrueCourse, Length crossTrackError, Length alongTrackDistance, Length turnRadius) CourseInterceptInfo => (_requiredTrueCourse, _xTk, _aTk, _turnRadius);

        private bool ShouldStartTurnToNextLeg(int intervalMs)
        {
            if (ActiveLeg == null || ActiveLeg.HasLegTerminated(_parentAircraft))
            {
                return true;
            }

            // If this and next leg connect, and a turn is involved
            IRouteLeg nextLeg = GetFirstLeg();

            return nextLeg != null &&
                ActiveLeg.EndPoint != null &&
                ActiveLeg.EndPoint.PointType == RoutePointTypeEnum.FLY_BY &&
                nextLeg.StartPoint != null &&
                ActiveLeg.EndPoint.Point.Equals(nextLeg.StartPoint.Point) &&
                ActiveLeg.FinalTrueCourse != null && nextLeg.InitialTrueCourse != null &&
                Math.Abs((nextLeg.InitialTrueCourse - ActiveLeg.FinalTrueCourse).Degrees) > 0.5 &&
                nextLeg.ShouldActivateLeg(_parentAircraft, intervalMs) && !Suspended;
        }

        private bool ShouldSequenceNextLeg(int intervalMs)
        {
            if (ActiveLeg == null || ActiveLeg.HasLegTerminated(_parentAircraft))
            {
                return true;
            }

            // If this and next leg connect, and a turn is involved
            IRouteLeg nextLeg = GetFirstLeg();

            if (ShouldStartTurnToNextLeg(intervalMs))
            {
                Bearing startBearing = ActiveLeg.FinalTrueCourse;
                Bearing endBearing = nextLeg.InitialTrueCourse;
                Angle turnAmt = endBearing - startBearing;

                // Find half turn and see if we've crossed abeam the point
                Bearing bisectorRadial = startBearing + (turnAmt / 2);

                (_, Length bisectorAtk, _) = AviationUtil.CalculateLinearCourseIntercept(_parentAircraft.Position.PositionGeoPoint, ActiveLeg.EndPoint.Point.PointPosition, bisectorRadial);

                return bisectorAtk.Meters <= 0;
            }

            return false;
        }

        public void OnPositionUpdate(int intervalMs)
        {
            // Calculate distance to destination
            var distanceToDest = AlongTrackDistance;
            lock (_routeLegsLock) {
                foreach (var leg in _routeLegs)
                {
                    if (leg.LegLength > (Length)0)
                    {
                        distanceToDest += leg.LegLength;
                    }
                }
            }

            // Method to set FMS phase
            SetPhase();

            // Set FMS Speed
            (_spdUnits, _selSpd) = CalculateFmsSpeed(PhaseType, distanceToDest, _parentAircraft.Position.IndicatedAltitude);

            var position = _parentAircraft.Position;

            // Activate next leg if there's no active leg
            if (ActiveLeg == null)
            {
                if (GetFirstLeg() == null)
                {
                    return;
                }

                ActivateNextLeg();
            }

            // Process Leg
            ActiveLeg.ProcessLeg(_parentAircraft, intervalMs);

            // Check if leg has terminated
            bool hasLegTerminated = ShouldSequenceNextLeg(intervalMs);

            // Trigger Waypoint Passed if it has
            if (_aTk.Meters > 0 && ActiveLeg.EndPoint != null && ActiveLeg.FinalTrueCourse != null)
            {
                (_, Length act_atk_m, _) = AviationUtil.CalculateLinearCourseIntercept(_parentAircraft.Position.PositionGeoPoint, ActiveLeg.EndPoint.Point.PointPosition, ActiveLeg.FinalTrueCourse);
                if (act_atk_m.Meters <= 0 || hasLegTerminated)
                {
                    if (!_wpEvtTriggered)
                    {
                        WaypointPassed?.Invoke(this, new WaypointPassedEventArgs(ActiveLeg.EndPoint.Point));
                        _wpEvtTriggered = true;
                    }
                }
            }

            // Check if we should start turning towards the next leg
            IRouteLeg nextLeg = GetFirstLeg();

            // Only sequence if next leg exists and fms is not suspended
            if (nextLeg != null && !Suspended)
            {
                if (hasLegTerminated)
                {
                    // Activate next leg on termination
                    ActivateNextLeg();
                }
                else if (ShouldStartTurnToNextLeg(intervalMs))
                {
                    // Begin turn to next leg, but do not activate
                    nextLeg.ProcessLeg(_parentAircraft, intervalMs);
                    (_requiredTrueCourse, _xTk, _aTk, _turnRadius) = nextLeg.GetCourseInterceptInfo(_parentAircraft);
                    return;
                }
            }

            // Calculate course values
            (_requiredTrueCourse, _xTk, _aTk, _turnRadius) = ActiveLeg.GetCourseInterceptInfo(_parentAircraft);

            // Check if we need to recalculate remaining legs and VNAV crossing altitudes
            if (Math.Abs((_lastGs - position.GroundSpeed).MetersPerSecond) > MIN_GS_DIFF.MetersPerSecond)
            {
                RecalculateVnavPath();
                _lastGs = position.GroundSpeed;
            }

            // Calculate VNAV values
            (_requiredFpa, _vTk) = GetPitchInterceptInfoForCurrentLeg();
        }

        private void SetPhase()
        {
            if (PhaseType == FmsPhaseType.CLIMB && Math.Abs(_parentAircraft.Position.IndicatedAltitude.Feet - CruiseAltitude) < 50)
            {
                PhaseType = FmsPhaseType.CRUISE;
            }
            else if (PhaseType == FmsPhaseType.CRUISE && _parentAircraft.Autopilot.SelectedAltitude < CruiseAltitude - 100 &&
                (_parentAircraft.Autopilot.CurrentVerticalMode != VerticalModeType.ALT || _parentAircraft.Autopilot.CurrentVerticalMode != VerticalModeType.VALT))
            {
                PhaseType = FmsPhaseType.DESCENT;
            }
            // TODO: Need to implement Airport Distance
            else if (PhaseType == FmsPhaseType.DESCENT && _parentAircraft.Position.IndicatedAltitude < _parentAircraft.RelaventAirport.Elevation + Length.FromFeet(5000))
            {
                PhaseType = FmsPhaseType.APPROACH;
            }
            else if (PhaseType == FmsPhaseType.GO_AROUND && _parentAircraft.Position.IndicatedAltitude > _parentAircraft.RelaventAirport.Elevation + Length.FromFeet(1300))
            {
                PhaseType = FmsPhaseType.APPROACH;
            }
        }

        private (McpSpeedUnitsType units, int selectedSpeed) CalculateFmsSpeed(FmsPhaseType fmsPhase, Length distanceToDest, Length indicatedAltitude)
        {
            if (PhaseType == FmsPhaseType.CLIMB)
            {
                if (indicatedAltitude < DepartureAirportElevation + Length.FromFeet(1000))
                {
                    return (McpSpeedUnitsType.KNOTS, _parentAircraft.PerformanceData.V2_KIAS);
                } else if (indicatedAltitude < DepartureAirportElevation + Length.FromFeet(3000))
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(210, PerfInit.ClimbKias));
                } else if (indicatedAltitude.Feet < PerfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(PerfInit.ClimbKias, PerfInit.LimitSpeed));
                } else
                {
                    if (PerfInit.ClimbMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, PerfInit.ClimbKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(PerfInit.ClimbKias), PerfInit.ClimbMach / 100.0);
                }
            } else if (PhaseType == FmsPhaseType.CRUISE)
            {
                if (CruiseAltitude < PerfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, PerfInit.LimitSpeed);
                } else
                {
                    if (PerfInit.CruiseMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, PerfInit.CruiseKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(PerfInit.CruiseKias), PerfInit.CruiseMach / 100.0);
                }
            } else if (PhaseType == FmsPhaseType.DESCENT)
            {
                if (indicatedAltitude.Feet < PerfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(PerfInit.DescentKias, PerfInit.LimitSpeed));
                } else
                {
                    if (PerfInit.DescentMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, PerfInit.DescentKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(PerfInit.DescentKias), PerfInit.DescentMach / 100.0);
                }
            } else if (PhaseType == FmsPhaseType.APPROACH)
            {
                var speedGates = _parentAircraft.PerformanceData.ApproachSpeedGates;
                
                if (speedGates == null || speedGates.Count <= 0)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(PerfInit.DescentKias, PerfInit.LimitSpeed));
                }

                // Use distance from threshold to determine approach speed
                foreach ((int distance, int speed) in speedGates)
                {
                    if (distanceToDest.NauticalMiles <= distance)
                    {
                        return (McpSpeedUnitsType.KNOTS, speed);
                    }
                }

                return (McpSpeedUnitsType.KNOTS, speedGates[speedGates.Count - 1].Item2);
            } else
            {
                // Go Around
                return (McpSpeedUnitsType.KNOTS, 135); // TODO: DEFINITELY CHANGE THIS!!!!!!!!!!!!!
            }
        }

        private (McpSpeedUnitsType units, int selectedSpeed) GetConversionSpeed(Velocity ias, double mach)
        {
            double curIasMach;
            var gribPoint = _parentAircraft.Position.GribPoint;
            if (gribPoint != null)
            {
                (_, curIasMach) = AtmosUtil.ConvertIasToTas(ias, gribPoint.LevelPressure, _parentAircraft.Position.TrueAltitude, gribPoint.GeoPotentialHeight, gribPoint.Temp);
            }
            else
            {
                (_, curIasMach) = AtmosUtil.ConvertIasToTas(ias, AtmosUtil.ISA_STD_PRES, _parentAircraft.Position.TrueAltitude, (Length) 0, AtmosUtil.ISA_STD_TEMP);
            }

            if (curIasMach >= mach)
            {
                return (McpSpeedUnitsType.MACH, (int)(mach * 100));
            }
            else
            {
                return (McpSpeedUnitsType.KNOTS, (int) ias.Knots);
            }
        }

        

        private double GetKnotsSpeed(McpSpeedUnitsType units, int speed, Length altitude, GribDataPoint gribPoint)
        {
            if (units == McpSpeedUnitsType.MACH)
            {
                Temperature t0 = gribPoint != null ? gribPoint.Temp : AtmosUtil.ISA_STD_TEMP;
                Length h0 = gribPoint != null ? gribPoint.GeoPotentialHeight : Length.FromMeters(0);
                Pressure p0 = gribPoint != null ? gribPoint.LevelPressure : AtmosUtil.ISA_STD_PRES;
                Temperature t = AtmosUtil.CalculateTempAtAlt(altitude, h0, t0);
                
                Velocity tas = AtmosUtil.ConvertMachToTas(speed / 100.0, t);
                return AtmosUtil.ConvertTasToIas(tas, p0, altitude, h0, t0).ias.Knots;
            }

            return speed;
        }

        public void RecalculatePerformance()
        {
            RecalculateVnavPath();
        }

        private void RecalculateVnavPath()
        {
            lock (_routeLegsLock) {
                int legIndex = _routeLegs.Count - 1;
                double apchAngle = -1;
                double distanceToRwy = 0.0;
                double lastAlt = -1;

                // Loop through legs backwards
                while (legIndex >= -1)
                {
                    IRouteLeg curLeg = legIndex >= 0 ? _routeLegs[legIndex] : _activeLeg;

                    // Get approach angle
                    if (apchAngle < 0)
                    {
                        apchAngle = curLeg.EndPoint.AngleConstraint > 0 ? curLeg.EndPoint.AngleConstraint : 3.0;
                    }

                    // Target Angle (for idle descent)
                    double targetAngle = 0;
                    if (distanceToRwy < 15)
                    {
                        targetAngle = apchAngle;
                    } else
                    {
                        // Calculate idle descent angle

                    }
                }
            }


            // Psuedo Code:
            // Loop through legs from last to first ending either when first leg is reached or cruise alt is reached
            // Each iteration:
            //      Use altitude last waypoint was crossed at
            //      Calculate idle/approach descent angle
            //      Figure out what start point altitude should be
            //          Respecting "further up the road" constraint
            //      Insert VNAV Point at the end point
            //      If necessary:
            //          Insert VNAV Point for decel to comply with speed restriction.
            //          If there was a "further up the road" speed constraint, then the decel should be from that speed to the new constraint at end point
            //              e.g.: 220kts from SOFEE -> 210kt at GOSHI
            //          else assume Perf Init speeds
            //              e.g.: 290kts -> 260kts at ROBUC
            //      If speed/altitude restriction is discovered that the current path will violate
            //          Return to the index where newly discovered constraint is still met
            //              e.g.: If we get to GOSHI and realize we are too high, return to WINNI and build a level off point
        }

        private (Angle requiredFpa, Length vTk_m) GetPitchInterceptInfoForCurrentLeg()
        {
            if (_activeLeg.EndPoint == null || _activeLeg.EndPoint.VnavTargetAltitude < 0 || _activeLeg.EndPoint.AngleConstraint < 0)
            {
                return ((Angle)0, (Length)0);
            }

            // TODO: Change this to actually figure out the angle
            Angle requiredFpa = Angle.FromDegrees(_activeLeg.EndPoint.AngleConstraint);

            // Calculate how much altitude we still need to climb/descend from here to the EndPoint
            Length deltaAlt_m = Length.FromMeters(Math.Tan(requiredFpa.Radians) * _aTk.Meters);

            // Add to Vnav target alt
            Length altTarget_m = deltaAlt_m + Length.FromFeet(_activeLeg.EndPoint.VnavTargetAltitude);

            Length vTk_m = _parentAircraft.Position.TrueAltitude - altTarget_m;

            return (requiredFpa, vTk_m);
        }
    }
}
