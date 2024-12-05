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
using NavData_Interface.Objects.LegCollections.Procedures;
using NavData_Interface.Objects.LegCollections.Legs;
using AviationCalcUtilNet.Atmos.Grib;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using AviationCalcUtilNet.Physics;
using AviationCalcUtilNet.Math;
using Newtonsoft.Json.Linq;
using SaunaSim.Core.Simulator.Aircraft.FMS.VNAV;

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
        private Velocity _lastGs = (Velocity)0;
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

        private int _routeStartIndex = 0;

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
        private FmsDeparture _dep;
        public FmsDeparture Dep
        {
            get => _dep;
        }
        private FmsArrival _arr;
        public FmsArrival Arr
        {
            get => _arr;
            set
            {
                _arr = value;
                RecalculatePerformance();
            }
        }

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
                } catch (ArgumentOutOfRangeException ex) { }
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

        public void AddAllLegs(IList<IRouteLeg> routeLegs)
        {
            lock (_routeLegsLock)
            {
                _routeLegs.AddRange(routeLegs);
                RecalculateVnavPath();
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
                } else if (_activeLeg != null && _activeLeg.EndPoint != null && _activeLeg.EndPoint.Point.Equals(routePoint))
                {
                    point = _activeLeg.EndPoint;
                    index = 0;
                } else
                {
                    foreach (IRouteLeg leg in _routeLegs)
                    {
                        if (leg.StartPoint != null && leg.StartPoint.Point.Equals(routePoint))
                        {
                            index = _routeLegs.IndexOf(leg);
                            point = leg.StartPoint;
                            break;
                        } else if (leg.EndPoint != null && leg.EndPoint.Point.Equals(routePoint))
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
                } else
                {
                    _routeLegs.Insert(0, new DiscoLeg(dtoLeg.FinalTrueCourse));
                }

                RecalculatePerformance();
            }
        }

        /// <summary>
        /// Adds a whole SID to the FMS
        /// </summary>
        /// <param name="sid">The SID to add. Relevant transition and runway transition MUST be selected beforehand.</param>
        /// <returns></returns>
        public bool AddSid(Sid sid)
        {
            lock (_routeLegsLock)
            {
                if (_routeStartIndex != 0)
                {
                    RemoveSid();
                }

                try
                {
                    foreach (var leg in LegFactory.RouteLegsFromNavDataLegs(sid.GetEnumerator(), _magTileMgr))
                    {
                        InsertAtIndex(leg, _routeStartIndex);
                        _routeStartIndex++;
                    }

                } catch (Exception ex)
                {
                    return false;
                }

            }

            // TODO: Set the FmsDeparture info
            _dep = new FmsDeparture
            {

            };
            return true;
        }

        public void RemoveSid()
        {
            while (_routeStartIndex > 0)
            {
                RemoveFirstLeg();
                _routeStartIndex--;
            }

            _dep = new FmsDeparture();

            RecalculatePerformance();
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
                } else
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
            lock (_routeLegsLock)
            {
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
            (_spdUnits, _selSpd) = FmsVnavUtil.CalculateFmsSpeed(PhaseType, distanceToDest, _parentAircraft.Position.IndicatedAltitude, _parentAircraft.PerformanceData, DepartureAirportElevation, PerfInit, _parentAircraft.Position.GribPoint);

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
                } else if (ShouldStartTurnToNextLeg(intervalMs))
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
                //RecalculateVnavPath();
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
            } else if (PhaseType == FmsPhaseType.CRUISE && _parentAircraft.Autopilot.SelectedAltitude < CruiseAltitude - 100 &&
                  (_parentAircraft.Autopilot.CurrentVerticalMode != VerticalModeType.ALT || _parentAircraft.Autopilot.CurrentVerticalMode != VerticalModeType.VALT))
            {
                PhaseType = FmsPhaseType.DESCENT;
            }
              // TODO: Need to implement Airport Distance
              else if (PhaseType == FmsPhaseType.DESCENT && _parentAircraft.Position.IndicatedAltitude < _parentAircraft.RelaventAirport.Elevation + Length.FromFeet(5000))
            {
                PhaseType = FmsPhaseType.APPROACH;
            } else if (PhaseType == FmsPhaseType.GO_AROUND && _parentAircraft.Position.IndicatedAltitude > _parentAircraft.RelaventAirport.Elevation + Length.FromFeet(1300))
            {
                PhaseType = FmsPhaseType.APPROACH;
            }
        }

        public void RecalculatePerformance()
        {
            RecalculateVnavPath();
        }

        private IRouteLeg GetVnavLeg(int index)
        {
            if (index == -1) return _activeLeg;
            if (index >= 0 && index < _routeLegs.Count) return _routeLegs[index];
            return null;
        }        

        private void RecalculateVnavPath()
        {
            lock (_routeLegsLock)
            {
                if (_routeLegs.Count > 0 || _activeLeg != null)
                {
                    var iterator = new FmsVnavLegIterator
                    {
                        Index = _routeLegs.Count - 1,
                        NextLegIndex = _routeLegs.Count,
                        MoveDir = 0,
                        AlongTrackDistance = Length.FromMeters(0),
                        ApchAngle = null,
                        DistanceToRwy = null,
                        EarlyUpperAlt = null,
                        EarlyUpperAltIndex = -1,
                        EarlySpeedSearch = false,
                        EarlySpeed = -1,
                        EarlySpeedIndex = -1,
                        EarlyUpperAltSearch = false,
                        DecelDist = null,
                        DecelSpeed = -1,
                        Finished = false
                    };

                    // Loop through legs from last to first ending either when first leg is reached or cruise alt is reached
                    while (iterator.Index >= -1 && !iterator.Finished)
                    {
                        iterator = VnavDescentUtil.ProcessLegForDescent(iterator, GetVnavLeg, _parentAircraft.PerformanceData, PerfInit, _parentAircraft.Data.Mass_kg, DepartureAirportElevation);
                    }
                }
            }
        }

        private (Angle requiredFpa, Length vTk_m) GetPitchInterceptInfoForCurrentLeg()
        {
            if (_activeLeg.EndPoint == null || _activeLeg.EndPoint.VnavPoints == null || _activeLeg.EndPoint.VnavPoints.Count == 0)
            {
                return ((Angle)0, (Length)0);
            }

            int i = _activeLeg.EndPoint.VnavPoints.Count - 1;
            FmsVnavPoint targetPoint = _activeLeg.EndPoint.VnavPoints[i];

            while (i > 0 && _aTk > targetPoint.AlongTrackDistance)
            {
                i++;
                targetPoint = _activeLeg.EndPoint.VnavPoints[i];
            }

            Angle requiredFpa = targetPoint.Angle;

            // Calculate how much altitude we still need to climb/descend from here to the EndPoint
            Length deltaAlt_m = Length.FromMeters(Math.Tan(requiredFpa.Radians) * (_aTk.Meters - targetPoint.AlongTrackDistance.Meters));

            // Add to Vnav target alt
            Length altTarget_m = deltaAlt_m + targetPoint.Alt;

            Length vTk_m = _parentAircraft.Position.TrueAltitude - altTarget_m;

            return (requiredFpa, vTk_m);
        }
    }
}
