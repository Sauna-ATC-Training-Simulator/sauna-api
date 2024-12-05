using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Procedures
{
    public abstract class TerminalProcedure : LegCollection
    {
        /// <summary>
        /// The identifier for the airport that this route is associated with
        /// </summary>
        public string AirportIdentifier { get; }

        /// <summary>
        /// The identifier for this terminal procedure. 
        /// <br></br>
        /// For SIDs and STARs, this will be the 5-letter identifier. 
        /// <para></para>
        /// For approaches, the identifier follows a different format: 
        /// <br></br>
        /// <list type="bullet">
        /// <item>The first character indicates the type of approach (ILS, IGS, VOR...)</item>
        /// <item>The next three characters indicate the runway.</item>
        /// <item>The next character indicates, if any, the 'variant' of the approach e.g ILS Y vs ILS Z</item>
        /// </list>
        /// 
        /// </summary>
        public string RouteIdentifier { get; }

        protected abstract List<Transition> FirstTransitions { get; }

        private List<Leg> _commonLegs;

        protected abstract List<Transition> SecondTransitions { get; }

        protected abstract Transition SelectedFirstTransition { get; set; }

        protected abstract Transition SelectedSecondTransition { get; set; }

        private Length _transitionAltitude;

        /// <summary>
        /// Returns the applicable transition altitude to use when flying this SID.
        /// <br></br>
        /// Returns null if the departure has multiple runway transitions and none are selected.
        /// </summary>
        public Length TransitionAltitude
        {
            get
            {
                if (SelectedFirstTransition != null)
                {
                    return SelectedFirstTransition.TransitionAltitude;
                }
                else
                {
                    return _transitionAltitude;
                }
            }
        }

        protected string normaliseRwyTransitionId(string identifier)
        {
            return ("RW" + identifier.ToUpper()).PadRight(5);
        }

        protected void selectFirstTransition(string identifier)
        {
            foreach (var transition in FirstTransitions)
            {
                if (transition.TransitionIdentifier == identifier)
                {
                    SelectedFirstTransition = transition;
                    return;
                }
            }

            throw new ArgumentException("Transition not found");
        }

        protected void selectSecondTransition(string identifier)
        {
            foreach (var transition in SecondTransitions)
            {
                if (transition.TransitionIdentifier == identifier)
                {
                    SelectedSecondTransition = transition;
                    return;
                }
            }

            throw new ArgumentException("Transition not found");
        }


        public override IEnumerator<Leg> GetEnumerator()
        {
            return new TerminalProcEnumerator(this);
        }


        public TerminalProcedure(string airportIdentifier, string routeIdentifier, List<Leg> commonLegs, Length transitionAltitude)
        {
            AirportIdentifier = airportIdentifier;
            RouteIdentifier = routeIdentifier;
            _commonLegs = commonLegs;
            _transitionAltitude = transitionAltitude;
        }

        private class TerminalProcEnumerator : IEnumerator<Leg>
        {
            private TerminalProcedure _parent;

            private int _cursor = 0;

            private int _state = -1;

            public Leg Current
            {
                get
                {
                    switch (_state)
                    {
                        case -1:
                            return null;
                        case 0:
                            return _parent.SelectedFirstTransition.legs[_cursor];
                        case 1:
                            return _parent._commonLegs[_cursor];
                        case 2:
                            return _parent.SelectedSecondTransition.legs[_cursor];
                        case 3:
                            return null;
                        default:
                            throw new IndexOutOfRangeException("Internal error in Terminal Procedure iterator");
                    }
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                switch (_state)
                {
                    case -1:
                        _state++;
                        _cursor = -1;
                        goto case 0;
                    case 0:
                        _cursor++;
                        if (_parent.SelectedFirstTransition == null || _cursor >= _parent.SelectedFirstTransition.legs.Count)
                        {
                            _state++;
                            _cursor = -1;
                            goto case 1;
                        }
                        else
                        {
                            return true;
                        }
                    case 1:
                        _cursor++;
                        if (_cursor >= _parent._commonLegs.Count)
                        {
                            _state++;
                            _cursor = -1;
                            goto case 2;
                        }
                        else
                        {
                            return true;
                        }
                    case 2:
                        _cursor++;
                        if (_parent.SelectedSecondTransition == null || _cursor >= _parent.SelectedSecondTransition.legs.Count)
                        {
                            _state++;
                            _cursor = 0;
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    case 3:
                        return false;
                    default:
                        throw new IndexOutOfRangeException("Internal error in SID iterator");
                }
            }

            public void Reset()
            {
                _state = -1;
                _cursor = 0;
            }
            public TerminalProcEnumerator(TerminalProcedure parent)
            {
                _parent = parent;
                _state = -1;
                _cursor = 0;
            }
        }

        protected virtual string FirstTransitionName => SelectedFirstTransition?.TransitionIdentifier;

        protected virtual string SecondTransitionName => SelectedSecondTransition?.TransitionIdentifier;

        public override string ToString()
        {
            var initialString =
                $"Terminal Procedure: {AirportIdentifier} - {RouteIdentifier}\n" +
                $"{FirstTransitionName}: {SelectedFirstTransition?.TransitionIdentifier} {SecondTransitionName}: {SelectedSecondTransition?.TransitionIdentifier}\n" +
                $"Transition Altitude: {TransitionAltitude}\n" +
                $"All {FirstTransitionName}: \n\n";

            foreach (var transition in FirstTransitions)
            {
                initialString = initialString + transition.ToString() + "\n---------------------------------------\n";
            }

            initialString += "\nCommon legs: \n";

            foreach (var leg in _commonLegs)
            {
                initialString = initialString + "\t" + leg.ToString() + "\n";
            }

            initialString += $"\nAll {SecondTransitionName}: \n\n";

            foreach (var transition in SecondTransitions)
            {
                initialString = initialString + transition.ToString() + "\n---------------------------------------\n";
            }

            return initialString;
        }
    }
}
