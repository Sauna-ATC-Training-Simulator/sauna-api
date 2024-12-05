using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Procedures
{
    public class Transition : LegCollection
    {
        internal List<Leg> legs;

        public string TransitionIdentifier { get; }

        public Length TransitionAltitude { get; }

        public override IEnumerator<Leg> GetEnumerator()
        {
            return legs.GetEnumerator();
        }

        public Transition(List<Leg> legs, string transitionIdentifier, Length transitionAltitude)
        {
            this.legs = legs;
            TransitionIdentifier = transitionIdentifier;
            TransitionAltitude = transitionAltitude;
        }

        public override string ToString()
        {
            var transitionString =
                $"Transition: {TransitionIdentifier}\n" +
                $"Transition Altitude: {TransitionAltitude?.Feet:F0}\n" +
                "\nLegs:\n";

            foreach (var leg in legs)
            {
                transitionString += $"\t{leg.ToString()}\n";
            }

            return transitionString;
        }

    }
}
