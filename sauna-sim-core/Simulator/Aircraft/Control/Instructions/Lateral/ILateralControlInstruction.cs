using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaunaSim.Core.Simulator.Aircraft
{
    public enum LateralControlMode
    {
        HEADING_HOLD,
        TRACK_HOLD,
        COURSE_INTERCEPT,
        NAV_ROUTE,
        HOLDING_PATTERN,
        APPROACH
    }

    public enum TurnDirection
    {
        LEFT = -1,
        RIGHT = 1,
        SHORTEST = 0
    }

    public interface ILateralControlInstruction : IControlInstruction
    {
        LateralControlMode Type { get; }
    }
}
