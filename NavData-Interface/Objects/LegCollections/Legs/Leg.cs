using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.Fixes;
using NavData_Interface.Objects.Fixes.Navaids;
using NavData_Interface.Objects.Fixes.Waypoints;
using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Legs
{
    public enum LegType
    {
        INITIAL_FIX,
        TRACK_TO_FIX,
        COURSE_TO_FIX,
        DIRECT_TO_FIX,
        FIX_TO_ALT,
        TRACK_TO_DISTANCE,
        TRACK_TO_DME,
        FIX_TO_MANUAL,
        COURSE_TO_ALT,
        COURSE_TO_DME,
        COURSE_TO_INTC,
        COURSE_TO_RADIAL,
        RADIUS_TO_FIX,
        ARC_TO_FIX,
        HEADING_TO_ALT,
        HEADING_TO_DME,
        HEADING_TO_INTC,
        HEADING_TO_MANUAL,
        HEADING_TO_RADIAL,
        PROCEDURE_TURN,
        HOLD_TO_ALT,
        HOLD_TO_FIX,
        HOLD_TO_MANUAL,
    }

    public enum SpeedRestrictionType
    {
        BELOW,
        AT,
        ABOVE
    }

    public enum RequiredTurnDirectionType
    {
        LEFT,
        RIGHT,
    }

    public class Leg
    {
        public LegType Type;

        public Velocity SpeedRestriction { get; internal set; }

        public SpeedRestrictionType? SpeedType { get; internal set; }

        public Length LowerAltitudeConstraint { get; }

        public Length UpperAltitudeConstraint { get; }

        public Fix EndPoint { get; }

        public Fix CenterPoint { get; }

        public Navaid RecommendedNavaid { get; }

        public WaypointDescription EndPointDescription { get; }

        public bool IsFinalLeg => EndPointDescription.IsEndOfRoute;

        public Bearing Theta { get; }

        public Bearing OutboundMagneticCourse { get; }

        public RequiredTurnDirectionType? RequiredTurnDirection { get; }

        public Length ArcRadius { get; }

        public HoldLegLengthTypeEnum? LegLengthType { get; }

        public double? LegLength { get; }

        public static LegType parseLegType(string legType)
        {
            switch (legType)
            {
                case "IF":
                    return LegType.INITIAL_FIX;
                case "TF":
                    return LegType.TRACK_TO_FIX;
                case "CF":
                    return LegType.COURSE_TO_FIX;
                case "DF":
                    return LegType.DIRECT_TO_FIX;
                case "FA":
                    return LegType.FIX_TO_ALT;
                case "FC":
                    return LegType.TRACK_TO_DISTANCE;
                case "FD":
                    return LegType.TRACK_TO_DME;
                case "FM":
                    return LegType.FIX_TO_MANUAL;
                case "CA":
                    return LegType.COURSE_TO_ALT;
                case "CD":
                    return LegType.COURSE_TO_DME;
                case "CI":
                    return LegType.COURSE_TO_INTC;
                case "CR":
                    return LegType.COURSE_TO_RADIAL;
                case "RF":
                    return LegType.RADIUS_TO_FIX;
                case "AF":
                    return LegType.ARC_TO_FIX;
                case "VA":
                    return LegType.HEADING_TO_ALT;
                case "VD":
                    return LegType.HEADING_TO_DME;
                case "VI":
                    return LegType.HEADING_TO_INTC;
                case "VM":
                    return LegType.HEADING_TO_MANUAL;
                case "VR":
                    return LegType.HEADING_TO_RADIAL;
                case "PI":
                    return LegType.PROCEDURE_TURN;
                case "HA":
                    return LegType.HOLD_TO_ALT;
                case "HF":
                    return LegType.HOLD_TO_FIX;
                case "MH":
                    return LegType.HOLD_TO_MANUAL;
                default:
                    // Should never reach here. However, if it does, this will probably make the leg get ignored
                    return LegType.INITIAL_FIX;
            }
        }

        public Leg(LegType type,
                    Velocity speedRestriction,
                    SpeedRestrictionType? speedType,
                    Length lowerAltitudeConstraint,
                    Length higherAltitudeConstraint,
                    Fix endPoint,
                    WaypointDescription endPointDescription,
                    Fix centerPoint,
                    Navaid recommendedNavaid,
                    Bearing theta,
                    Bearing outboundMagneticCourse,
                    RequiredTurnDirectionType? requiredTurnDirection,
                    Length arcRadius,
                    HoldLegLengthTypeEnum? legLengthType,
                    double? legLength)
        {
            Type = type;
            SpeedRestriction = speedRestriction;
            SpeedType = speedType;
            LowerAltitudeConstraint = lowerAltitudeConstraint;
            UpperAltitudeConstraint = higherAltitudeConstraint;
            EndPoint = endPoint;
            CenterPoint = centerPoint;
            RecommendedNavaid = recommendedNavaid;
            EndPointDescription = endPointDescription;
            OutboundMagneticCourse = outboundMagneticCourse;
            RequiredTurnDirection = requiredTurnDirection;
            ArcRadius = arcRadius;
            LegLengthType = legLengthType ;
            LegLength = legLength;
        }

        public override string ToString()
        {
            string lowerAltitudeConstraintString = LowerAltitudeConstraint != null ? $"Alt: {LowerAltitudeConstraint.Feet:F0}" : "Alt: N/A";
            string upperAltitudeConstraintString = UpperAltitudeConstraint != null ? $" - {UpperAltitudeConstraint.Feet:F0} feet" : string.Empty;

            return $"{Enum.GetName(typeof(LegType), Type)} | " +
                   $"{(SpeedRestriction != null ? $"Speed: {SpeedRestriction.Knots:F0}kts" : "No Speed")} | " +
                   $"{(SpeedType.HasValue ? $"Speed Type: {Enum.GetName(typeof(SpeedRestrictionType), SpeedType)}" : "No Speed Type")} | " +
                   $"{lowerAltitudeConstraintString}{upperAltitudeConstraintString} | " +
                   $"End: {EndPoint?.Identifier} | " +
                   $"{(CenterPoint != null ? $"Center: {CenterPoint.Identifier}" : "No Center")} | " +
                   $"{(RecommendedNavaid != null ? $"Navaid: {RecommendedNavaid.Identifier}" : "No Navaid")} | " +
                   $"{EndPointDescription} | " +
                   $"Theta: {Theta?.Degrees:F0} | " +
                   $"Course: {OutboundMagneticCourse?.Degrees:F0} | " +
                   $"{(RequiredTurnDirection.HasValue ? $"Turn: {Enum.GetName(typeof(RequiredTurnDirectionType), RequiredTurnDirection)}" : "No Turn")} | " +
                   $"{(ArcRadius != null ? $"Arc Radius: {ArcRadius.NauticalMiles:F0}" : "No Arc Radius")} | " +
                   $"{(LegLengthType.HasValue ? $"Leg Length Type: {Enum.GetName(typeof(HoldLegLengthTypeEnum), LegLengthType)}" : "No Leg Length Type")} | " +
                   $"{(LegLength.HasValue ? $"Leg Length: {LegLength:F0}" : "No Leg Length")}";
        }

    }
}
