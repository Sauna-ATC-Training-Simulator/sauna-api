using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Procedures
{
    public class Star : TerminalProcedure
    {
        private List<Transition> _transitions = new List<Transition>();
        private Transition _selectedTransition = null;

        private List<Transition> _rwyTransitions = new List<Transition>();
        private Transition _selectedRwyTransition = null;

        protected override List<Transition> FirstTransitions {  get { return _transitions; } }
        protected override Transition SelectedFirstTransition { get { return _selectedTransition; } set { _selectedTransition = value; } }
        
        protected override List<Transition> SecondTransitions { get { return _rwyTransitions; } }
        protected override Transition SelectedSecondTransition { get { return _selectedRwyTransition;} set { _selectedRwyTransition = value; } }
        public void selectRunwayTransition(string runwayTransitionIdentifier)
        {
            try
            {
                selectSecondTransition(normaliseRwyTransitionId(runwayTransitionIdentifier));
            }
            catch (ArgumentException ex)
            {
                // Maybe we want, say 01L or R, but the transition is for both
                // So try 01B
                if (runwayTransitionIdentifier.EndsWith("R") || runwayTransitionIdentifier.EndsWith("L"))
                {
                    selectSecondTransition(normaliseRwyTransitionId(runwayTransitionIdentifier).Remove(4) + "B");
                }
            }
        }

        public void selectTransition(string transitionIdentifier) => selectFirstTransition(transitionIdentifier);


        public Star(string airportIdentifier, string routeIdentifier, List<Transition> transitions, List<Leg> commonLegs, List<Transition> rwyTransitions, Length transitionAltitude) : base(airportIdentifier, routeIdentifier, commonLegs, transitionAltitude)
        {
            _transitions = transitions;
            _rwyTransitions = rwyTransitions;
        }
    }
}
