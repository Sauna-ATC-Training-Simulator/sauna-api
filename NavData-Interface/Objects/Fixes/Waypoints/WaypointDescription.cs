using System;
using System.Collections.Generic;
using System.Text;

namespace NavData_Interface.Objects.Fixes.Waypoints
{
    /// <summary>
    /// Essentially, the use for this waypoint for a specific leg of a procedure.
    /// </summary>
    public class WaypointDescription
    {
        public bool IsEndOfRoute { get; }

        public bool IsFlyOver { get; }

        public bool IsMissedAppStart { get; }

        public WaypointDescription(
            bool isEndOfRoute,
            bool isFlyOver,
            bool isMissedAppStart) {
            
            this.IsEndOfRoute = isEndOfRoute;
            this.IsFlyOver = isFlyOver;
            this.IsMissedAppStart = isMissedAppStart;
        }
        public override string ToString()
        {
            List<string> descriptions = new List<string>();

            if (IsEndOfRoute) descriptions.Add("End");
            if (IsFlyOver) descriptions.Add("Fly Over");
            if (IsMissedAppStart) descriptions.Add("Missed App Start");

            var final = string.Join(", ", descriptions);

            if (final == "")
            {
                final = "-";
            }

            return $"{final}";
        }

    }
}
