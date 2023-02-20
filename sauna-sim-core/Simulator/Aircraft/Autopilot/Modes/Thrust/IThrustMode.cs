using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Thrust
{
    public enum ThrustModeType
    {
        SPEED,
        IDLE,
        THRUST,
        TAXI,
        TO,
        LDG,
        GA
    }
    public interface IThrustMode : IAutopilotMode
    {
        ThrustModeType Type { get; }
    }
}
