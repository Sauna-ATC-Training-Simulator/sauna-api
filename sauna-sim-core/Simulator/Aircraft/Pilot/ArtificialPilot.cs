using AviationCalcUtilNet.Units;
using SaunaSim.Core.Simulator.Aircraft.FMS;
using SaunaSim.Core.Simulator.Aircraft.Performance;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Pilot
{
    public class ArtificialPilot
    {
        private SimAircraft _parentAircraft;

        private bool _landingLights;
        public bool LandingLights
        {
            get
            {
                return _landingLights;
            }
            set
            {
                if(value != _landingLights)
                {
                    _parentAircraft.Connection.SetLandingLights(value);
                }
                _landingLights = value;
            }
        }

        private bool _taxiLights;
        public bool TaxiLights
        {
            get
            {
                return _taxiLights;
            }
            set
            {
                if (value != _taxiLights)
                {
                    _parentAircraft.Connection.SetTaxiLights(value);
                }
                _taxiLights = value;
            }
        }

        private bool _strobeLights;
        public bool StrobeLights
        {
            get
            {
                return _strobeLights;
            }
            set
            {
                if (value != _strobeLights)
                {
                    _parentAircraft.Connection.SetStrobeLight(value);
                }
                _strobeLights = value;
            }
        }

        private bool _logoLights;
        public bool LogoLights
        {
            get
            {
                return _logoLights;
            }
            set
            {
                if (value != _logoLights)
                {
                    _parentAircraft.Connection.SetLogoLight(value);
                }
                _logoLights = value;
            }
        }


        public ArtificialPilot(SimAircraft parentAircraft)
        {
            _parentAircraft = parentAircraft;
        }

        public void OnPositionUpdate(int intervalMs)
        {
            SetConfig();            
        }

        public void AircraftLights()
        {
            if(_parentAircraft.FlightPhase == FlightPhaseType.ON_GROUND)
            {
                if (_parentAircraft.GroundHandler.TakeoffPhase == Ground.TakeoffPhaseType.LINEUP)
                {
                    LandingLights = true;
                    StrobeLights = true;
                    TaxiLights = true;
                    LogoLights = true;
                }
                else if(_parentAircraft.GroundHandler.TakeoffPhase == Ground.TakeoffPhaseType.CLIMB)
                {
                    TaxiLights = false;
                    LogoLights = false;
                }
            }
            else
            {
                StrobeLights = true;
                if(_parentAircraft.Position.IndicatedAltitude < Length.FromFeet(10000))
                {
                    LandingLights = true;
                    LogoLights = true;
                    TaxiLights = true;
                }
                else
                {
                    LandingLights = false;
                    LogoLights = false;
                    TaxiLights = false;
                }
                
            }
        }

        private void SetConfig()
        {
            List<PerfConfigSetting> config = _parentAircraft.PerformanceData.ConfigList;
            int ias = _parentAircraft.Fms.FmsSpeedValue;

            if(_parentAircraft.Fms.PhaseType == FmsPhaseType.GO_AROUND)
            {
                for (int i = 0; i < config.Count; i++)
                {
                    if (!config[i].GearDown)
                    {
                        _parentAircraft.Data.Config = i;
                        Console.WriteLine($"Selected Config: {i}");
                        return;
                    }
                }
            }
            else
            {
                for (int i = 0; i < config.Count; i++)
                {
                    if (ias >= config[i].MinKias && ias < config[i].MaxKias)
                    {
                        _parentAircraft.Data.Config = i;
                        Console.WriteLine($"Selected Config: {i}");
                        return;
                    }
                }
            }
            //if (_parentAircraft.Position.IndicatedAirSpeed < )

            //if (_parentAircraft.Fms.PhaseType == FmsPhaseType.CLIMB && _parentAircraft.Fms.FmsSpeedValue >= 210)
            //{
            //    _parentAircraft.Data.Config = 0;
            //}
            //else if (_parentAircraft.Fms.PhaseType == FmsPhaseType.APPROACH && _parentAircraft.Fms.FmsSpeedValue <= 135)
            //{
            //    _parentAircraft.Data.Config = 4;
            //}
            //else if (_parentAircraft.Fms.PhaseType == FmsPhaseType.APPROACH && _parentAircraft.Fms.FmsSpeedValue <= 160)
            //{
            //    _parentAircraft.Data.Config = 3;
            //}
            //else if (_parentAircraft.Fms.PhaseType == FmsPhaseType.APPROACH && _parentAircraft.Fms.FmsSpeedValue <= 180)
            //{
            //    _parentAircraft.Data.Config = 2;
            //}
            //else if (_parentAircraft.Fms.PhaseType == FmsPhaseType.APPROACH && _parentAircraft.Fms.FmsSpeedValue <= 210)
            //{
            //    _parentAircraft.Data.Config = 0;
            //}
            //else if (_parentAircraft.Fms.PhaseType == FmsPhaseType.GO_AROUND && _parentAircraft.Fms.FmsSpeedValue >= 135)
            //{
            //    _parentAircraft.Data.Config = 2;
            //}
        }
    }
}
