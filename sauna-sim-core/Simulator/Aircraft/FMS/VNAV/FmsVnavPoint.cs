using System;
using AviationCalcUtilNet.Units;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;

namespace SaunaSim.Core.Simulator.Aircraft.FMS.VNAV
{
    public struct FmsVnavPoint
    {
        public Length AlongTrackDistance { get; set; }
        public Length Alt { get; set; }
        public int Speed { get; set; }
        public McpSpeedUnitsType SpeedUnits { get; set; }
        public int CmdSpeed { get; set; }
        public McpSpeedUnitsType CmdSpeedUnits { get; set; }
        public Angle Angle { get; set; }
    }
}

