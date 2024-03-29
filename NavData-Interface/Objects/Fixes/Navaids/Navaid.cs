﻿using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.Fixes.Navaids
{
    public abstract class Navaid : Fix
    {
        public string AreaCode { get; }
        public string IcaoCode { get; }
        public double Frequency { get; }

        public Length Range { get; }

        protected Navaid(string areaCode, string identifier, string icaoCode, GeoPoint location, string name, double frequency, Length range) : base(identifier, name, location)
        {
            IcaoCode = icaoCode;
            AreaCode = areaCode;
            Frequency = frequency;
            Range = range;
        }
    }
}
