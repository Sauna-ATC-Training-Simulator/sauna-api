using SaunaSim.Api.ApiObjects.Aircraft;
using SaunaSim.Core;
using SaunaSim.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaunaSim.Core.Simulator.Aircraft;
using System.Runtime.CompilerServices;
using SaunaSim.Api.Utilities;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using SaunaSim.Core.Data.Loaders;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;
using System.Threading;
using SaunaSim.Api.WebSockets;
using SaunaSim.Api.Services;
using AviationCalcUtilNet.Geo;

namespace SaunaSim.Api.Controllers
{
    [ApiController]
    [Route("api/aircraft")]
    public class AircraftController : ControllerBase
    {

        private readonly ILogger<DataController> _logger;
        private readonly ISimAircraftService _aircraftService;

        public AircraftController(ILogger<DataController> logger, ISimAircraftService aircraftService)
        {
            _logger = logger;
            _aircraftService = aircraftService;
        }

        [HttpPost("create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftResponse> CreateAircraft(CreateAircraftRequest request)
        {
            try
            {
                AircraftBuilder builder = new AircraftBuilder(
                    request.Callsign,
                    request.Cid,
                    request.Password,
                    request.Server,
                    request.Port,
                    _aircraftService.Handler.MagTileManager,
                    _aircraftService.Handler.GribTileManager,
                    _aircraftService.CommandHandler)
                {
                    FullName = request.FullName,
                    Protocol = request.Protocol,
                    Position = new AviationCalcUtilNet.GeoTools.GeoPoint(request.Position.Latitude,
                    request.Position.Longitude,
                    request.Position.IndicatedAltitude),
                    HeadingMag = Bearing.FromDegrees(request.Position.MagneticHeading),
                    LogInfo = (string msg) =>
                    {
                        _logger.LogInformation($"{request.Callsign}: {msg}");
                    },
                    LogWarn = (string msg) =>
                    {
                        _logger.LogWarning($"{request.Callsign}: {msg}");
                    },
                    LogError = (string msg) =>
                    {
                        _logger.LogError($"{request.Callsign}: {msg}");
                    },
                    XpdrMode = request.TransponderMode,
                    Squawk = request.Squawk,
                    Speed = request.Position.IndicatedSpeed,
                    IsSpeedMach = request.Position.IsMachNumber
                };

                // TODO: VERY IMPORTANT this parameter is no longer needed!!!!! or used!!!!!!
                if (request.FmsWaypointList != null)
                {
                    //builder.FmsWaypoints = request.FmsWaypointList;
                }

                var pilot = builder.Create(PrivateInfoLoader.GetClientInfo((string msg) => { _logger.LogWarning($"{request.Callsign}: {msg}"); }));
                _aircraftService.Handler.AddAircraft(pilot);
                pilot.Start();

                return Ok(new AircraftResponse(pilot, true));
            } catch (Exception e)
            {
                return BadRequest(e);
            }
        }

        [HttpGet("getAll")]
        public List<AircraftResponse> GetAllAircraft()
        {
            List<AircraftResponse> pilots = new List<AircraftResponse>();
            _aircraftService.AircraftListLock.WaitOne();

                foreach (var pilot in _aircraftService.AircraftList)
                {
                    pilots.Add(new AircraftResponse(pilot));
                }

            _aircraftService.AircraftListLock.ReleaseMutex();
            return pilots;
        }

        [HttpGet("getAllWithFms")]
        public List<AircraftResponse> GetAllAircraftWithFms()
        {
            List<AircraftResponse> pilots = new List<AircraftResponse>();
            _aircraftService.AircraftListLock.WaitOne();

                foreach (var pilot in _aircraftService.AircraftList)
                {
                    pilots.Add(new AircraftResponse(pilot, true));
                }
            _aircraftService.AircraftListLock.ReleaseMutex();
            return pilots;
        }

        [HttpGet("getByCallsign/{callsign}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftResponse> GetAircraftByCallsign(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            return Ok(new AircraftResponse(client, true));
        }

        [HttpGet("websocketByCallsign/{callsign}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task WebSocketForCallsign(string callsign)
        {
            if (_aircraftService.Handler.GetAircraftByCallsign(callsign) == null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            } else if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                try
                {
                    using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                    await _aircraftService.WebSocketHandler.HandleAircraftSocket(callsign, webSocket);
                } catch (Exception e)
                {
                    _logger.LogWarning($"Websocket connection failed: {e.Message}");
                }
            } else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpGet("getByPartialCallsign/{callsign}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftResponse> GetAircraftByPartialCallsign(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftWhichContainsCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            return Ok(new AircraftResponse(client, true));
        }

        [HttpPost("all/unpause")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<AircraftStateRequestResponse> UnpauseAll()
        {
            _aircraftService.Handler.AllPaused = false;

            return Ok(new AircraftStateRequestResponse
            {
                Paused = _aircraftService.Handler.AllPaused,
                SimRate = _aircraftService.Handler.SimRate
            });
        }

        [HttpPost("all/pause")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<AircraftStateRequestResponse> PauseAll()
        {
            _aircraftService.Handler.AllPaused = true;

            return Ok(new AircraftStateRequestResponse
            {
                Paused = _aircraftService.Handler.AllPaused,
                SimRate = _aircraftService.Handler.SimRate
            });
        }

        [HttpPost("all/simrate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<AircraftStateRequestResponse> SetAllSimRate(AircraftStateRequestResponse request)
        {
            _aircraftService.Handler.SimRate = request.SimRate;

            return Ok(new AircraftStateRequestResponse
            {
                Paused = _aircraftService.Handler.AllPaused,
                SimRate = _aircraftService.Handler.SimRate
            });
        }

        [HttpGet("all/simState")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<AircraftStateRequestResponse> GetAllSimState()
        {
            return Ok(new AircraftStateRequestResponse
            {
                Paused = _aircraftService.Handler.AllPaused,
                SimRate = _aircraftService.Handler.SimRate
            });
        }

        [HttpPost("byCallsign/{callsign}/unpause")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftStateRequestResponse> UnpauseAircraft(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Paused = false;

            return Ok(new AircraftStateRequestResponse
            {
                Paused = client.Paused,
                SimRate = client.SimRate / 10.0
            });
        }

        [HttpPost("byCallsign/{callsign}/pause")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftStateRequestResponse> PauseAircraft(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Paused = true;

            return Ok(new AircraftStateRequestResponse
            {
                Paused = client.Paused,
                SimRate = client.SimRate / 10.0
            });
        }

        [HttpPost("byCallsign/{callsign}/simrate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftStateRequestResponse> SetAircraftSimRate(string callsign, AircraftStateRequestResponse request)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.SimRate = (int)(request.SimRate * 10);

            return Ok(new AircraftStateRequestResponse
            {
                Paused = client.Paused,
                SimRate = client.SimRate / 10.0
            });
        }

        [HttpGet("byCallsign/{callsign}/simState")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftStateRequestResponse> GetAircraftSimState(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            return Ok(new AircraftStateRequestResponse
            {
                Paused = _aircraftService.Handler.AllPaused,
                SimRate = _aircraftService.Handler.SimRate
            });
        }

        [HttpDelete("removeByCallsign/{callsign}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftResponse> RemoveAircraftByCallsign(string callsign)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            _aircraftService.Handler.RemoveAircraftByCallsign(callsign);

            return Ok(new AircraftResponse(client, true));
        }

        [HttpDelete("all/remove")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult RemoveAll()
        {
            _aircraftService.Handler.DeleteAllAircraft();
            return Ok();
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSpeedMode/{speedMode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetSpeedMode(string callsign, McpSpeedSelectorType speedMode)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedSpeedMode = speedMode;

            return Ok(client.Autopilot);
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSpeedUnits/{speedUnits}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetSpeedUnits(string callsign, McpSpeedUnitsType speedUnits)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedSpeedUnits = speedUnits;

            return Ok(client.Autopilot);
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSelSpeed/{selSpeed}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetSpeedBug(string callsign, int selSpeed)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedSpeed = selSpeed;

            return Ok(client.Autopilot);
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSelHdg/{selHdg}/{turnDir}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetHdgBug(string callsign, int selHdg, McpKnobDirection turnDir)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedHeading = selHdg;
            client.Autopilot.HdgKnobTurnDirection = turnDir;

            return Ok(client.Autopilot);
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSelAlt/{selAlt}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetAltBug(string callsign, int selAlt)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedAltitude = selAlt;

            return Ok(client.Autopilot);
        }

        [HttpPost("mcp/byCallsign/{callsign}/setSelFpa/{selFpa}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AircraftAutopilot> McpSetFpaBug(string callsign, int selFpa)
        {
            SimAircraft client = _aircraftService.Handler.GetAircraftByCallsign(callsign);

            if (client == null)
            {
                return BadRequest("The aircraft was not found!");
            }

            client.Autopilot.SelectedFpa = selFpa;

            return Ok(client.Autopilot);
        }
    }
}
