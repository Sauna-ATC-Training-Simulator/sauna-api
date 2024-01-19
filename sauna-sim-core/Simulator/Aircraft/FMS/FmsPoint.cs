﻿using AviationCalcUtilNet.Atmos.Grib;
using AviationCalcUtilNet.Units;
using System.Collections.Generic;

namespace SaunaSim.Core.Simulator.Aircraft.FMS
{
    public enum RoutePointTypeEnum
    {
        FLY_BY,
        FLY_OVER
    }

    public class FmsPoint
    {
        private IRoutePoint _point;
        private RoutePointTypeEnum _routePointType;

        public FmsPoint(IRoutePoint point, RoutePointTypeEnum type)
        {
            _point = point;
            _routePointType = type;
            GribPoints = new Dictionary<Length, GribDataPoint>();
        }

        public IRoutePoint Point => _point;

        public RoutePointTypeEnum PointType { get => _routePointType; set => _routePointType = value; }

        public Dictionary<Length, GribDataPoint> GribPoints { get; private set; }

        public int LowerAltitudeConstraint { get; set; }

        public int UpperAltitudeConstraint { get; set; }

        public double AngleConstraint { get; set; } = -1;

        public double VnavTargetAltitude { get; internal set; } = -1;

        public ConstraintType SpeedConstraintType { get; set; } = ConstraintType.FREE;

        public double SpeedConstraint { get; set; }

        public override string ToString()
        {
            return _point.PointName;
        }
    }
}
