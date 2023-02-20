using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Thrust
{
    public class SpeedMode : IThrustMode
    {
        public ThrustModeType Type => ThrustModeType.SPEED;

        public void OnPositionUpdate(ref SimAircraft aircraft, int posCalcInterval)
        {
            throw new NotImplementedException();
        }

        public bool ShouldActivateInstruction(SimAircraft aircraft, int posCalcInterval)
        {
            return true;
        }
    }
}
