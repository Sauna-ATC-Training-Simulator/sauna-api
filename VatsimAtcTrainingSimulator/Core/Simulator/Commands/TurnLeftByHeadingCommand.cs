﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VatsimAtcTrainingSimulator.Core.GeoTools;
using VatsimAtcTrainingSimulator.Core.Simulator.AircraftControl;

namespace VatsimAtcTrainingSimulator.Core.Simulator.Commands
{
    public class TurnLeftByHeadingCommand : IAircraftCommand
    {
        public VatsimClientPilot Aircraft { get; set; }
        public Action<string> Logger { get; set; }

        private int hdg;

        public void ExecuteCommand()
        {
            Aircraft.Control.CurrentLateralInstruction = new HeadingHoldInstruction(hdg);
        }

        public bool HandleCommand(ref List<string> args)
        {
            // Check argument length
            if (args.Count < 1)
            {
                Logger?.Invoke($"ERROR: Turn Left By requires at least 1 argument!");
                return false;
            }

            // Get heading string
            string headingString = args[0];
            args.RemoveAt(0);

            try
            {
                // Parse heading
                int degTurn = Convert.ToInt32(headingString);
                hdg = (int)Math.Round(AcftGeoUtil.NormalizeHeading(Aircraft.Position.Heading_Mag - degTurn), MidpointRounding.AwayFromZero);

                Logger?.Invoke($"{Aircraft.Callsign} turning left heading {hdg:000} degrees.");
            }
            catch (Exception)
            {
                Logger?.Invoke($"ERROR: Heading {headingString} not valid!");
                return false;
            }

            return true;
        }
    }
}