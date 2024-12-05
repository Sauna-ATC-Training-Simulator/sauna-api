using System;
using System.Collections.Generic;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.FMS
{
    public struct FmsArrival
    {
        public string Runway { get; set; }
        public string Star { get; set; }
        public string StarTransition { get; set; }
        public string Approach { get; set; }
        public string ApproachTransition { get; set; }
    }
}
