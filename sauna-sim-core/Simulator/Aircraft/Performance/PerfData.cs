using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.Performance
{
    public class PerfData
    {
        public int MinMass_kg {get; set;}
        public int MaxMass_kg { get;set;}
        public int IdleThrust_N { get; set; }
        public int MaxThrust_N { get; set; }

        public int Climb_KIAS { get; set; }
        public double Climb_Mach { get; set; }
        public int Cruise_KIAS { get; set; }
        public double Cruise_Mach { get;set; }
        public int Descent_KIAS { get; set; }
        public double Descent_Mach { get; set; }
    }
}
