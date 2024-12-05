using AviationCalcUtilNet.Aviation;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.Fixes;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS.NavDisplay;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.FMS.Legs
{
    public class RadiusToFixLeg : IRouteLeg
    {
        private FmsPoint _startPoint;
        private FmsPoint _endPoint;

        public FmsPoint StartPoint => _startPoint;

        public FmsPoint EndPoint => _endPoint;

        private Bearing _initialTrueCourse;
        private Bearing _finalTrueCourse;
        private Length _legLength;

        public Length LegLength => _legLength;

        public Bearing InitialTrueCourse => _initialTrueCourse;

        public Bearing FinalTrueCourse => _finalTrueCourse;

        public RouteLegTypeEnum LegType => RouteLegTypeEnum.RADIUS_TO_FIX;

        private TurnCircle _turnCircle;

        public TurnCircle ArcInfo => _turnCircle;

        private TrackToFixLeg _trackToRFLeg;
        private TrackToFixLeg _trackFromRFLeg;

        public class TurnCircle
        {
            public GeoPoint Center { get; set; }

            public GeoPoint TangentialPointA { get; set; }
            public Bearing PointARadial => GeoPoint.InitialBearing(Center, TangentialPointA);

            public GeoPoint TangentialPointB { get; set; }
            public Bearing PointBRadial => GeoPoint.InitialBearing(Center, TangentialPointB);

            public Length RadiusM { get; set; }

            public override string ToString()
            {
                return $"Center: ({Center.Lat}, {Center.Lon}) Radius (m): {RadiusM}";
            }

            public TurnCircle(GeoPoint center, GeoPoint tangentialPointA, GeoPoint tangentialPointB, Length radiusM)
            {
                Center = center;
                TangentialPointA = tangentialPointA;
                TangentialPointB = tangentialPointB;
                RadiusM = radiusM;
            }

            public TurnCircle(GeoPoint center, GeoPoint tangentialPointA, GeoPoint tangentialPointB) : this(center, tangentialPointA, tangentialPointB, GeoPoint.Distance(center, tangentialPointA)) 
            {}

            public GeoPoint BisectorIntersection { get; set; }
        }

        public class InvalidTurnCircleException : Exception
        {
            TurnCircle Circle { get; }
            GeoPoint StartPoint { get; }
            GeoPoint EndPoint { get; }

            Bearing InitialTrueCourse { get; }
            Bearing FinalTrueCourse { get; }

            public InvalidTurnCircleException(TurnCircle c, GeoPoint startPoint, GeoPoint endPoint, Bearing initialTrueCourse, Bearing finalTrueCourse) : base("The turnCircle doesn't seem to be valid")
            {
                Circle = c;
                StartPoint = startPoint;
                EndPoint = endPoint;
                InitialTrueCourse = initialTrueCourse;
                FinalTrueCourse = finalTrueCourse;
            }
        }

        private enum RfState
        {
            TRACK_TO_RF,
            IN_RF,
            TRACK_FROM_RF
        }

        private RfState _legState = RfState.TRACK_TO_RF;

        public RadiusToFixLeg(FmsPoint startPoint, FmsPoint endPoint, Fix centerPoint, Bearing initialTrueCourse, Bearing finalTrueCourse)
        {
            _startPoint = startPoint;
            _endPoint = endPoint;
            _initialTrueCourse = initialTrueCourse;
            _finalTrueCourse = finalTrueCourse;

            _turnCircle = new TurnCircle(centerPoint.Location, startPoint.Point.PointPosition, endPoint.Point.PointPosition);
            
            // This leg is defined by NavData, so there are no sublegs, just the turn.
            // _legLength = rfTurnLength
            (_, _legLength, _) = AviationUtil.CalculateArcCourseIntercept(_turnCircle.TangentialPointA, _turnCircle.Center, InitialTrueCourse, FinalTrueCourse, _turnCircle.RadiusM, isClockwise());
        }

        public RadiusToFixLeg(FmsPoint startPoint, FmsPoint endPoint, Bearing initialTrueCourse, Bearing finalTrueCourse)
        {
            _startPoint = startPoint;
            _endPoint = endPoint;
            _initialTrueCourse = initialTrueCourse;
            _finalTrueCourse = finalTrueCourse;

            CalculateTurnCircle();

            // Initial subleg is Track To RF

            if (!StartPoint.Point.PointPosition.Equals(_turnCircle.TangentialPointA))
            {
                _trackToRFLeg = new TrackToFixLeg(StartPoint, new FmsPoint(new RouteWaypoint(_turnCircle.TangentialPointA), RoutePointTypeEnum.FLY_OVER));
                _legState = RfState.TRACK_TO_RF;
            } else
            {
                _legState = RfState.IN_RF;
            }

            if (!EndPoint.Point.PointPosition.Equals(_turnCircle.TangentialPointB))
            {
                _trackFromRFLeg = new TrackToFixLeg(new FmsPoint(new RouteWaypoint(_turnCircle.TangentialPointB), RoutePointTypeEnum.FLY_OVER), EndPoint);
            }

            Length toRFAlongTrackDistance = GeoPoint.Distance(StartPoint.Point.PointPosition, _turnCircle.TangentialPointA);
            (_, Length rfTurnLength, _) = AviationUtil.CalculateArcCourseIntercept(_turnCircle.TangentialPointA, _turnCircle.Center, InitialTrueCourse, FinalTrueCourse, _turnCircle.RadiusM, isClockwise());

            Length fromRFAlongTrackDistance = GeoPoint.Distance(_turnCircle.TangentialPointB, EndPoint.Point.PointPosition);

            _legLength = toRFAlongTrackDistance + rfTurnLength + fromRFAlongTrackDistance;
        }

        public void CalculateTurnCircle()
        {
            Angle turnAmount = FinalTrueCourse - InitialTrueCourse;

            GeoPoint bisectorIntersection;

            if (Math.Abs(turnAmount.Degrees) < 3 || Math.Abs(turnAmount.Degrees) > 177) // TODO: Figure out the margin of error. Probably less than 3
            {
                GeoPoint turnCircleCenter;
                Length turnCircleRadiusM;
                
                // Calculate tangential circle to parallel legs:
                (_, Length startToEndAlongTrackDistance, _) = AviationUtil.CalculateLinearCourseIntercept(StartPoint.Point.PointPosition, EndPoint.Point.PointPosition, FinalTrueCourse);
                if (startToEndAlongTrackDistance.Meters > 0) 
                {
                    // if we turn at StartPoint, we'll have some distance to go to EndPoint
                    // thus, we'll turn at StartPoint

                    Bearing diameterCourse = InitialTrueCourse + Angle.FromDegrees(90);
                    GeoPoint tangentialPointA = StartPoint.Point.PointPosition;
                    GeoPoint tangentialPointB = GeoPoint.FindClosestIntersection(StartPoint.Point.PointPosition, diameterCourse, EndPoint.Point.PointPosition, FinalTrueCourse);

                    turnCircleRadiusM = GeoPoint.Distance(tangentialPointA, tangentialPointB) / 2;

                    // diameterCourse is the correct direction but not necessarily going the correct way
                    // we'll recalculate it now that we know both tangential points, so it goes the right way
                    diameterCourse = GeoPoint.InitialBearing(tangentialPointA, tangentialPointB);

                    turnCircleCenter = (GeoPoint) tangentialPointA.Clone();
                    turnCircleCenter.MoveBy(diameterCourse, turnCircleRadiusM);
                } else
                {
                    // if we turn at StartPoint, we'll be past EndPoint.
                    // we'll turn when we're abeam EndPoint

                    Bearing diameterCourse = FinalTrueCourse + Angle.FromDegrees(90);
                    GeoPoint tangentialPointA = GeoPoint.FindClosestIntersection(EndPoint.Point.PointPosition, diameterCourse, StartPoint.Point.PointPosition, InitialTrueCourse);
                    GeoPoint tangentialPointB = EndPoint.Point.PointPosition;

                    turnCircleRadiusM = GeoPoint.Distance(tangentialPointB, tangentialPointA) / 2;

                    // diameterCourse is the correct direction but not necessarily going the correct way
                    // we'll recalculate it now that we know both tangential points, so it goes the right way
                    diameterCourse = GeoPoint.InitialBearing(tangentialPointB, tangentialPointA);

                    turnCircleCenter = (GeoPoint)tangentialPointB.Clone();
                    turnCircleCenter.MoveBy(diameterCourse, turnCircleRadiusM);
                }
                _turnCircle = new TurnCircle(turnCircleCenter, StartPoint.Point.PointPosition, EndPoint.Point.PointPosition, turnCircleRadiusM);
            } else
            {
                // Calculate tangential circle to crossing legs:

                // Calculate bisector of both legs
                bisectorIntersection = GeoPoint.FindClosestIntersection(StartPoint.Point.PointPosition, InitialTrueCourse, EndPoint.Point.PointPosition, FinalTrueCourse);

                // Calculate the courses again because great circle
                Bearing bisectorStartRadial = GeoPoint.InitialBearing(bisectorIntersection, StartPoint.Point.PointPosition);
                Bearing bisectorEndRadial = GeoPoint.InitialBearing(bisectorIntersection, EndPoint.Point.PointPosition);

                // Handle cases where we turn > 180 degrees. In these cases the bisectorRadials are not reciprocals of the courses for either start or end
                if (Math.Abs((bisectorStartRadial - InitialTrueCourse).Degrees) < 2 && Math.Abs((bisectorEndRadial - FinalTrueCourse).Degrees) < 2)
                {
                    bisectorEndRadial += Angle.FromDegrees(180);
                }

                Bearing bisectorRadial = bisectorStartRadial + ((bisectorEndRadial - bisectorStartRadial) / 2);
                // The bisector is now defined by bisectorIntersection and bisectorCourse

                // Figure out which of the two posible turn circles we want:
                (_, Length intersectionToStartPointAlongTrackDistance, _) = AviationUtil.CalculateLinearCourseIntercept(bisectorIntersection, StartPoint.Point.PointPosition, InitialTrueCourse);

                // either A or B, whichever point we'll be crossing *while in the turn*
                GeoPoint referenceTangentPoint;

                // the two tangent points of the circle. One of them is A or B, whichever one referenceTangentPoint is, but we don't care.
                GeoPoint tangentPointA;
                GeoPoint tangentPointB;

                // the course for the line perpendicular to the leg of the referenceTangentPoint that goes through referenceTangentPoint
                Bearing referenceTangentPointPerpendicularCourse;

                // The center of the circle is the intersection between the bisector and the perpendicular line
                GeoPoint turnCircleCenter;

                if (intersectionToStartPointAlongTrackDistance.Meters < 0)
                {
                    // This means we're heading into the intersection.
                    // The reference tangent point should be the closest one to the intersection
                    if (GeoPoint.Distance(StartPoint.Point.PointPosition, bisectorIntersection) < GeoPoint.Distance(EndPoint.Point.PointPosition, bisectorIntersection)) {
                        referenceTangentPointPerpendicularCourse = InitialTrueCourse + Angle.FromDegrees(90); // perpendicular to StartPoint
                        referenceTangentPoint = StartPoint.Point.PointPosition;
                        tangentPointA = StartPoint.Point.PointPosition;

                        turnCircleCenter = GeoPoint.FindClosestIntersection(bisectorIntersection, bisectorRadial, referenceTangentPoint, referenceTangentPointPerpendicularCourse);

                        // Calculate the other tangent point
                        Bearing tangentPointBPerpendicularCourse = FinalTrueCourse + Angle.FromDegrees(90);
                        tangentPointB = GeoPoint.FindClosestIntersection(EndPoint.Point.PointPosition, FinalTrueCourse + Angle.FromDegrees(180), turnCircleCenter, tangentPointBPerpendicularCourse);
                    } else
                    {
                        referenceTangentPointPerpendicularCourse = FinalTrueCourse + Angle.FromDegrees(90); // perpendicular to EndPoint
                        referenceTangentPoint = EndPoint.Point.PointPosition;
                        tangentPointB = EndPoint.Point.PointPosition;

                        turnCircleCenter = GeoPoint.FindClosestIntersection(bisectorIntersection, bisectorRadial, referenceTangentPoint, referenceTangentPointPerpendicularCourse);
                        // Calculate the other tangent point
                        Bearing tangentPointAPerpendicularCourse = InitialTrueCourse + Angle.FromDegrees(90);
                        tangentPointA = GeoPoint.FindClosestIntersection(StartPoint.Point.PointPosition, InitialTrueCourse, turnCircleCenter, tangentPointAPerpendicularCourse);
                    }
                }
                else
                {
                    // This means we're heading away from the intersection.
                    // The reference tangent point should be the furthest one to the intersection
                    if (GeoPoint.Distance(StartPoint.Point.PointPosition, bisectorIntersection) > GeoPoint.Distance(EndPoint.Point.PointPosition, bisectorIntersection)) {
                        referenceTangentPointPerpendicularCourse = InitialTrueCourse + Angle.FromDegrees(90); // perpendicular to StartPoint
                        referenceTangentPoint = StartPoint.Point.PointPosition;
                        tangentPointA = StartPoint.Point.PointPosition;

                        turnCircleCenter = GeoPoint.FindClosestIntersection(bisectorIntersection, bisectorRadial, referenceTangentPoint, referenceTangentPointPerpendicularCourse);
                        // Calculate the other tangent point
                        Bearing tangentPointBPerpendicularCourse = FinalTrueCourse + Angle.FromDegrees(90);
                        tangentPointB = GeoPoint.FindClosestIntersection(EndPoint.Point.PointPosition, FinalTrueCourse + Angle.FromDegrees(180), turnCircleCenter, tangentPointBPerpendicularCourse);
                    }
                    else
                    {
                        referenceTangentPointPerpendicularCourse = FinalTrueCourse + Angle.FromDegrees(90); // perpendicular to EndPoint
                        referenceTangentPoint = EndPoint.Point.PointPosition;
                        tangentPointB = EndPoint.Point.PointPosition;

                        turnCircleCenter = GeoPoint.FindClosestIntersection(bisectorIntersection, bisectorRadial, referenceTangentPoint, referenceTangentPointPerpendicularCourse);

                        // Calculate the other tangent point
                        Bearing tangentPointAPerpendicularCourse = InitialTrueCourse + Angle.FromDegrees(90);
                        tangentPointA = GeoPoint.FindClosestIntersection(StartPoint.Point.PointPosition, InitialTrueCourse, turnCircleCenter, tangentPointAPerpendicularCourse);
                    }
                }

                Length turnCircleRadius = GeoPoint.Distance(referenceTangentPoint, turnCircleCenter);

                _turnCircle = new TurnCircle(turnCircleCenter, tangentPointA, tangentPointB, turnCircleRadius);
                _turnCircle.BisectorIntersection = bisectorIntersection;

                if (turnCircleRadius.Meters > 50000)
                {
                    throw new InvalidTurnCircleException(_turnCircle, StartPoint.Point.PointPosition, EndPoint.Point.PointPosition, InitialTrueCourse, FinalTrueCourse);
                }
            }

            // Calculate leg length
            _legLength = Length.FromMeters(0);
            if (_trackToRFLeg != null)
            {
                _legLength += _trackFromRFLeg.LegLength;
            }
            if (_turnCircle != null)
            {
                (_, Length rfArcLength, _) = AviationUtil.CalculateArcCourseIntercept(_turnCircle.TangentialPointA, _turnCircle.Center, _turnCircle.PointARadial, _turnCircle.PointBRadial, _turnCircle.RadiusM, isClockwise());
                _legLength += rfArcLength;
            }
            if (_trackFromRFLeg != null)
            {
                _legLength += _trackFromRFLeg.LegLength;
            }
        }

        public bool HasLegTerminated(SimAircraft aircraft)
        {
            (_, _, Length alongTrackDistance, _) = GetCourseInterceptInfo(aircraft);
            return alongTrackDistance.Meters <= 0;
        }

        private bool isClockwise()
        {
            // Calculate the shortest turn from the initial leg to a leg inbound the turnCircle Center
            Angle turnAmount = GeoPoint.InitialBearing(_turnCircle.TangentialPointA, _turnCircle.Center) - InitialTrueCourse;
            // If the turnAmount is greater than 0, it is a right-hand turn, thus, a clockwise turn
            return turnAmount.Degrees > 0;
        }

        public void ProcessLeg(SimAircraft aircraft, int intervalMs)
        {
            switch (_legState)
            {
                case RfState.TRACK_TO_RF:
                    HandleTrackToRF(aircraft, intervalMs);
                    break;
                case RfState.IN_RF:
                    HandleRFTurn(aircraft, intervalMs);
                    break;
                case RfState.TRACK_FROM_RF:
                    break;
            }
        }

        public void HandleRFTurn(SimAircraft aircraft, int intervalMs)
        {
            (Bearing requiredTrueCourse, Length alongTrackDistanceM, _) = AviationUtil.CalculateArcCourseIntercept(aircraft.Position.PositionGeoPoint, _turnCircle.Center, _turnCircle.PointARadial, _turnCircle.PointBRadial, _turnCircle.RadiusM, isClockwise());

            if (alongTrackDistanceM.Meters < 0 && _trackFromRFLeg != null)
            {
                _legState = RfState.TRACK_FROM_RF;
            }
        }

        public void HandleTrackToRF(SimAircraft aircraft, int intervalMs)
        {
            if (_trackToRFLeg.HasLegTerminated(aircraft))
            {
                _legState = RfState.IN_RF;
                HandleRFTurn(aircraft, intervalMs);
            }
        }

        public (Bearing requiredTrueCourse, Length crossTrackError, Length alongTrackDistance, Length turnRadius) GetCourseInterceptInfo(SimAircraft aircraft)
        {
            // Depending on how far along the leg we are, we'll add the along track distances of the relevant internal legs.

            Length alongTrackDistance = new Length(0);
            Length crossTrackError = new Length(0);
            Bearing requiredTrueCourse = Bearing.FromDegrees(0);
            Length turnRadius = new Length(0);

            switch (_legState)
            {
                case RfState.TRACK_TO_RF:
                    Length toRFAlongTrackDistance;
                    (requiredTrueCourse, crossTrackError, toRFAlongTrackDistance, _) = _trackToRFLeg.GetCourseInterceptInfo(aircraft);
                    (_, Length rfTurnLength, _) = AviationUtil.CalculateArcCourseIntercept(_turnCircle.TangentialPointA, _turnCircle.Center, InitialTrueCourse, FinalTrueCourse, _turnCircle.RadiusM, isClockwise());

                    Length fromRFAlongTrackDistance = GeoPoint.Distance(_turnCircle.TangentialPointB, EndPoint.Point.PointPosition);

                    alongTrackDistance = toRFAlongTrackDistance + rfTurnLength + fromRFAlongTrackDistance;

                    break;
                case RfState.IN_RF:
                    (requiredTrueCourse, rfTurnLength, crossTrackError) = AviationUtil.CalculateArcCourseIntercept(aircraft.Position.PositionGeoPoint, _turnCircle.Center, _turnCircle.PointARadial, _turnCircle.PointBRadial, _turnCircle.RadiusM, isClockwise());
                    turnRadius = _turnCircle.RadiusM * (isClockwise() ? 1 : -1);

                    if (_trackFromRFLeg != null)
                    {
                        fromRFAlongTrackDistance = GeoPoint.Distance(_turnCircle.TangentialPointB, EndPoint.Point.PointPosition);
                    } else
                    {
                        fromRFAlongTrackDistance = new Length(0);
                    }
                    
                    alongTrackDistance = rfTurnLength + fromRFAlongTrackDistance;

                    break;
                case RfState.TRACK_FROM_RF:
                    if(_trackFromRFLeg != null)
                    {
                        (requiredTrueCourse, crossTrackError, fromRFAlongTrackDistance, _) = _trackFromRFLeg.GetCourseInterceptInfo(aircraft);
                        alongTrackDistance = fromRFAlongTrackDistance;
                    } else
                    {
                        goto case RfState.IN_RF;
                    }
                    
                    break;
            }

            return (requiredTrueCourse, crossTrackError, alongTrackDistance, turnRadius);
        }

        public bool ShouldActivateLeg(SimAircraft aircraft, int intervalMs)
        {
            return AviationUtil.CalculateLinearCourseIntercept(aircraft.Position.PositionGeoPoint, StartPoint.Point.PointPosition, InitialTrueCourse).crossTrackError.Meters < 0;
        }

        public void InitializeLeg(SimAircraft aircraft)
        {
        }

        public override string ToString()
        {
            return $"({StartPoint}) (RF) => ({EndPoint}) TurnCircle: {_turnCircle}\n" +
                $"Legs: \n" +
                $"S->Ta: ({_trackToRFLeg})\n" +
                $"Tb->E: ({_trackFromRFLeg})";
        }

        public List<NdLine> UiLines
        {
            get
            {
                var retList = new List<NdLine>();
                if (!StartPoint.Point.PointPosition.Equals(_turnCircle.TangentialPointA))
                {
                    retList.Add(new NdLine(StartPoint.Point.PointPosition, _turnCircle.TangentialPointA));
                }

                retList.Add(new NdArc(_turnCircle.TangentialPointA, _turnCircle.TangentialPointB, _turnCircle.Center, _turnCircle.RadiusM.Meters, _turnCircle.PointARadial.Degrees, _turnCircle.PointBRadial.Degrees, isClockwise()));
                if (!EndPoint.Point.PointPosition.Equals(_turnCircle.TangentialPointB))
                {
                    retList.Add(new NdLine(_turnCircle.TangentialPointB, EndPoint.Point.PointPosition));
                }

                // Debugging
                /*if (_turnCircle.BisectorIntersection != null)
                {
                    retList.Add(new NdLine(_turnCircle.BisectorIntersection, StartPoint.Point.PointPosition));
                    retList.Add(new NdLine(_turnCircle.BisectorIntersection, EndPoint.Point.PointPosition));
                }*/

                return retList;
            }
        }
    }
}
