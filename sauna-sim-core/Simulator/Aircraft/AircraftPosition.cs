using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.GeoTools.GribTools;
using AviationCalcUtilNet.GeoTools.MagneticTools;
using AviationCalcUtilNet.MathTools;
using System;
using System.Collections.Generic;
using SaunaSim.Core.Data;
using FsdConnectorNet;

namespace SaunaSim.Core.Simulator.Aircraft
{
    public class AircraftPosition
    {
        private double _altInd;
        private double _altPres;
        private double _altDens;
        private double _altAbs;
        private double _magneticHdg;
        private double _trueHdg;
        private double _trueTrack;
        private double _altSetting_hPa = AtmosUtil.ISA_STD_PRES_hPa;
        private double _sfcPress_hPa = AtmosUtil.ISA_STD_PRES_hPa;
        private double _ias;
        private double _tas;
        private double _gs;
        private double _mach;
        private GribDataPoint _gribPoint;

        // Position
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public double IndicatedAltitude
        {
            get => _altInd;
            set
            {
                _altInd = value;
                _altPres = AtmosUtil.ConvertIndicatedToPressureAlt(_altInd, _altSetting_hPa);
                _altAbs = AtmosUtil.ConvertIndicatedToAbsoluteAlt(_altInd, _altSetting_hPa, SurfacePressure_hPa);
                if (_gribPoint != null)
                {
                    double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Temp_K);
                    double p = AtmosUtil.CalculatePressureAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Level_hPa * 100, T);
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));
                } else
                {
                    double T = MathUtil.ConvertCelsiusToKelvin(AtmosUtil.CalculateIsaTemp(_altPres));
                    double p = AtmosUtil.ISA_STD_PRES_Pa;
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));
                }
            }
        }

        public double AbsoluteAltitude
        {
            get => _altAbs;
            set
            {
                _altAbs = value;
                _altInd = AtmosUtil.ConvertAbsoluteToIndicatedAlt(_altAbs, _altSetting_hPa, _sfcPress_hPa);
                _altPres = AtmosUtil.ConvertIndicatedToPressureAlt(_altInd, _altSetting_hPa);
                if (_gribPoint != null)
                {
                    double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Temp_K);
                    double p = AtmosUtil.CalculatePressureAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Level_hPa * 100, T);
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));
                }
                else
                {
                    double T = MathUtil.ConvertCelsiusToKelvin(AtmosUtil.CalculateIsaTemp(_altPres));
                    double p = AtmosUtil.ISA_STD_PRES_Pa;
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));
                }
            }
        }

        public double PressureAltitude => _altPres;
        public double DensityAltitude => _altDens;

        // Rotation
        public double Heading_Mag
        {
            get => _magneticHdg;

            set
            {
                _magneticHdg = value;

                // Calculate True Heading
                _trueHdg = MagneticUtil.ConvertMagneticToTrueTile(_magneticHdg, PositionGeoPoint);

                // Calculate True Track
                double wca = TrueAirSpeed == 0 ? 0 : Math.Acos(WindXComp / TrueAirSpeed);
                _trueTrack = GeoUtil.NormalizeHeading(_trueHdg + wca);
            }
        }

        public double Heading_True
        {
            get => _trueHdg;

            set
            {
                _trueHdg = value;

                // Set Magnetic Heading
                _magneticHdg = MagneticUtil.ConvertTrueToMagneticTile(_trueHdg, PositionGeoPoint);

                // Calculate True Track
                double wca = TrueAirSpeed == 0 ? 0 : Math.Acos(WindXComp / TrueAirSpeed);
                _trueTrack = GeoUtil.NormalizeHeading(_trueHdg + wca);
            }
        }

        public double Track_True
        {
            get => _trueTrack;

            set
            {
                _trueTrack = value;

                // Calculate True Heading
                double wca = TrueAirSpeed == 0 ? 0 : Math.Acos(WindXComp / TrueAirSpeed);
                _trueHdg = GeoUtil.NormalizeHeading(_trueTrack - wca);

                // Set Magnetic Heading
                _magneticHdg = MagneticUtil.ConvertTrueToMagneticTile(_trueHdg, PositionGeoPoint);
            }
        }

        public double Bank { get; set; }
        public double Pitch { get; set; }

        // Linear Velocities
        public double IndicatedAirSpeed
        {
            get => _ias;
            set
            {
                _ias = value;

                if (_gribPoint != null)
                {
                    _tas = AtmosUtil.ConvertIasToTas(_ias, _gribPoint.Level_hPa, _altAbs, _gribPoint.GeoPotentialHeight_Ft, _gribPoint.Temp_K, out _mach);
                }
                else
                {
                    _tas = AtmosUtil.ConvertIasToTas(_ias, AtmosUtil.ISA_STD_PRES_hPa, _altAbs, 0, AtmosUtil.ISA_STD_TEMP_K, out _mach);
                }
                _gs = _tas == 0 ? 0 : (_tas - WindHComp);
            }
        }

        public double TrueAirSpeed => _tas;

        public double GroundSpeed
        {
            get => _gs;
            set
            {
                _gs = value;
                _tas = _gs + WindHComp;
                if (_gribPoint != null)
                {
                    _ias = AtmosUtil.ConvertTasToIas(_tas, _gribPoint.Level_hPa, _altAbs, _gribPoint.GeoPotentialHeight_Ft, _gribPoint.Temp_K, out _mach);
                }
                else
                {
                    _ias = AtmosUtil.ConvertIasToTas(_tas, AtmosUtil.ISA_STD_PRES_hPa, _altAbs, 0, AtmosUtil.ISA_STD_TEMP_K, out _mach);
                }
            }
        }

        public double MachNumber
        {
            get => _mach;
            set
            {
                _mach = value;
                if (_gribPoint != null)
                {
                    double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Temp_K);
                    _tas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_mach, T));
                    _ias = AtmosUtil.ConvertTasToIas(_tas, _gribPoint.Level_hPa, _altAbs, _gribPoint.GeoPotentialHeight_Ft, _gribPoint.Temp_K, out _);
                }
                else
                {
                    double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), 0, AtmosUtil.ISA_STD_TEMP_K);
                    _tas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_mach, T));
                    _ias = AtmosUtil.ConvertTasToIas(_tas, AtmosUtil.ISA_STD_PRES_hPa, _altAbs, 0, AtmosUtil.ISA_STD_TEMP_K, out _);
                }
                _gs = _tas == 0 ? 0 : (_tas - WindHComp);
            }
        }

        public double VerticalSpeed { get; set; }
        public double Velocity_X_MPerS => MathUtil.ConvertKtsToMpers(GroundSpeed) * Math.Sin(MathUtil.ConvertDegreesToRadians(Track_True));
        public double Velocity_Y_MPerS => MathUtil.ConvertFeetToMeters(VerticalSpeed) / 60;
         public double Velocity_Z_MPerS => MathUtil.ConvertKtsToMpers(GroundSpeed) * Math.Cos(MathUtil.ConvertDegreesToRadians(Track_True));
        public double WindDirection { get; private set; }
        public double WindSpeed { get; private set; }

        // Rotational Velocities
        public double Heading_Velocity_RadPerS => 0;
        public double Bank_Velocity_RadPerS => 0;
        public double Pitch_Velocity_RadPerS => 0;
        
        // Atmospheric Data        
        public double AltimeterSetting_hPa
        {
            get => _altSetting_hPa;
            set
            {
                _altSetting_hPa = value;

                // Backwards compute new Indicated Alt
                _altInd = AtmosUtil.ConvertAbsoluteToIndicatedAlt(_altAbs, _altSetting_hPa, _sfcPress_hPa);
                _altPres = AtmosUtil.ConvertIndicatedToPressureAlt(_altInd, _altSetting_hPa);
            }
        }
        public double SurfacePressure_hPa
        {
            get => _sfcPress_hPa;
            set
            {
                _sfcPress_hPa = value;

                // Backwards compute new Indicated Alt
                _altInd = AtmosUtil.ConvertAbsoluteToIndicatedAlt(_altAbs, _altSetting_hPa, _sfcPress_hPa);
                _altPres = AtmosUtil.ConvertIndicatedToPressureAlt(_altInd, _altSetting_hPa);
            }
        }
        public int PresAltDiff => (int)((AtmosUtil.ISA_STD_PRES_hPa - (SurfacePressure_hPa == 0 ? AtmosUtil.ISA_STD_PRES_hPa : SurfacePressure_hPa)) * AtmosUtil.ISA_STD_PRES_DROP_ft_PER_hPa);
                

        public double WindXComp => WindSpeed * Math.Sin((Heading_True - WindDirection) * Math.PI / 180.0);

        public double WindHComp => GeoUtil.HeadwindComponent(WindSpeed, WindDirection, Heading_True);
        
        public GribDataPoint GribPoint
        {
            get => _gribPoint;
            set
            {
                if (value == null)
                {
                    _gribPoint = value;

                    WindDirection = 0;
                    WindSpeed = 0;
                    SurfacePressure_hPa = AtmosUtil.ISA_STD_PRES_hPa;

                    // Calculate True Track
                    double wca = TrueAirSpeed == 0 ? 0 : Math.Acos(WindXComp / TrueAirSpeed);
                    _trueTrack = GeoUtil.NormalizeHeading(_trueHdg + wca);

                    // Calculate TAS
                    _tas = AtmosUtil.ConvertIasToTas(_ias, AtmosUtil.ISA_STD_PRES_hPa, _altAbs, 0, AtmosUtil.ISA_STD_TEMP_K, out _mach);

                    // Density Alt
                    double T = MathUtil.ConvertCelsiusToKelvin(AtmosUtil.CalculateIsaTemp(_altPres));
                    double p = AtmosUtil.ISA_STD_PRES_Pa;
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));
                }
                else if (_gribPoint != value)
                {
                    _gribPoint = value;

                    if (WindDirection != _gribPoint.WDir_deg || WindSpeed != _gribPoint.WSpeed_kts)
                    {
                        WindDirection = _gribPoint.WDir_deg;
                        WindSpeed = _gribPoint.WSpeed_kts;

                        // Calculate True Track
                        double wca = TrueAirSpeed == 0 ? 0 : Math.Acos(WindXComp / TrueAirSpeed);
                        _trueTrack = GeoUtil.NormalizeHeading(_trueHdg + wca);
                    }
                    if (_gribPoint.SfcPress_hPa != 0)
                    {
                        SurfacePressure_hPa = _gribPoint.SfcPress_hPa;
                    }
                    else
                    {
                        SurfacePressure_hPa = AtmosUtil.ISA_STD_PRES_hPa;
                    }

                    // Density Alt
                    double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Temp_K);
                    double p = AtmosUtil.CalculatePressureAtAlt(MathUtil.ConvertFeetToMeters(_altAbs), _gribPoint.GeoPotentialHeight_M, _gribPoint.Level_hPa * 100, T);
                    _altDens = MathUtil.ConvertMetersToFeet(AtmosUtil.CalculateDensityAltitude(p, T));

                    // Calculate TAS
                    _tas = AtmosUtil.ConvertIasToTas(_ias, _gribPoint.Level_hPa, _altAbs, _gribPoint.GeoPotentialHeight_Ft, _gribPoint.Temp_K, out _mach);
                }
                _gs = _tas == 0 ? 0 : (_tas - WindHComp);
            }
        }

        public GeoPoint PositionGeoPoint => new GeoPoint(Latitude, Longitude, AbsoluteAltitude);

        public void UpdateGribPoint()
        {
            GribTile tile = GribTile.FindOrCreateGribTile(PositionGeoPoint, DateTime.UtcNow);

            if (tile == null)
            {
                GribPoint = null;
            }

            GribPoint = tile.GetClosestPoint(PositionGeoPoint);
        }
        
    }
}
