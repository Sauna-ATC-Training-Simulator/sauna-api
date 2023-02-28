﻿using FsdConnectorNet;
using FsdConnectorNet.Args;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft.Control;
using SaunaSim.Core.Simulator.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.MathTools;
using SaunaSim.Core.Simulator.Aircraft.Autopilot;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.FMS;
using SaunaSim.Core.Simulator.Aircraft.Performance;


namespace SaunaSim.Core.Simulator.Aircraft
{
    public enum ConstraintType
    {
        FREE = -2,
        LESS = -1,
        EXACT = 0,
        MORE = 1
    }


    public enum ConnectionStatusType
    {
        WAITING,
        DISCONNECTED,
        CONNECTING,
        CONNECTED
    }

    public enum FlightPhaseType
    {
        AT_GATE,
        PUSH_BACK,
        TAXI_OUT,
        TAKE_OFF,
        DEPARTURE,
        ENROUTE,
        ARRIVAL,
        APPROACH,
        LANDING,
        GO_AROUND,
        TAXI_IN
    }

    public class SimAircraft : IDisposable
    {
        private Thread _posUpdThread;
        private PauseableTimer _delayTimer;
        private bool _paused;
        private string _flightPlan;
        private AircraftPosition _position;
        private bool disposedValue;
        private bool _shouldUpdatePosition = false;
        private ClientInfo _clientInfo;
        private LoginInfo _loginInfo;
        private ConnectionStatusType _connectionStatus = ConnectionStatusType.WAITING;
        private Connection _connection;
        private int _config;
        private double _thrustLeverPos;
        private double _thrustLeverVel;
        private double _speedBrakePos;
        private PerfData _performanceData;
        private double _massKg;
        private TransponderModeType _xpdrMode;
        private int _squawk;
        private int _delayMs;
        private AircraftConfig _aircraftConfig;
        private string _aircraftType;
        private string _airlineCode;
        private Action<string> _logInfo;
        private Action<string> _logWarn;
        private Action<string> _logError;
        private AircraftAutopilot _autopilot;
        private AircraftFms _fms;
        private FlightPhaseType _flightPhase;

        public SimAircraft(string callsign, string networkId, string password, string fullname, string hostname, ushort port, ProtocolRevision protocol, ClientInfo clientInfo,
            PerfData perfData, double lat, double lon, double alt, double hdg_mag, int delayMs = 0)
        {
            _loginInfo = new LoginInfo(networkId, password, callsign, fullname, PilotRatingType.Student, hostname, protocol, AppSettingsManager.CommandFrequency, port);
            _clientInfo = clientInfo;
            _connection = new Connection();
            _connection.Connected += OnConnectionEstablished;
            _connection.Disconnected += OnConnectionTerminated;
            _connection.FrequencyMessageReceived += OnFrequencyMessageReceived;
            _paused = true;
            _position = new AircraftPosition(lat, lon, alt)
            {
                Pitch = 2.5,
                Bank = 0,
                IndicatedAirSpeed = 250.0,
                Heading_Mag = hdg_mag
            };
            _thrustLeverPos = 0;
            _speedBrakePos = 0;
            _autopilot = new AircraftAutopilot(this)
            {
                SelectedAltitude = Convert.ToInt32(alt),
                SelectedHeading = Convert.ToInt32(hdg_mag),
                SelectedSpeed = Convert.ToInt32(250.0),
                CurrentLateralMode = LateralModeType.HDG,
                CurrentThrustMode = ThrustModeType.SPEED,
                CurrentVerticalMode = VerticalModeType.FLCH
            };
            _fms = new AircraftFms();
            _performanceData = perfData;
            _delayMs = delayMs;
            _aircraftConfig = new AircraftConfig(true, false, false, true, true, false, false, 0, false, false, new AircraftEngine(true, false), new AircraftEngine(true, false));
            _flightPlan = "";
            _aircraftType = "A320";
            _airlineCode = "FFT";

            // TODO: Change This To Actually Calculate Mass
            _massKg = (perfData.MTOW_kg + perfData.OEW_kg) / 2;
        }

        public void Start()
        {
            // Set initial assignments
            Position.UpdateGribPoint();

            // Connect if no delay, otherwise start timer
            if (DelayMs <= 0)
            {
                OnTimerElapsed(this, null);
            }
            else
            {
                _delayTimer = new PauseableTimer(DelayMs);
                _delayTimer.Elapsed += OnTimerElapsed;

                if (!_paused)
                {
                    _delayTimer.Start();
                }
            }
        }

        private void OnFrequencyMessageReceived(object sender, FrequencyMessageEventArgs e)
        {
            if (e.Frequency == AppSettingsManager.CommandFrequency && e.Message.StartsWith($"{Callsign}, "))
            {
                // Split message into args
                List<string> split = e.Message.Replace($"{Callsign}, ", "").Split(' ').ToList();

                // Loop through command list
                while (split.Count > 0)
                {
                    // Get command name
                    string command = split[0].ToLower();
                    split.RemoveAt(0);

                    split = CommandHandler.HandleCommand(command, this, split, (string msg) =>
                    {
                        string returnMsg = msg.Replace($"{Callsign} ", "");
                        Connection.SendFrequencyMessage(e.Frequency, returnMsg);
                    });
                }
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _delayMs = -1;
            _delayTimer?.Stop();

            // Connect to FSD Server
            _connection.Connect(_clientInfo, LoginInfo, GetFsdPilotPosition(), AircraftConfig, new PlaneInfo(AircraftType, AirlineCode));
            _connectionStatus = ConnectionStatusType.CONNECTING;
        }

        private void OnConnectionEstablished(object sender, EventArgs e)
        {
            _connectionStatus = ConnectionStatusType.CONNECTED;
            // Start Position Update Thread
            _shouldUpdatePosition = true;
            _posUpdThread = new Thread(new ThreadStart(AircraftPositionWorker));
            _posUpdThread.Name = $"{Callsign} Position Worker";
            _posUpdThread.Start();

            // Send Flight Plan
            // TODO: Send Flight Plan
        }

        private void OnConnectionTerminated(object sender, EventArgs e)
        {
            _connectionStatus = ConnectionStatusType.DISCONNECTED;
            _shouldUpdatePosition = false;
            _delayTimer?.Stop();
        }

        private void AircraftPositionWorker()
        {
            while (_shouldUpdatePosition)
            {
                // Calculate position
                if (!_paused)
                {
                    // Run Autopilot
                    _autopilot.OnPositionUpdate(AppSettingsManager.PosCalcRate);

                    // TODO: Update Mass
                    
                    // Move Aircraft
                    MoveAircraft(AppSettingsManager.PosCalcRate);
                    
                    // Update Grib Data
                    Position.UpdateGribPoint();
                    
                    // Update FSD
                    Connection.UpdatePosition(GetFsdPilotPosition());
                }

                Thread.Sleep(AppSettingsManager.PosCalcRate);
            }
        }

        private void MoveAircraft(int intervalMs)
        {
            double t = intervalMs / 1000.0;
            
            // Calculate Pitch, Bank, and Thrust Lever Position
            Position.Pitch += PerfDataHandler.CalculateDisplacement(Position.PitchRate, 0, t);
            Position.Bank += PerfDataHandler.CalculateDisplacement(Position.BankRate, 0, t);
            ThrustLeverPos += PerfDataHandler.CalculateDisplacement(ThrustLeverVel, 0, t);
            
            // Calculate Performance Values
            (double accelFwd, double vs) = PerfDataHandler.CalculatePerformance(PerformanceData, Position.Pitch, ThrustLeverPos / 100.0, Position.IndicatedAirSpeed,
                Position.DensityAltitude, Mass_kg, SpeedBrakePos, Config);
            
            // Calculate New Velocities
            double curGs = Position.GroundSpeed;
            Position.IndicatedAirSpeed = MathUtil.ConvertMpersToKts(PerfDataHandler.CalculateFinalVelocity(
                MathUtil.ConvertKtsToMpers(Position.IndicatedAirSpeed), MathUtil.ConvertKtsToMpers(accelFwd), t));
            Position.VerticalSpeed = vs;
            
            // Calculate Displacement
            double displacement = 0.5 * (MathUtil.ConvertKtsToMpers(Position.GroundSpeed + curGs)) * t;
            double distanceTravelledNMi = MathUtil.ConvertMetersToNauticalMiles(displacement);
            
            // Calculate Position
            if (Math.Abs(Position.Bank) < double.Epsilon)
            {
                GeoPoint point = new GeoPoint(Position.PositionGeoPoint);
                point.MoveByNMi(Position.Track_True, distanceTravelledNMi);
                Position.Latitude = point.Lat;
                Position.Longitude = point.Lon;
            }
            else
            {
                // Calculate radius of turn
                double radiusOfTurn = GeoUtil.CalculateRadiusOfTurn(Math.Abs(Position.Bank), Position.GroundSpeed);
                
                // Calculate degrees to turn
                double degreesToTurn = GeoUtil.CalculateDegreesTurned(distanceTravelledNMi, radiusOfTurn);
                
                // Figure out turn direction
                bool isRightTurn = Position.Bank > 0;
                
                // Calculate end heading
                double endHeading = GeoUtil.CalculateEndHeading(Position.Heading_Mag, degreesToTurn, isRightTurn);
                
                // Calculate chord line data
                Tuple<double, double> chordLine = GeoUtil.CalculateChordHeadingAndDistance(Position.Heading_Mag, degreesToTurn, radiusOfTurn, isRightTurn);
                
                // Calculate new position
                Position.Heading_Mag = chordLine.Item1;
                GeoPoint point = new GeoPoint(Position.PositionGeoPoint);
                point.MoveByNMi(Position.Track_True, distanceTravelledNMi);
                Position.Latitude = point.Lat;
                Position.Longitude = point.Lon;
                Position.Heading_Mag = endHeading;
            }
            
            // Calculate Altitude
            Position.IndicatedAltitude += Position.VerticalSpeed * t / 60;
        }

        public PilotPosition GetFsdPilotPosition()
        {
            return new PilotPosition(XpdrMode, (ushort)Squawk, Position.Latitude, Position.Longitude, Position.TrueAltitude, Position.TrueAltitude,
                Position.PressureAltitude, Position.GroundSpeed, Position.Pitch, Position.Bank, Position.Heading_True, Position.OnGround, Position.Velocity_X_MPerS, Position.Velocity_Y_MPerS,
                Position.Velocity_Z_MPerS, Position.Pitch_Velocity_RadPerS, Position.Heading_Velocity_RadPerS, Position.Bank_Velocity_RadPerS);
        }

        public LoginInfo LoginInfo => _loginInfo;
        public string Callsign => LoginInfo.callsign;
        public ConnectionStatusType ConnectionStatus => _connectionStatus;
        public Connection Connection => _connection;
        
        public AircraftPosition Position
        {
            get => _position;
            set => _position = value;
        }

        public int Config
        {
            get => _config;
            set => _config = value;
        }

        public double ThrustLeverPos
        {
            get => _thrustLeverPos;
            set => _thrustLeverPos = value;
        }

        public double SpeedBrakePos
        {
            get => _speedBrakePos;
            set => _speedBrakePos = value;
        }

        public PerfData PerformanceData
        {
            get => _performanceData;
            set => _performanceData = value;
        }

        public double Mass_kg
        {
            get => _massKg;
            set => _massKg = value;
        }

        public TransponderModeType XpdrMode
        {
            get => _xpdrMode;
            set => _xpdrMode = value;
        }

        public int Squawk
        {
            get => _squawk;
            set => _squawk = value;
        }

        public int DelayMs
        {
            get => _delayMs;
            set => _delayMs = value;
        }

        public AircraftConfig AircraftConfig
        {
            get => _aircraftConfig;
            set => _aircraftConfig = value;
        }

        public string AircraftType => _aircraftType;

        public string AirlineCode => _airlineCode;

        // Loggers
        public Action<string> LogInfo
        {
            get => _logInfo;
            set => _logInfo = value;
        }

        public Action<string> LogWarn
        {
            get => _logWarn;
            set => _logWarn = value;
        }

        public Action<string> LogError
        {
            get => _logError;
            set => _logError = value;
        }

        // TODO: Convert to FlightPlan Struct/Object
        public string FlightPlan
        {
            get => _flightPlan;
            set
            {
                _flightPlan = value;
                if (ConnectionStatus == ConnectionStatusType.CONNECTED)
                {
                    // TODO: Send Flight Plan
                }
            }
        }

        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                if (DelayMs > 0 && _delayTimer != null)
                {
                    if (!_paused)
                    {
                        _delayTimer.Start();
                    }
                    else
                    {
                        _delayTimer.Pause();
                    }
                }
            }
        }

        // Assigned values
        public AircraftAutopilot Autopilot => _autopilot;
        public AircraftFms Fms => _fms;

        public FlightPhaseType FlightPhase
        {
            get => _flightPhase;
            set => _flightPhase = value;
        }

        public double ThrustLeverVel
        {
            get => _thrustLeverVel;
            set => _thrustLeverVel = value;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _connection.Dispose();
                    _shouldUpdatePosition = false;
                    _posUpdThread?.Join();
                    _delayTimer?.Stop();
                    _delayTimer?.Dispose();
                }

                _connection = null;
                _posUpdThread = null;
                _position = null;
                _autopilot = null;
                _fms = null;
                _delayTimer = null;
                disposedValue = true;
            }
        }

        ~SimAircraft()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}