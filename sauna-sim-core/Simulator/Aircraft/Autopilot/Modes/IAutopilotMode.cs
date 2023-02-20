using SaunaSim.Core.Simulator.Aircraft.Control.FMS;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes
{
    /// <summary>
    /// Autopilot mode interface.
    /// Implementations will perform control inputs based on their respective function.
    /// </summary>
    public interface IAutopilotMode
    {
        /// <summary>
        /// Determines whether this mode should be activated.
        /// </summary>
        /// <param name="aircraft"><c>SimAircraft</c> The calling aircraft.</param>
        /// <param name="posCalcInterval"><c>int</c> Time (ms) before next position update.</param>
        /// <returns><c>bool</c> Whether or not the instruction should be activated</returns>
        bool ShouldActivateInstruction(SimAircraft aircraft, int posCalcInterval);

        /// <summary>
        /// Performs flight/thrust control actions on position update.
        /// </summary>
        /// <param name="aircraft"><c>SimAircraft</c> The calling aircraft.</param>
        /// <param name="posCalcInterval"><c>int</c> Time (ms) before next position update.</param>
        void OnPositionUpdate(ref SimAircraft aircraft, int posCalcInterval);
    }
}
