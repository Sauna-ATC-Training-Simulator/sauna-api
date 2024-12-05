using AviationCalcUtilNet.Atmos;
using AviationCalcUtilNet.Atmos.Grib;
using AviationCalcUtilNet.Aviation;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.Math;
using AviationCalcUtilNet.Units;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.FMS.VNAV
{
    public struct FmsVnavLegIterator
    {
        /// <summary>
        /// Leg Index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Index of next leg
        /// </summary>
        public int NextLegIndex { get; set; }

        /// <summary>
        /// Which direction to move
        /// </summary>
        public int MoveDir { get; set; }

        /// <summary>
        /// Along Track Distance
        /// </summary>
        public Length AlongTrackDistance { get; set; }

        /// <summary>
        /// Cumulative distance to destination runway. 
        /// Measured to the end of the current leg
        /// </summary>
        public Length DistanceToRwy { get; set; }

        /// <summary>
        /// Approach Angle (Only used in the approach phase)
        /// </summary>
        public Angle ApchAngle { get; set; }

        /// <summary>
        /// Deceleration distance.
        /// Used to figure out how much level off distance is left for a speed constraint
        /// </summary>
        public Length DecelDist { get; set; }

        /// <summary>
        /// Deceleration speed.
        /// Used to indicated to the current iteration that a speed constraint will follow.
        /// Assumed to be in knots (kts)
        /// </summary>
        public int DecelSpeed { get; set; }

        /// <summary>
        /// A Constraint from earlier on the route
        /// </summary>
        public int EarlySpeed { get; set; }

        /// <summary>
        /// Index where earlier speed constraint was encountered
        /// </summary>
        public int EarlySpeedIndex { get; set; }

        /// <summary>
        /// Indicates whether a new constraint was encountered and
        /// that retracing should occur to find when the constraint
        /// was last complied with.
        /// </summary>
        public bool EarlySpeedSearch { get; set; }

        /// <summary>
        /// A Constraint from earlier on the route
        /// </summary>
        public Length EarlyUpperAlt { get; set; }

        /// <summary>
        /// Index where earlier upper altitude constraint was encountered
        /// </summary>
        public int EarlyUpperAltIndex { get; set; }

        /// <summary>
        /// Indicates whether a new constraint was encountered and
        /// that retracing should occur to find when the constraint
        /// was last complied with.
        /// </summary>
        public bool EarlyUpperAltSearch { get; set; }

        public bool Finished { get; set; }
    }

    public static class FmsVnavUtil
    {
        private static (IRouteLeg curLeg, int curLegIndex, IRouteLeg nextLeg, int nextLegIndex) GoForwardVnav(int nextLegIndex, Func<int, IRouteLeg> getLegFunc)
        {
            var newCurLeg = getLegFunc(nextLegIndex) ?? throw new IndexOutOfRangeException("Cannot go forward one leg!");
            var newCurIndex = nextLegIndex;
            var newNextIndex = nextLegIndex + 1;
            var newNextLeg = getLegFunc(newNextIndex);

            while (newNextLeg != null && (newNextLeg.EndPoint == null || newNextLeg.LegLength <= Length.FromMeters(0)))
            {
                newNextIndex++;
                newNextLeg = getLegFunc(newNextIndex);
            }

            return (newCurLeg, newCurIndex, newNextLeg, newNextIndex);
        }

        private static (IRouteLeg curLeg, int curLegIndex, IRouteLeg nextLeg, int nextLegIndex) GoBackwardVnav(int curLegIndex, Func<int, IRouteLeg> getLegFunc)
        {
            var newNextIndex = curLegIndex;
            var newNextLeg = getLegFunc(curLegIndex);
            var newCurIndex = curLegIndex - 1;
            var newCurLeg = getLegFunc(newCurIndex) ?? throw new IndexOutOfRangeException("Cannot go backward one leg!");

            while (newCurLeg.EndPoint == null || newCurLeg.LegLength <= Length.FromMeters(0))
            {
                newCurIndex--;
                newCurLeg = getLegFunc(newCurIndex) ?? throw new IndexOutOfRangeException("Cannot go backward one leg!");
            }
            return (newCurLeg, newCurIndex, newNextLeg, newNextIndex);
        }

        public static (IRouteLeg curLeg, int curLegIndex, IRouteLeg nextLeg, int nextLegIndex) IterateVnav(int moveDir, int curLegIndex, int nextLegIndex, Func<int, IRouteLeg> getLegFunc)
        {
            var curLeg = getLegFunc(curLegIndex) ?? throw new IndexOutOfRangeException("Cannot get current leg!");
            var nextLeg = getLegFunc(nextLegIndex);

            if (curLeg.EndPoint == null || curLeg.LegLength <= Length.FromMeters(0))
            {
                curLegIndex = GoBackwardVnav(curLegIndex, getLegFunc).curLegIndex;
                return (getLegFunc(curLegIndex), curLegIndex, getLegFunc(nextLegIndex), nextLegIndex);
            }

            if (nextLeg != null && (nextLeg.EndPoint == null || nextLeg.LegLength <= Length.FromMeters(0)))
            {
                nextLegIndex = GoForwardVnav(nextLegIndex, getLegFunc).nextLegIndex;
                return (getLegFunc(curLegIndex), curLegIndex, getLegFunc(nextLegIndex), nextLegIndex);
            }

            if (moveDir > 0)
            {
                return GoForwardVnav(nextLegIndex, getLegFunc);
            }
            if (moveDir < 0)
            {
                return GoBackwardVnav(curLegIndex, getLegFunc);
            }

            return (getLegFunc(curLegIndex), curLegIndex, getLegFunc(nextLegIndex), nextLegIndex);
        }   

        public static Length CalculateStartAltitude(Length endAlt, Length legLength, Angle angle)
        {
            return endAlt + legLength * Math.Tan(angle.Radians);
        }

        public static Length CalculateDistanceForAltitude(Length endAlt, Length startAlt, Angle angle)
        {
            return (startAlt - endAlt) / Math.Tan(angle.Radians);
        }

        public static Length CalculateDensityAltitude(Length alt, GribDataPoint gribPoint)
        {
            var temp = AtmosUtil.CalculateTempAtAlt(alt, gribPoint.GeoPotentialHeight, gribPoint.Temp);
            var pressure = AtmosUtil.CalculatePressureAtAlt(alt, gribPoint.GeoPotentialHeight, gribPoint.LevelPressure, temp);
            return AtmosUtil.CalculateDensityAltitude(pressure, temp);
        }

        public static double GetKnotsSpeed(McpSpeedUnitsType units, int speed, Length altitude, GribDataPoint gribPoint)
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

        public static (McpSpeedUnitsType units, int selectedSpeed) GetConversionSpeed(Velocity ias, double mach, Length altitude, GribDataPoint gribPoint)
        {
            double curIasMach;
            if (gribPoint != null)
            {
                (_, curIasMach) = AtmosUtil.ConvertIasToTas(ias, gribPoint.LevelPressure, altitude, gribPoint.GeoPotentialHeight, gribPoint.Temp);
            }
            else
            {
                (_, curIasMach) = AtmosUtil.ConvertIasToTas(ias, AtmosUtil.ISA_STD_PRES, altitude, (Length)0, AtmosUtil.ISA_STD_TEMP);
            }

            if (curIasMach >= mach)
            {
                return (McpSpeedUnitsType.MACH, (int)(mach * 100));
            }
            else
            {
                return (McpSpeedUnitsType.KNOTS, (int)ias.Knots);
            }
        }

        public static (McpSpeedUnitsType units, int selectedSpeed) CalculateFmsSpeed(FmsPhaseType fmsPhase, Length distanceToDest, Length indicatedAltitude, PerfData perfData, Length departureAirportElevation, PerfInit perfInit, GribDataPoint gribPoint)
        {
            if (fmsPhase == FmsPhaseType.CLIMB)
            {
                if (indicatedAltitude < departureAirportElevation + Length.FromFeet(1000))
                {
                    return (McpSpeedUnitsType.KNOTS, perfData.V2_KIAS);
                }
                else if (indicatedAltitude < departureAirportElevation + Length.FromFeet(3000))
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(210, perfInit.ClimbKias));
                }
                else if (indicatedAltitude.Feet < perfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(perfInit.ClimbKias, perfInit.LimitSpeed));
                }
                else
                {
                    if (perfInit.ClimbMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, perfInit.ClimbKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(perfInit.ClimbKias), perfInit.ClimbMach / 100.0, indicatedAltitude, gribPoint);
                }
            }
            else if (fmsPhase == FmsPhaseType.CRUISE)
            {
                if (perfInit.CruiseAlt < perfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, perfInit.LimitSpeed);
                }
                else
                {
                    if (perfInit.CruiseMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, perfInit.CruiseKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(perfInit.CruiseKias), perfInit.CruiseMach / 100.0, indicatedAltitude, gribPoint);
                }
            }
            else if (fmsPhase == FmsPhaseType.DESCENT)
            {
                if (indicatedAltitude.Feet < perfInit.LimitAlt)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(perfInit.DescentKias, perfInit.LimitSpeed));
                }
                else
                {
                    if (perfInit.DescentMach <= 0)
                    {
                        return (McpSpeedUnitsType.KNOTS, perfInit.DescentKias);
                    }

                    return GetConversionSpeed(Velocity.FromKnots(perfInit.DescentKias), perfInit.DescentMach / 100.0, indicatedAltitude, gribPoint);
                }
            }
            else if (fmsPhase == FmsPhaseType.APPROACH)
            {
                var speedGates = perfData.ApproachSpeedGates;

                if (speedGates == null || speedGates.Count <= 0)
                {
                    return (McpSpeedUnitsType.KNOTS, Math.Min(perfInit.DescentKias, perfInit.LimitSpeed));
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
            }
            else
            {
                // Go Around
                return (McpSpeedUnitsType.KNOTS, 135); // TODO: DEFINITELY CHANGE THIS!!!!!!!!!!!!!
            }
        }

        public static GribDataPoint GetGribPointForLeg(IRouteLeg curLeg, Length alt)
        {
            return new GribDataPoint(curLeg.EndPoint.Point.PointPosition.Lat, curLeg.EndPoint.Point.PointPosition.Lon, AtmosUtil.ISA_STD_PRES); // TODO: Change to actually use wind-uplink.
        }
    }
}
