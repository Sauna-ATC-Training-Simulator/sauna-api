using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Procedures
{
    public class Approach : TerminalProcedure
    {
        protected List<Transition> _vias = new List<Transition>();
        protected Transition _selectedVia = null;
        protected override List<Transition> FirstTransitions => _vias;

        protected override Transition SelectedFirstTransition { get => _selectedVia; set => _selectedVia = value; }

        protected override List<Transition> SecondTransitions => null;
        protected override Transition SelectedSecondTransition { get => null; set => throw new NotImplementedException(); }
        protected override string SecondTransitionName => "";

        public Approach(string airportIdentifier, string routeIdentifier, List<Leg> commonLegs, List<Transition> vias, Length transitionAltitude) : base(airportIdentifier, routeIdentifier, commonLegs, transitionAltitude)
        {
            _vias = vias;
        }
    }
}
