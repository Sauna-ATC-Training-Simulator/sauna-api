using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SaunaSim.Core.Simulator.Aircraft.FMS
{
    public struct FmsDeparture
    {
        public string Runway { get; set; }
        public string Sid { get; set; }
        public string Transition { get; set; }
    }
}
