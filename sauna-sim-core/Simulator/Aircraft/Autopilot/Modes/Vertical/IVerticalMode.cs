using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Vertical
{
    public enum VerticalModeType
    {
        FLCH,
        VS,
        FPA,
        VNAV,
        TOGA,
        LDG,
        TAXI,
        APCH
    }

    public interface IVerticalMode : IAutopilotMode
    {
        VerticalModeType Type { get; }
    }
}
