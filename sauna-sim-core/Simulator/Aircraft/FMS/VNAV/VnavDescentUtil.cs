using AviationCalcUtilNet.Atmos.Grib;
using AviationCalcUtilNet.Atmos;
using AviationCalcUtilNet.Aviation;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.Units;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.FMS.VNAV
{
    public static class VnavDescentUtil
    {
        public static Length CalculateDecelLength(int targetIas_kts, int initialIas_kts, Length alt, Length densAlt, double acftMass_kg, Bearing finalTrueCourse, PerfData perfData, GribDataPoint gribPoint)
        {
            // Get deceleration at level flight at speedconstraint
            var lvlPitch = PerfDataHandler.GetRequiredPitchForVs(perfData, 0, targetIas_kts, densAlt.Feet, acftMass_kg, 0, 0);
            (var decelRate, _) = PerfDataHandler.CalculatePerformance(perfData, lvlPitch, 0, targetIas_kts, densAlt.Feet, acftMass_kg, 0, 0);

            // Calculate initial and final velocities
            var (initialVelocity, _) = AtmosUtil.ConvertIasToTas(Velocity.FromKnots(initialIas_kts), gribPoint.LevelPressure, alt, gribPoint.GeoPotentialHeight, gribPoint.Temp);
            var (finalVelocity, _) = AtmosUtil.ConvertIasToTas(Velocity.FromKnots(targetIas_kts), gribPoint.LevelPressure, alt, gribPoint.GeoPotentialHeight, gribPoint.Temp);
            var (windDir, windSpd) = gribPoint.Wind;
            var headWind = AviationUtil.GetHeadwindComponent(windDir, windSpd, finalTrueCourse);
            initialVelocity += headWind;
            finalVelocity += headWind;

            return Length.FromMeters((Math.Pow(finalVelocity.MetersPerSecond, 2) - Math.Pow(initialVelocity.MetersPerSecond, 2)) / (double)Acceleration.FromKnotsPerSecond(decelRate * 2)); // Vf^2 - Vi^2 / (2a)
        }

        private static FmsVnavLegIterator GoToNextVnavDescentLeg(IRouteLeg curLeg, IRouteLeg nextLeg, FmsVnavLegIterator iterator)
        {
            if (curLeg == null || curLeg.EndPoint == null || curLeg.LegLength <= Length.FromMeters(0))
            {
                throw new ArgumentException("curLeg was not a valid VNAV leg!");
            }

            if (nextLeg != null && (nextLeg.EndPoint == null || nextLeg.LegLength <= Length.FromMeters(0)))
            {
                throw new ArgumentException("nextLeg was not a valid VNAV leg!");
            }

            // if (curLeg.EndPoint.VnavPoints == null || curLeg.EndPoint.VnavPoints.Count < 1)
            // {
            //     throw new ArgumentException("curLeg's VnavPoints must have at least 1 point!");
            // }

            if (curLeg.EndPoint.VnavPoints.Count < 2 && (nextLeg == null || nextLeg.EndPoint.VnavPoints == null || nextLeg.EndPoint.VnavPoints.Count < 1))
            {
                throw new IndexOutOfRangeException("nextLeg did not have any VNAV Points or is not a valid leg!");
            }

            // Clean up iterator
            iterator.DecelDist = null;
            iterator.DecelSpeed = -1;

            // Remove last VNAV point
            if (curLeg.EndPoint.VnavPoints != null && curLeg.EndPoint.VnavPoints.Count > 0)
            {
                curLeg.EndPoint.VnavPoints.RemoveAt(curLeg.EndPoint.VnavPoints.Count - 1);

                // If current leg still has VNAV Points
                if (curLeg.EndPoint.VnavPoints.Count > 0)
                {
                    iterator.AlongTrackDistance = curLeg.EndPoint.VnavPoints[curLeg.EndPoint.VnavPoints.Count - 1].AlongTrackDistance;
                    return iterator;
                }
            }

            // Move to next leg
            iterator.MoveDir = 1;
            iterator.DistanceToRwy -= nextLeg.LegLength;
            iterator.AlongTrackDistance = nextLeg.EndPoint.VnavPoints[nextLeg.EndPoint.VnavPoints.Count - 1].AlongTrackDistance;

            return iterator;
        }

        private static (FmsVnavPoint lastVnavPoint, FmsVnavLegIterator iterator, Length distance) GetLastVnavDescentPoint(IRouteLeg curLeg, IRouteLeg nextLeg, FmsVnavLegIterator iterator, PerfData perfData, PerfInit perfInit, double mass_kg, Length depArptElev)
        {
            Length distance = Length.FromMeters(0);

            // ---  2. Get Last VNAV point
            var curEndPoint = curLeg.EndPoint;
            FmsVnavPoint? lastVnavPoint = null;
            if (curEndPoint.VnavPoints != null && curEndPoint.VnavPoints.Count > 0)
            {
                lastVnavPoint = curEndPoint.VnavPoints[curEndPoint.VnavPoints.Count - 1];
                distance = iterator.AlongTrackDistance - lastVnavPoint.Value.AlongTrackDistance;
            } else if (nextLeg != null && nextLeg.EndPoint.VnavPoints != null && nextLeg.EndPoint.VnavPoints.Count > 0)
            {
                lastVnavPoint = nextLeg.EndPoint.VnavPoints[nextLeg.EndPoint.VnavPoints.Count - 1];
                distance = nextLeg.LegLength - lastVnavPoint.Value.AlongTrackDistance;
            }

            // ---  3. Initialize first leg
            if (!lastVnavPoint.HasValue)
            {
                var alt = curEndPoint.LowerAltitudeConstraint > 0 ? Length.FromFeet(curEndPoint.LowerAltitudeConstraint) : Length.FromMeters(0);
                var startGribPoint = FmsVnavUtil.GetGribPointForLeg(curLeg, alt);
                iterator.DistanceToRwy = Length.FromMeters(0);
                (var targetSpeedUnits, var targetSpeed) = FmsVnavUtil.CalculateFmsSpeed(FmsPhaseType.APPROACH, Length.FromMeters(0), alt, perfData, depArptElev, perfInit, startGribPoint);
                iterator.AlongTrackDistance = Length.FromMeters(0);

                // Calculate approach angle
                iterator.ApchAngle = curEndPoint.AngleConstraint > 0 ? Angle.FromDegrees(curEndPoint.AngleConstraint) : Angle.FromDegrees(3.0);

                // Set last VNAV Point
                lastVnavPoint = new FmsVnavPoint()
                {
                    AlongTrackDistance = iterator.AlongTrackDistance,
                    Alt = alt,
                    Angle = iterator.ApchAngle,
                    Speed = targetSpeed,
                    SpeedUnits = targetSpeedUnits,
                    CmdSpeed = targetSpeed,
                    CmdSpeedUnits = targetSpeedUnits,
                };
                curEndPoint.VnavPoints.Add(lastVnavPoint.Value);
            }

            return (lastVnavPoint.Value, iterator, distance);
        }

        /// <summary>
        /// Completes an iteration for a leg for a VNAV descent calculation.
        /// </summary>
        /// <param name="iterator">Iterator object</param>
        /// <param name="getLegFunc">A Function that takes an index and returns an IRouteLeg</param>
        /// <returns>Modified iterator object</returns>
        public static FmsVnavLegIterator ProcessLegForDescent(FmsVnavLegIterator iterator, Func<int, IRouteLeg> getLegFunc, PerfData perfData, PerfInit perfInit, double mass_kg, Length depArptElev)
        {
            // ---  Process iteration
            IRouteLeg curLeg, nextLeg;
            try
            {
                (curLeg, iterator.Index, nextLeg, iterator.NextLegIndex) = FmsVnavUtil.IterateVnav(iterator.MoveDir, iterator.Index, iterator.NextLegIndex, getLegFunc);
            } catch (IndexOutOfRangeException)
            {
                iterator.Finished = true;
                return iterator;
            }
            iterator.MoveDir = 0;

            // ---  Get Last VNAV Point
            FmsVnavPoint lastVnavPoint;
            Length lastVnavPointDist;
            (lastVnavPoint, iterator, lastVnavPointDist) = GetLastVnavDescentPoint(curLeg, nextLeg, iterator, perfData, perfInit, mass_kg, depArptElev);
            

            // ---  Check for constraint search
            var gribPoint = FmsVnavUtil.GetGribPointForLeg(curLeg, lastVnavPoint.Alt);

            if (iterator.EarlySpeedSearch)
            {
                if (FmsVnavUtil.GetKnotsSpeed(lastVnavPoint.CmdSpeedUnits, lastVnavPoint.CmdSpeed, lastVnavPoint.Alt, gribPoint) <= iterator.EarlySpeed)
                {
                    iterator.EarlySpeedSearch = false;
                    return iterator;
                }

                try
                {
                    return GoToNextVnavDescentLeg(curLeg, nextLeg, iterator);
                } catch (IndexOutOfRangeException)
                {
                    // Give up searching for speed and force last vnav point to speed
                    iterator.EarlySpeedSearch = false;
                    iterator.MoveDir = 0;
                    lastVnavPoint.CmdSpeed = iterator.EarlySpeed;
                    lastVnavPoint.CmdSpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                    lastVnavPoint.Speed = iterator.EarlySpeed;
                    lastVnavPoint.SpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                    return iterator;
                }
            }

            if (iterator.EarlyUpperAltSearch)
            {
                if (lastVnavPoint.Alt <= iterator.EarlyUpperAlt)
                {
                    iterator.EarlyUpperAltSearch = false;
                    iterator.MoveDir = 0;
                    return iterator;
                }
                try
                {
                    return GoToNextVnavDescentLeg(curLeg, nextLeg, iterator);
                } catch (IndexOutOfRangeException)
                {
                    // Give up searching for speed
                    iterator.EarlyUpperAltSearch = false;
                    iterator.MoveDir = 0;
                    lastVnavPoint.Alt = iterator.EarlyUpperAlt;
                    return iterator;
                }
            }

            // ---  Clear earlier constraints
            if (iterator.AlongTrackDistance <= Length.FromMeters(0))
            {
                if (iterator.Index == iterator.EarlySpeedIndex)
                {
                    iterator.EarlySpeed = -1;
                    iterator.EarlySpeedIndex = -1;
                }

                if (iterator.Index == iterator.EarlyUpperAltIndex)
                {
                    iterator.EarlyUpperAltIndex = -1;
                    iterator.EarlyUpperAlt = null;
                }
            }

            // ---  Move to previous leg
            if (iterator.AlongTrackDistance >= curLeg.LegLength)
            {
                iterator.MoveDir = -1;
                iterator.AlongTrackDistance = Length.FromMeters(0);
                iterator.DistanceToRwy += curLeg.LegLength;
                return iterator;
            }

            // ---  Calculate Parameters
            // Set current phase
            var currentPhase = FmsPhaseType.DESCENT;
            if (iterator.DistanceToRwy + iterator.AlongTrackDistance < Length.FromNauticalMiles(15))
            {
                currentPhase = FmsPhaseType.APPROACH;
            }

            // Calculate current alt and grib point
            var curAlt = FmsVnavUtil.CalculateStartAltitude(lastVnavPoint.Alt, lastVnavPointDist, lastVnavPoint.Angle);
            gribPoint = FmsVnavUtil.GetGribPointForLeg(curLeg, curAlt);
            var curDensAlt = FmsVnavUtil.CalculateDensityAltitude(curAlt, gribPoint);

            // Calculate target speed
            (var targetSpeedUnits, var targetSpeed) = FmsVnavUtil.CalculateFmsSpeed(currentPhase, iterator.DistanceToRwy + iterator.AlongTrackDistance, curAlt, perfData, depArptElev, perfInit, gribPoint);
            var targetSpeedInKts = FmsVnavUtil.GetKnotsSpeed(targetSpeedUnits, targetSpeed, curAlt, gribPoint);

            // Check for decel speed
            if (iterator.DecelSpeed > 0)
            {
                iterator.DecelDist = CalculateDecelLength(iterator.DecelSpeed, Convert.ToInt32(Math.Round(targetSpeedInKts, MidpointRounding.AwayFromZero)), curAlt, curDensAlt, mass_kg, curLeg.FinalTrueCourse, perfData, gribPoint);
                targetSpeed = iterator.DecelSpeed;
                targetSpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                iterator.DecelSpeed = -1;
            }

            // ---  Handle Constraints
            if (iterator.AlongTrackDistance <= Length.FromMeters(0))
            {
                // Does speed constraint exist
                if (currentPhase == FmsPhaseType.DESCENT &&
                    (curLeg.EndPoint.SpeedConstraintType == ConstraintType.LESS || curLeg.EndPoint.SpeedConstraintType == ConstraintType.EXACT) &&
                    curLeg.EndPoint.SpeedConstraint > 0)
                {
                    // Was speed constraint violated
                    if (lastVnavPoint.CmdSpeedUnits == Autopilot.McpSpeedUnitsType.MACH || lastVnavPoint.CmdSpeed > curLeg.EndPoint.SpeedConstraint)
                    {
                        // Go back to last time speed constraint was followed
                        iterator.EarlySpeed = Convert.ToInt32(Math.Round(curLeg.EndPoint.SpeedConstraint, MidpointRounding.AwayFromZero));
                        iterator.EarlySpeedIndex = iterator.Index;
                        iterator.EarlySpeedSearch = true;
                        try
                        {
                            return GoToNextVnavDescentLeg(curLeg, nextLeg, iterator);
                        } catch (IndexOutOfRangeException)
                        {
                            iterator.EarlySpeedSearch = false;
                            iterator.EarlySpeed = -1;
                            iterator.EarlySpeedIndex = -1;
                            iterator.MoveDir = 0;
                            lastVnavPoint.CmdSpeed = iterator.EarlySpeed;
                            lastVnavPoint.CmdSpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                            lastVnavPoint.Speed = iterator.EarlySpeed;
                            lastVnavPoint.SpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                        }
                    }
                    if (targetSpeedInKts > curLeg.EndPoint.SpeedConstraint)
                    {
                        // Calculate Decel Length
                        iterator.DecelDist = CalculateDecelLength(
                            Convert.ToInt32(Math.Round(curLeg.EndPoint.SpeedConstraint, MidpointRounding.AwayFromZero)),
                            Convert.ToInt32(Math.Round(targetSpeedInKts, MidpointRounding.AwayFromZero)),
                            curAlt, curDensAlt, mass_kg, curLeg.FinalTrueCourse, perfData, gribPoint);
                        targetSpeed = Convert.ToInt32(Math.Round(curLeg.EndPoint.SpeedConstraint, MidpointRounding.AwayFromZero));
                        targetSpeedUnits = Autopilot.McpSpeedUnitsType.KNOTS;
                    }
                }

                // Does upper alt constraint exist
                if (curLeg.EndPoint.UpperAltitudeConstraint > 0 && curAlt > Length.FromFeet(curLeg.EndPoint.UpperAltitudeConstraint))
                {
                    iterator.EarlyUpperAlt = Length.FromFeet(curLeg.EndPoint.UpperAltitudeConstraint);
                    iterator.EarlyUpperAltIndex = iterator.Index;
                    iterator.EarlyUpperAltSearch = true;
                    try
                    {
                        return GoToNextVnavDescentLeg(curLeg, nextLeg, iterator);
                    } catch (IndexOutOfRangeException)
                    {
                        iterator.EarlyUpperAlt = null;
                        iterator.EarlyUpperAltIndex = -1;
                        iterator.EarlyUpperAltSearch = false;
                        iterator.MoveDir = 0;
                        lastVnavPoint.Alt = Length.FromFeet(curLeg.EndPoint.UpperAltitudeConstraint);
                        curAlt = lastVnavPoint.Alt;
                    }
                }
            }

            targetSpeedInKts = FmsVnavUtil.GetKnotsSpeed(targetSpeedUnits, targetSpeed, curAlt, gribPoint);

            // ---  Calculate Angle
            Angle targetAngle = Angle.FromDegrees(3.0);
            if (iterator.DecelDist != null || iterator.EarlyUpperAlt >= curAlt)
            {
                targetAngle = Angle.FromRadians(0);
            } else if (currentPhase == FmsPhaseType.APPROACH)
            {
                targetAngle = iterator.ApchAngle;
            } else
            {
                // Calculate idle descent angle
                var pitch = PerfDataHandler.GetRequiredPitchForThrust(perfData, 0, 0, targetSpeedInKts, curDensAlt.Feet, mass_kg, 0, 0);
                var vs = Velocity.FromFeetPerMinute(PerfDataHandler.CalculatePerformance(perfData, pitch, 0, targetSpeedInKts, curDensAlt.Feet, mass_kg, 0, 0).vs * 0.9);
                var tas = AtmosUtil.ConvertIasToTas(Velocity.FromKnots(targetSpeedInKts), gribPoint.LevelPressure, curAlt, gribPoint.GeoPotentialHeight, gribPoint.Temp).tas;
                var gs = tas + AviationUtil.GetHeadwindComponent(gribPoint.Wind.windDir, gribPoint.Wind.windSpd, curLeg.FinalTrueCourse);
                targetAngle = -AviationUtil.CalculateFlightPathAngle(gs, vs);

                if (targetAngle < Angle.FromRadians(0))
                {
                    targetAngle = Angle.FromRadians(0);
                }
            }

            // ---  Add VNAV Point
            var newVnavPt = new FmsVnavPoint
            {
                AlongTrackDistance = iterator.AlongTrackDistance,
                Alt = curAlt,
                Speed = targetSpeed,
                Angle = targetAngle,
                SpeedUnits = targetSpeedUnits,
                CmdSpeed = iterator.DecelDist != null ? lastVnavPoint.CmdSpeed : targetSpeed,
                CmdSpeedUnits = iterator.DecelDist != null ? lastVnavPoint.CmdSpeedUnits : targetSpeedUnits,
            };
            if (curLeg.EndPoint.VnavPoints == null)
            {
                curLeg.EndPoint.VnavPoints = new List<FmsVnavPoint>();
                curLeg.EndPoint.VnavPoints.Add(newVnavPt);
            }
            else
            {
                var foundVnavPt = false;
                foreach (var vnavPt in curLeg.EndPoint.VnavPoints)
                {
                    if (vnavPt.AlongTrackDistance == iterator.AlongTrackDistance)
                    {
                        foundVnavPt = true;
                        break;
                    }
                }

                if (!foundVnavPt)
                {
                    curLeg.EndPoint.VnavPoints.Add(newVnavPt);
                }
            }

            // ---  Adjust iterator
            var newAlongTrack = curLeg.LegLength;

            // Adjust decelDist
            if (iterator.DecelDist != null)
            {
                if (iterator.DecelDist + iterator.AlongTrackDistance < curLeg.LegLength)
                {
                    newAlongTrack = iterator.AlongTrackDistance + iterator.DecelDist;
                    iterator.DecelDist = null;
                } else
                {
                    iterator.DecelDist -= (curLeg.LegLength - iterator.AlongTrackDistance);
                }
            } else if (targetAngle > Angle.FromRadians(0))
            {
                var startAlt = FmsVnavUtil.CalculateStartAltitude(curAlt, newAlongTrack - iterator.AlongTrackDistance, targetAngle);
                var limitAlt = Length.FromFeet(perfInit.LimitAlt);
                var cruiseAlt = Length.FromFeet(perfInit.CruiseAlt);

                // Check if cruise alt was reached
                if (startAlt >= cruiseAlt && curAlt < cruiseAlt)
                {
                    iterator.Finished = true;
                    return iterator;
                }
                // Check for limit alt crossing
                if (startAlt >= limitAlt && curAlt < limitAlt && targetSpeedInKts > perfInit.LimitSpeed)
                {
                    newAlongTrack = FmsVnavUtil.CalculateDistanceForAltitude(curAlt, limitAlt, targetAngle);
                }
                if (iterator.EarlyUpperAlt != null && startAlt > iterator.EarlyUpperAlt && iterator.EarlyUpperAlt < limitAlt)
                {
                    newAlongTrack = FmsVnavUtil.CalculateDistanceForAltitude(curAlt, iterator.EarlyUpperAlt, targetAngle);
                }
            }

            if (iterator.DistanceToRwy + iterator.AlongTrackDistance < Length.FromNauticalMiles(15) && iterator.DistanceToRwy + newAlongTrack > Length.FromNauticalMiles(15))
            {
                newAlongTrack = Length.FromNauticalMiles(15) - iterator.DistanceToRwy;
                iterator.DecelSpeed = Convert.ToInt32(Math.Round(targetSpeedInKts, MidpointRounding.AwayFromZero));
            }

            iterator.AlongTrackDistance = newAlongTrack;

            return iterator;
        }
    }
}
