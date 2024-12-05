using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.LegCollections.Procedures
{
    public class Arrival : LegCollection
    {
        private Star _star;
        private Approach _approach;

        public Arrival(Star star, Approach approach)
        {
            _star = star;
            _approach = approach;
        }

        public override IEnumerator<Leg> GetEnumerator()
        {
            return new ArrivalEnumerator(_star, _approach);
        }

        private class ArrivalEnumerator : IEnumerator<Leg>
        {
            private IEnumerator<Leg> _starEnumerator;
            private IEnumerator<Leg> _approachEnumerator;

            private bool finishedStar = false;

            public Leg Current { get
                {
                    if (!finishedStar)
                    {
                        return _starEnumerator.Current;
                    } else
                    {
                        return _approachEnumerator.Current;
                    }
                } 
            }

            object IEnumerator.Current => Current;

            public ArrivalEnumerator(Star star, Approach app) {
                _starEnumerator = star.GetEnumerator();
                _approachEnumerator = app.GetEnumerator();
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                if (_starEnumerator.MoveNext())
                {
                    return true;
                } else
                {
                    finishedStar = true;
                    return _approachEnumerator.MoveNext();
                }
            }

            public void Reset()
            {
                _starEnumerator.Reset();
                _approachEnumerator.Reset();
            }
        }
    }
}
