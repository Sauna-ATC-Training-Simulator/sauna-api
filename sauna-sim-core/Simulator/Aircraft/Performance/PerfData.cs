using System;
using System.Collections.Generic;
using System.Text;
using AviationCalcUtilNet.Math;

namespace SaunaSim.Core.Simulator.Aircraft.Performance
{
    public class PerfData
    {
        // Weights
        public double OEW_kg { get; set; }
        public double MZFW_kg { get; set; }
        public double MFuel_kg { get; set; }
        public double MTOW_kg { get; set; }
        public double MLW_kg { get; set; }
        
        // Performance
        public int Engines { get; set; }
        public bool EnginesReverse { get; set; }
        public List<PerfConfigSetting> ConfigList { get; set; }
        public int DataPointMass_kg { get; set; }
        public int DataPointDeltaMass_kg { get; set; }
        public int DeltaMassVsPenalty_fpm { get; set; }
        public int SpeedBrakeVsPenalty_fpm { get; set; }
        public List<(int, List<(int, PerfDataPoint)>)> DataPoints { get; set; }
        public List<(int, int)> ApproachSpeedGates { get; set; }

        //  Perf Init Data
        public int V2_KIAS { get; set; }
        public int Climb_KIAS { get; set; }
        public double Climb_Mach { get; set; }
        public int Cruise_KIAS { get; set; }
        public double Cruise_Mach { get; set; }
        public int Descent_KIAS { get; set; }
        public double Descent_Mach { get; set; }

        private ((int, List<(int, PerfDataPoint)>), (int, List<(int, PerfDataPoint)>)) GetBoundingAltitudes(int alt)
        {
            // Check if list is empty
            if (DataPoints == null || DataPoints.Count < 1)
            {
                return ((alt, new List<(int, PerfDataPoint)>()), (alt, new List<(int, PerfDataPoint)>()));
            }
            
            // Check if the list only has 1 item
            var alt1 = DataPoints[0];
            (int, List<(int, PerfDataPoint)>) alt2;
            
            if (DataPoints.Count < 2)
            {
                alt2 = DataPoints[0];
            }
            else
            {
                alt2 = DataPoints[1];
                for (int i = 1; i < DataPoints.Count - 1 && DataPoints[i].Item1 < alt; i++)
                {
                    alt1 = DataPoints[i];
                    alt2 = DataPoints[i + 1];
                }
            }

            return (alt1, alt2);
        }

        private ((int, PerfDataPoint), (int, PerfDataPoint)) GetBoundingAirspeeds(List<(int, PerfDataPoint)> iasList, int ias)
        {
            // Check if list is empty
            if (iasList == null || iasList.Count < 1)
            {
                return ((ias, new PerfDataPoint()), (ias, new PerfDataPoint()));
            }

            // Check if the list only has 1 item
            var ias1 = iasList[0];
            (int, PerfDataPoint) ias2;
            
            if (iasList.Count < 2)
            {
                ias2 = iasList[0];
            }
            else
            {
                ias2 = iasList[1];
                for (int i = 1; i < iasList.Count - 1 && iasList[i].Item1 < ias; i++)
                {
                    ias1 = iasList[i];
                    ias2 = iasList[i + 1];
                }
            }

            return (ias1, ias2);
        }

        public PerfDataPoint GetDataPoint(int dens_alt_ft, int ias_kts, int mass_kg, double spdBrake, int config)
        {
            // Get Box Points
            (var alt1, var alt2) = GetBoundingAltitudes(dens_alt_ft);
            (var alt1Ias1, var alt1Ias2) = GetBoundingAirspeeds(alt1.Item2, ias_kts);
            (var alt2Ias1, var alt2Ias2) = GetBoundingAirspeeds(alt2.Item2, ias_kts);

            // Interpolate
            var alt1Interp = PerfDataPoint.Interpolate(alt1Ias1.Item1, alt1Ias2.Item1, alt1Ias1.Item2, alt1Ias2.Item2, ias_kts);
            var alt2Interp = PerfDataPoint.Interpolate(alt2Ias1.Item1, alt2Ias2.Item1, alt2Ias1.Item2, alt2Ias2.Item2, ias_kts);

            PerfDataPoint interp = PerfDataPoint.Interpolate(alt1.Item1, alt2.Item1, alt1Interp, alt2Interp, dens_alt_ft);
            
            // Calculate Mass Penalty
            double massMult = (mass_kg - DataPointMass_kg) / (double)(DataPointDeltaMass_kg - DataPointMass_kg);
            double curMassVsPenalty = MathUtil.InterpolateNumbers(0, DeltaMassVsPenalty_fpm, massMult);
            
            // Calculate Speedbrake Penalty
            double curSpdBrkVsPenalty = MathUtil.InterpolateNumbers(0, SpeedBrakeVsPenalty_fpm, spdBrake);
            
            // Calculate Config Penalty/Pitch Change
            double curCfgVsPenalty = 0, curCfgPitchDelta = 0;
            if (ConfigList != null && config >= 0 && config < ConfigList.Count)
            {
                curCfgVsPenalty = ConfigList[config].VsPenalty;
                curCfgPitchDelta = ConfigList[config].PitchChange;
            }
            
            // Calculate totalVsPenalty
            double totalVsPenalty = curMassVsPenalty + curSpdBrkVsPenalty + curCfgVsPenalty;
            
            // Apply corrections
            interp.VsClimb += (int) totalVsPenalty;
            interp.VsDescent += (int) totalVsPenalty;
            interp.PitchClimb += curCfgPitchDelta;
            interp.PitchDescent += curCfgPitchDelta;

            return interp;
        }
    }
}