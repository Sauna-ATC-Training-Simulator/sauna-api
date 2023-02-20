using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.GeoTools.GribTools;
using AviationCalcUtilNet.MathTools;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Lateral;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Thrust;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Modes.Vertical;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SaunaSim.Core.Simulator.Aircraft.Autopilot
{
    public class AircraftAutopilot
    {
        private SimAircraft _parentAircraft;

        // Modes
        private ILateralMode _curLatMode;
        private ILateralMode _armedLatMode;
        private IVerticalMode _curVertMode;
        private List<IVerticalMode> _armedVertModes;
        private IThrustMode _curThrustMode;
        private IThrustMode _armedThrustMode;

        // MCP
        private int _selHdg;
        private int _selAlt;
        private int _selVs;
        private double _selFpa;
        private McpSpeedSelectorType _spdMode;
        private McpSpeedUnitsType _spdUnits;
        private int _selMinSpd;
        private int _selMaxSpd;

        public AircraftAutopilot(SimAircraft parentAircraft)
        {
            _parentAircraft = parentAircraft;

            // Initialize Modes

            // Initialize MCP
            _selHdg = 0;
            _selAlt = 0;
            _selVs = 0;
            _selFpa = 0;
            _spdMode = McpSpeedSelectorType.FMS;
            _spdUnits = McpSpeedUnitsType.KNOTS;
            _selMinSpd = 0;
            _selMaxSpd = 0;
        }

        public int SelectedHeading {
            get => _selHdg;
            set {
                if (value < 0 || value >= 360)
                {
                    _selHdg = 0;
                } else
                {
                    _selHdg = value;
                }
            }
        }

        public int SelectedAltitude {
            get => _selAlt;
            set {
                if (value < 0)
                {
                    _selAlt = 0;
                } else if (value > 99999)
                {
                    _selAlt = 99999;
                } else
                {
                    _selAlt = value;
                }
            }
        }

        public int SelectedVerticalSpeed {
            get => _selVs;
            set => _selVs = value;
        }

        public double SelectedFpa {
            get => _selFpa;
            set => _selFpa = value;
        }

        public McpSpeedSelectorType SelectedSpeedMode {
            get => _spdMode;
            set => _spdMode = value;
        }

        public McpSpeedUnitsType SelectedSpeedUnits {
            get => _spdUnits;
            set {
                GribDataPoint gribPoint = _parentAircraft.Position.GribPoint;
                double absAlt = _parentAircraft.Position.AbsoluteAltitude;

                if (value == McpSpeedUnitsType.MACH)
                {
                    // Convert selected speeds from IAS to Mach
                    if (gribPoint != null)
                    {
                        AtmosUtil.ConvertIasToTas(_selMaxSpd, gribPoint.Level_hPa, absAlt, gribPoint.GeoPotentialHeight_Ft, gribPoint.Temp_K, out double maxMach);
                        _selMaxSpd = (int)(maxMach * 100);
                        AtmosUtil.ConvertIasToTas(_selMinSpd, gribPoint.Level_hPa, absAlt, gribPoint.GeoPotentialHeight_Ft, gribPoint.Temp_K, out double minMach);
                        _selMinSpd = (int)(minMach * 100);
                    } else
                    {
                        AtmosUtil.ConvertIasToTas(_selMaxSpd, AtmosUtil.ISA_STD_PRES_hPa, absAlt, 0, AtmosUtil.ISA_STD_TEMP_K, out double maxMach);
                        _selMaxSpd = (int)(maxMach * 100);
                        AtmosUtil.ConvertIasToTas(_selMinSpd, AtmosUtil.ISA_STD_PRES_hPa, absAlt, 0, AtmosUtil.ISA_STD_TEMP_K, out double minMach);
                        _selMinSpd = (int)(minMach * 100);
                    }
                } else
                {
                    // Convert selected speeds from Mach to IAS
                    if (gribPoint != null)
                    {
                        double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(absAlt), gribPoint.GeoPotentialHeight_M, gribPoint.Temp_K);
                        double selMaxTas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_selMaxSpd / 100.0, T));
                        _selMaxSpd = (int) AtmosUtil.ConvertTasToIas(selMaxTas, gribPoint.Level_hPa, absAlt, gribPoint.GeoPotentialHeight_Ft, gribPoint.Temp_K, out _);
                        double selMinTas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_selMinSpd / 100.0, T));
                        _selMinSpd = (int) AtmosUtil.ConvertTasToIas(selMinTas, gribPoint.Level_hPa, absAlt, gribPoint.GeoPotentialHeight_Ft, gribPoint.Temp_K, out _);
                    } else
                    {
                        double T = AtmosUtil.CalculateTempAtAlt(MathUtil.ConvertFeetToMeters(absAlt), 0, AtmosUtil.ISA_STD_TEMP_K);
                        double selMaxTas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_selMaxSpd / 100.0, T));
                        _selMaxSpd = (int) AtmosUtil.ConvertTasToIas(selMaxTas, AtmosUtil.ISA_STD_PRES_hPa, absAlt, 0, AtmosUtil.ISA_STD_TEMP_K, out _);
                        double selMinTas = MathUtil.ConvertMpersToKts(AtmosUtil.ConvertMachToTas(_selMinSpd / 100.0, T));
                        _selMinSpd = (int) AtmosUtil.ConvertTasToIas(selMinTas, AtmosUtil.ISA_STD_PRES_hPa, absAlt, 0, AtmosUtil.ISA_STD_TEMP_K, out _);
                    }
                }
                _spdUnits = value;
            }
        }

        public double SelectedMaxSpeed {
            get {
                if (_spdUnits == McpSpeedUnitsType.KNOTS)
                {
                    return _selMaxSpd;
                }
                return _selMaxSpd / 100.0;
            }

            set {
                if (_spdUnits == McpSpeedUnitsType.KNOTS)
                {
                    _selMaxSpd = (int)value;
                } else
                {
                    _selMaxSpd = (int)(value * 100);
                }
            }
        }

        public double SelectedMinSpeed {
            get {
                if (_spdUnits == McpSpeedUnitsType.KNOTS)
                {
                    return _selMinSpd;
                }
                return _selMinSpd / 100.0;
            }

            set {
                if (_spdUnits == McpSpeedUnitsType.KNOTS)
                {
                    _selMinSpd = (int)value;
                } else
                {
                    _selMinSpd = (int)(value * 100);
                }
            }
        }
    }
}
