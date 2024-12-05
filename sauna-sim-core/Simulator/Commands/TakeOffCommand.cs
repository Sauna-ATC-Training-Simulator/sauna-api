using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.Magnetic;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fixes;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using SaunaSim.Core.Simulator.Aircraft.Ground;

namespace SaunaSim.Core.Simulator.Commands
{
    public class TakeOffCommand : IAircraftCommand
    {
        public SimAircraft Aircraft { get; set; }
        public Action<string> Logger { get; set; }

        private Localizer _loc;
        private IRoutePoint _locRoutePoint;
        private MagneticTileManager _magTileMgr;
        private IAircraftCommand _targetWaypoint;
        
        private Fix waypoint;

        public TakeOffCommand(MagneticTileManager magTileMgr)
        {
            _magTileMgr = magTileMgr;
        }
        
        public void ExecuteCommand()
        {
            if (Aircraft.Autopilot.CurrentLateralMode == LateralModeType.LNAV)
            {
                Aircraft.Autopilot.SelectedHeading = (int)Aircraft.Position.Heading_Mag;
            }

            // Add the LOC/TO leg
            _locRoutePoint = new RouteWaypoint("RWY" + _loc.Runway_identifier, _loc.Loc_location);
            FmsPoint locFmsPoint = new FmsPoint(_locRoutePoint, RoutePointTypeEnum.FLY_OVER);
            FixToManualLeg toLeg = new FixToManualLeg(locFmsPoint, BearingTypeEnum.MAGNETIC, Bearing.FromDegrees((double)_loc.Loc_bearing), _magTileMgr);

            Aircraft.Fms.InsertAtIndex(toLeg, 0);

            // Activate leg now, skipping all previous legs
            while ((Aircraft.Fms.ActiveLeg == null || !Aircraft.Fms.ActiveLeg.Equals(toLeg)) && Aircraft.Fms.GetRouteLegs().Count > 0)
            {
                Aircraft.Fms.ActivateNextLeg();
            }

            Aircraft.GroundHandler.GroundPhase = GroundPhaseType.TAKEOFF;

            if(waypoint != null)
            {
                RouteWaypoint rwp = new RouteWaypoint(waypoint);

                Aircraft.Fms.ActivateDirectTo(rwp);

                Aircraft.Autopilot.AddArmedLateralMode(LateralModeType.LNAV);
            }
        }

        public bool HandleCommand(ref List<string> args)
        {
            // Check argument length
            if (args.Count < 1)
            {
                Logger?.Invoke($"ERROR: Takeoff requires at least 1 arguments!");
                return false;
            }

            // Get runway string
            string rwyStr = args[0];
            
            args.RemoveAt(0);

            // Find Waypoint
            Localizer wp = DataHandler.GetLocalizer(DataHandler.FAKE_AIRPORT_NAME, rwyStr);
            
            if (wp == null)
            {
                Logger?.Invoke($"ERROR: Runway {rwyStr} not found!");
                return false;
            }

            _loc = wp;

            if (args.Count >= 1) 
            {
                string waypointStr = args[0];
                waypoint = DataHandler.GetClosestWaypointByIdentifier(waypointStr, Aircraft.Position.PositionGeoPoint);
                
                if(waypoint == null)
                {
                    Logger?.Invoke($"ERROR: Waypoint {waypointStr} not found!");
                    return false;
                }                
                Logger?.Invoke($"{Aircraft.Callsign} Taking off Runway {rwyStr} with route to {waypointStr}.");
            }
            else
            {
                Logger?.Invoke($"{Aircraft.Callsign} Taking off Runway {rwyStr}");
            }            

            return true;
        }
    }
}
