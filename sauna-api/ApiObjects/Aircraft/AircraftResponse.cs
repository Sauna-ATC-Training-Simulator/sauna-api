using SaunaSim.Core;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft;
using SaunaSim.Core.Simulator.Aircraft.Control;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using FsdConnectorNet;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;

namespace SaunaSim.Api.ApiObjects.Aircraft
{
    public class AircraftResponse
    {
        public string Callsign { get; set; }
        public int DelayMs { get; set; }
        public bool Paused { get; set; }
        public double ThrustLeverPos { get; set; }
        public FlightPlan? FlightPlan { get; set; }
        public TransponderModeType XpdrMode { get; set; }
        public int Squawk { get; set; }
        public AircraftPosition Position { get; set; }
        public AircraftFmsResponse Fms { get; set; }
        public AircraftAutopilot Autopilot { get; set; }
        public ConnectionStatusType ConnectionStatus { get; set; }

        public AircraftResponse()
        {

        }

        public AircraftResponse(SimAircraft pilot, bool includeFms = false)
        {
            Callsign = pilot.Callsign;
            DelayMs = pilot.DelayRemainingMs;
            Paused = pilot.Paused;
            FlightPlan = pilot.FlightPlan;
            XpdrMode = pilot.XpdrMode;
            Squawk = pilot.Squawk;
            Position = pilot.Position;
            if (includeFms)
            {
                Fms = new AircraftFmsResponse(pilot.Fms);
            }
            ConnectionStatus = pilot.ConnectionStatus;
            ThrustLeverPos = pilot.ThrustLeverPos;
            Autopilot = pilot.Autopilot;
        }
    }
}