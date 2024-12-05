using SaunaSim.Api.ApiObjects.Data;
using SaunaSim.Core;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft;
using SaunaSim.Core.Simulator.Commands;
using AviationCalcUtilNet.GeoTools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FsdConnectorNet;
using SaunaSim.Core.Data.Loaders;
using SaunaSim.Api.Utilities;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS;
using SaunaSim.Core.Simulator.Aircraft.FMS.Legs;
using NavData_Interface.Objects;
using System.Runtime.CompilerServices;
using System.Configuration;
using NavData_Interface.Objects.Fixes;
using SaunaSim.Api.Services;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.Units;

namespace SaunaSim.Api.Controllers
{
    [ApiController]
    [Route("api/data")]
    public class DataController : ControllerBase
    {

        private readonly ILogger<DataController> _logger;
        private readonly ISimAircraftService _aircraftService;

        public DataController(ILogger<DataController> logger, ISimAircraftService aircraftService)
        {
            _logger = logger;
            _aircraftService = aircraftService;
        }

        [HttpGet("settings")]
        public AppSettingsRequestResponse GetSettings()
        {
            return new AppSettingsRequestResponse(AppSettingsManager.Settings);
        }

        [HttpPost("settings")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<AppSettings> UpdateSettings(AppSettingsRequestResponse settings)
        {
            try
            {
                AppSettingsManager.Settings = settings.ToAppSettings();
            } catch (Exception ex)
            {
                if (ex is IndexOutOfRangeException || ex is FormatException || ex is OverflowException)
                {
                    return BadRequest("Command frequency was not in the correct format.");
                }
                throw;
            }


            return Ok(new AppSettingsRequestResponse(AppSettingsManager.Settings));
        }

        [HttpGet("navigraphApiCreds")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<NavigraphApiCreds> GetNavigraphApiCreds()
        {
            string error = "";
            var navigraphCreds = PrivateInfoLoader.GetNavigraphCreds((string s) =>
            {
                error = s;
            });

            if (navigraphCreds == null)
            {
                return BadRequest(error);
            }

            return navigraphCreds;
        }

        [HttpGet("hasNavigraphDataLoaded")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<NavigraphLoadedResponse> GetHasNavigraphDataLoaded()
        {
            return Ok(new NavigraphLoadedResponse() { Loaded = DataHandler.HasNavigraphDataLoaded(), Uuid = DataHandler.GetNavigraphFileUuid() });
        }

        [HttpGet("loadedSectorFiles")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<List<string>> GetLoadedSectorFiles()
        {
            return Ok(DataHandler.GetSectorFilesLoaded());
        }

        [HttpPost("loadSectorFile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult LoadSectorFile(LoadFileRequest request)
        {
            try
            {
                DataHandler.LoadSectorFile(request.FileName);
                return Ok();
            } catch (System.IO.FileNotFoundException)
            {
                return BadRequest("The file could not be found.");
            } catch (Exception)
            {
                return BadRequest("The file is not a valid Sector file.");
            }
        }

        [HttpPost("loadDFDNavData")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult LoadDFDNavData(LoadDfdFileRequest request)
        {
            try
            {
                DataHandler.LoadNavigraphDataFile(request.FileName, request.Uuid);
                return Ok();
            } catch (System.IO.FileNotFoundException)
            {
                return BadRequest("The file could not be found.");
            } catch (Exception)
            {
                return BadRequest("The file is not a vaid NavData file.");
            }
        }

        [HttpPost("loadEuroscopeScenario")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> LoadEuroscopeScenario(LoadScenarioFileRequest request)
        {
            try
            {
                string[] filelines = System.IO.File.ReadAllLines(request.FileName);

                List<AircraftBuilder> pilots = new List<AircraftBuilder>();

                AircraftBuilder lastPilot = null;

                double refLat = 190;
                double refLon = 190;

                foreach (string line in filelines)
                {
                    // Create pilot and update position
                    if (line.StartsWith("@N"))
                    {
                        string[] items = line.Split(':');
                        string callsign = items[1];
                        TransponderModeType xpdrMode;
                        switch (items[0].ToCharArray()[1])
                        {
                            case 'N':
                                xpdrMode = TransponderModeType.ModeC;
                                break;
                            case 'S':
                                xpdrMode = TransponderModeType.Standby;
                                break;
                            case 'Y':
                                xpdrMode = TransponderModeType.Ident;
                                break;
                            default:
                                xpdrMode = TransponderModeType.ModeC;
                                break;
                        }
                        // Load the coordinates. These could be in decimal or DMS format.
                        // TODO: If this fails, skip this aircraft. Right now, we set pos to 0,0!

                        Latitude lat = (Latitude)0;
                        Longitude lon = (Longitude)0;

                        // XPDR code
                        int squawk = 0;
                        try
                        {
                            squawk = Convert.ToInt32(items[2]);
                        } catch (Exception e)
                        {
                            if (e is not OverflowException && e is not FormatException)
                            {
                                throw;
                            }
                        }

                        try
                        {
                            (lat, lon) = CoordinateUtil.ParseCoordinate(items[4], items[5]);
                        } catch (FormatException)
                        {
                            Console.WriteLine($"ERROR loading aircraft {callsign}: Could not parse coordinates");
                        }

                        EuroScopeLoader.ReadVatsimPosFlag(Convert.ToInt32(items[8]), out double hdg, out double bank, out double pitch, out bool onGround);
                        //SimAircraft(string callsign, string networkId, string password,        string fullname, string hostname, ushort port, bool vatsim,   ProtocolRevision protocol,      double lat, double lon, double alt, double hdg_mag, int delayMs = 0)
                        lastPilot = new AircraftBuilder(callsign, request.Cid, request.Password, request.Server, request.Port, _aircraftService.Handler.MagTileManager, _aircraftService.Handler.GribTileManager, _aircraftService.CommandHandler)
                        {
                            Protocol = request.Protocol,
                            Position = new GeoPoint(lat, lon, Length.FromFeet(Convert.ToDouble(items[6]))),
                            HeadingMag = Bearing.FromDegrees(hdg),
                            LogInfo = (string msg) =>
                            {
                                _logger.LogInformation($"{callsign}: {msg}");
                            },
                            LogWarn = (string msg) =>
                            {
                                _logger.LogWarning($"{callsign}: {msg}");
                            },
                            LogError = (string msg) =>
                            {
                                _logger.LogError($"{callsign}: {msg}");
                            },
                            XpdrMode = xpdrMode,
                            Squawk = squawk
                        };

                        // Add to temp list
                        pilots.Add(lastPilot);
                    } else if (line.StartsWith("$FP"))
                    {
                        if (lastPilot != null)
                        {
                            lastPilot.EsFlightPlanStr = line;
                        }
                    } else if (line.StartsWith("REQALT"))
                    {
                        string[] items = line.Split(':');

                        if (lastPilot != null && items.Length >= 3)
                        {
                            try
                            {
                                int reqAlt = Convert.ToInt32(items[2]);
                                lastPilot.RequestedAlt = reqAlt;
                            } catch (Exception) { }
                        }
                    } else if (line.StartsWith("START"))
                    {
                        string[] items = line.Split(':');

                        if (lastPilot != null && items.Length >= 2)
                        {
                            try
                            {
                                int delay = Convert.ToInt32(items[1]) * 60000;
                                lastPilot.DelayMs = delay;
                            } catch (Exception) { }
                        }
                    } else if (line.StartsWith("ILS"))
                    {
                        string[] items = line.Split(':');
                        string wpId = items[0].Replace("ILS", "");

                        try
                        {
                            GeoPoint threshold = new GeoPoint(Convert.ToDouble(items[1]), Convert.ToDouble(items[2]));

                            if (refLat > 180 || refLon > 180)
                            {
                                refLat = threshold.Lat.Degrees;
                                refLon = threshold.Lon.Degrees;
                            }

                            Bearing course = Bearing.FromRadians(0);
                            if (items.Length == 4)
                            {
                                course = Bearing.FromDegrees(Convert.ToDouble(items[3]));
                            } else if (items.Length > 4)
                            {
                                GeoPoint otherThreshold = new GeoPoint(Convert.ToDouble(items[3]), Convert.ToDouble(items[4]));
                                course = _aircraftService.Handler.MagTileManager.TrueToMagnetic(threshold, DateTime.UtcNow, GeoPoint.InitialBearing(threshold, otherThreshold));
                            }

                            Length elev = (Length)0;
                            Airport airport = DataHandler.GetAirportByIdentifier(DataHandler.FAKE_AIRPORT_NAME);

                            if (airport != null)
                            {
                                elev = airport.Elevation;
                            }

                            Glideslope gs = new Glideslope(threshold, Angle.FromDegrees(3.0), elev);
                            Localizer loc = new Localizer("", "", DataHandler.FAKE_AIRPORT_NAME, wpId, wpId, threshold, 0, course, (Angle)0, IlsCategory.CATI, gs, 0);

                            DataHandler.AddLocalizer(loc);
                        } catch (Exception)
                        {
                            Console.WriteLine("Well that didn't work did it.");
                        }
                    } else if (line.StartsWith("HOLDING"))
                    {
                        string[] items = line.Split(':');

                        try
                        {
                            string wpId = items[1];
                            double inboundCourse = Convert.ToDouble(items[2]);
                            HoldTurnDirectionEnum turnDirection = (HoldTurnDirectionEnum)Convert.ToInt32(items[3]);
                            Fix fix = DataHandler.GetClosestWaypointByIdentifier(wpId, new GeoPoint(Latitude.FromDegrees(refLat), Longitude.FromDegrees(refLon)));
                            if (fix != null)
                            {
                                DataHandler.AddPublishedHold(new PublishedHold(fix, Bearing.FromDegrees(inboundCourse), turnDirection));
                            }
                        } catch (Exception)
                        {
                            Console.WriteLine("Error Loading Hold.");
                        }
                    } else if (line.StartsWith("AIRPORT_ALT"))
                    {
                        string[] items = line.Split(':');

                        try
                        {
                            double airportElev = Convert.ToDouble(items[1]);
                            Airport airport = new Airport(DataHandler.FAKE_AIRPORT_NAME, new GeoPoint(0, 0, 0), "", "", "", DataHandler.FAKE_AIRPORT_NAME, true, RunwaySurfaceCode.Hard, Length.FromFeet(airportElev), Length.FromFeet(18000), Length.FromFeet(18000), Velocity.FromKnots(250), Length.FromFeet(10000), "");
                            DataHandler.AddAirport(airport);
                        } catch (Exception)
                        {
                            Console.WriteLine("Error loading airport elevation");
                        }
                    }
                }

                List<Task> tasks = new();

                foreach (AircraftBuilder pilot in pilots)
                {
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        DateTime start = DateTime.UtcNow;
                        var aircraft = pilot.Create(PrivateInfoLoader.GetClientInfo((string msg) => { _logger.LogWarning($"{pilot.Callsign}: {msg}"); }));
                        _aircraftService.Handler.AddAircraft(aircraft);
                        aircraft.Start();
                        _logger.LogInformation($"{pilot.Callsign} created in {(DateTime.UtcNow - start).TotalMilliseconds}ms");
                    }));
                }

                await Task.WhenAll(tasks);
            } catch (Exception ex)
            {
                return BadRequest(ex.StackTrace);
            }
            return Ok();
        }

    }
}
