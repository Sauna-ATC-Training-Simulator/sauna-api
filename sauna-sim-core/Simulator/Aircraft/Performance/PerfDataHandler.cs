using System;
using System.Collections.Generic;
using System.Text;
using AviationCalcUtilNet.MathTools;

namespace SaunaSim.Core.Simulator.Aircraft.Performance
{
    public static class PerfDataHandler
    {
        public static PerfData LookupForAircraft(string icaoEquip)
        {
            PerfData a320 = new PerfData()
            {
                V2_KIAS = 180,
                Climb_KIAS = 270,
                Climb_Mach = 0.73,
                Cruise_KIAS = 290,
                Cruise_Mach = 0.78,
                Descent_KIAS = 290,
                Descent_Mach = 0.78,
                Engines = 2,
                EnginesReverse = true,
                ApproachSpeedGates = new List<(int, int)>()
                {
                    (4, 140),
                    (6, 160),
                    (10, 180),
                    (15, 210)
                },
                ConfigList = new List<PerfConfigSetting>()
                {
                    new PerfConfigSetting()
                    {
                        GearDown = false,
                        MaxKias = 350,
                        MinKias = 180,
                        NormKias = 250,
                        VsPenalty = 0,
                        PitchChange = 0
                    },
                    new PerfConfigSetting()
                    {
                        GearDown = false,
                        MaxKias = 230,
                        MinKias = 160,
                        NormKias = 210,
                        VsPenalty = 0,
                        PitchChange = 0
                    },
                    new PerfConfigSetting()
                    {
                        GearDown = false,
                        MaxKias = 215,
                        MinKias = 150,
                        NormKias = 180,
                        VsPenalty = -400,
                        PitchChange = -4
                    },
                    new PerfConfigSetting()
                    {
                        GearDown = true,
                        MaxKias = 200,
                        MinKias = 140,
                        NormKias = 160,
                        VsPenalty = -800,
                        PitchChange = -9.5
                    },
                    new PerfConfigSetting()
                    {
                        GearDown = true,
                        MaxKias = 180,
                        MinKias = 110,
                        NormKias = 140,
                        VsPenalty = -1000,
                        PitchChange = -10
                    }
                },
                MTOW_kg = 78000,
                MLW_kg = 66000,
                MZFW_kg = 62500,
                OEW_kg = 42600,
                MFuel_kg = 23963,
                DeltaMassVsPenalty_fpm = -900,
                SpeedBrakeVsPenalty_fpm = -800,
                DataPointMass_kg = 59000,
                DataPointDeltaMass_kg = 74400,
                DataPoints = new List<(int, List<(int, PerfDataPoint)>)>()
            };
            
            // Load PerfDataPoints from file
            string[] filelines = System.IO.File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory+ @"perf-data-files/A320.csv");
            (int, List<(int, PerfDataPoint)>) curAlt = (-1, null);
            foreach (var line in filelines)
            {
                string[] split = line.Split(',');
                if (split.Length >= 10)
                {
                    int alt = Convert.ToInt32(split[0]);
                    int ias = Convert.ToInt32(split[1]);
                    if (curAlt.Item1 == -1)
                    {
                        curAlt = (alt, new List<(int, PerfDataPoint)>());
                    } else if (alt != curAlt.Item1)
                    {
                        a320.DataPoints.Add(curAlt);
                        curAlt = (alt, new List<(int, PerfDataPoint)>());
                    }
                    
                    curAlt.Item2?.Add((ias, new PerfDataPoint()
                    {
                        VsClimb = Convert.ToInt32(split[2]),
                        VsDescent = Convert.ToInt32(split[3]),
                        PitchClimb = Convert.ToDouble(split[4]),
                        PitchDescent = Convert.ToDouble(split[5]),
                        AccelLevelMaxThrust = Convert.ToDouble(split[6]),
                        AccelLevelIdleThrust = Convert.ToDouble(split[7]),
                        N1Climb = Convert.ToDouble(split[8]),
                        N1Descent = Convert.ToDouble(split[9]),
                    }));
                }
            }

            return a320;
        }

        public static double GetRequiredPitchForThrust(PerfData perfData, double thrustLeverPos, double desiredAccelFwd, double ias_kts, double dens_alt_ft, double mass_kg,
            double spdBrake, int config)
        {
            PerfDataPoint dataPoint = perfData.GetDataPoint((int) dens_alt_ft, (int) ias_kts, (int) mass_kg, spdBrake, config);

            double zeroAccelThrust = -dataPoint.AccelLevelIdleThrust / (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust);
            double thrustDelta = ((desiredAccelFwd - dataPoint.AccelLevelIdleThrust) / (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust)) - zeroAccelThrust;
            double pitchPerc = thrustLeverPos - thrustDelta;
            double pitch_degs = pitchPerc * (dataPoint.PitchClimb - dataPoint.PitchDescent) + dataPoint.PitchDescent;

            return pitch_degs;
        }

        public static double GetRequiredPitchForVs(PerfData perfData, double desiredVs, double ias_kts, double dens_alt_ft, double mass_kg,
            double spdBrake, int config)
        {
            PerfDataPoint dataPoint = perfData.GetDataPoint((int)dens_alt_ft, (int)ias_kts, (int)mass_kg, spdBrake, config);

            double pitchPerc = (desiredVs - dataPoint.VsDescent) / (dataPoint.VsClimb - dataPoint.VsDescent);
            double pitch_degs = (pitchPerc * (dataPoint.PitchClimb - dataPoint.PitchDescent)) + dataPoint.PitchDescent;

            return pitch_degs;
        }

        public static double GetRequiredThrustForVs(PerfData perfData, double vs, double desiredAccelFwd, double ias_kts, double dens_alt_ft, double mass_kg, double spdBrake,
            int config)
        {
            PerfDataPoint dataPoint = perfData.GetDataPoint((int) dens_alt_ft, (int) ias_kts, (int) mass_kg, spdBrake, config);

            double zeroAccelThrust = -dataPoint.AccelLevelIdleThrust / (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust);
            double thrustDelta = ((desiredAccelFwd - dataPoint.AccelLevelIdleThrust) / (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust)) - zeroAccelThrust;
            double pitchPerc = (vs - dataPoint.VsDescent) / (dataPoint.VsClimb - dataPoint.VsDescent);
            return thrustDelta + pitchPerc;
        }

        public static (double accelFwd, double vs) CalculatePerformance(PerfData perfData, double pitch_degs, double thrustLeverPos, double ias_kts, double dens_alt_ft, double mass_kg, double spdBrake, int config)
        {
            PerfDataPoint dataPoint = perfData.GetDataPoint((int) dens_alt_ft, (int) ias_kts, (int) mass_kg, spdBrake, config);
            double pitchPerc = (pitch_degs - dataPoint.PitchDescent) / (dataPoint.PitchClimb - dataPoint.PitchDescent);
            double vs = pitchPerc * (dataPoint.VsClimb - dataPoint.VsDescent) + dataPoint.VsDescent;
            double thrustDelta = thrustLeverPos - pitchPerc;
            double zeroAccelThrust = -dataPoint.AccelLevelIdleThrust / (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust);
            double accelFwd = ((zeroAccelThrust + thrustDelta) * (dataPoint.AccelLevelMaxThrust - dataPoint.AccelLevelIdleThrust)) + dataPoint.AccelLevelIdleThrust;

            return (accelFwd, vs);
        }
    }
}