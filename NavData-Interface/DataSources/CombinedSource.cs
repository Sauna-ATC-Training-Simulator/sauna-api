using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fixes;
using NavData_Interface.Objects.LegCollections.Airways;
using NavData_Interface.Objects.LegCollections.Procedures;
using System.Collections;
using System.Collections.Generic;

namespace NavData_Interface.DataSources
{
    public class CombinedSource : DataSource, IEnumerable<KeyValuePair<int, DataSource>>
    {
        private SortedList<int, DataSource> _sources = new SortedList<int, DataSource>();

        private string _id;

        public override string GetId()
        {
            return _id;
        }

        public CombinedSource(string id)
        {
            _id = id;
        }
        
        /// <summary>
        /// Creates a new combined source from a list of DataSources
        /// </summary>
        /// <param name="sources">Sources to add on construction, going from highest priority (leftmost argument) to lowest priority (rightmost argument)</param>
        public CombinedSource(string id, params DataSource[] sources) : this(id)
        {
            _sources = new SortedList<int, DataSource>();

            int priority = 0;
            foreach (var source in sources)
            {
                _sources.Add(priority, source);

                priority++;
            }
        }

        /// <summary>
        /// Adds the specified source to the sources, with the lowest priority
        /// </summary>
        /// <param name="source">The source to be added</param>
        /// <returns>true if the source was added, or false if the CombinedSource already contains this source</returns>
        public bool AddSource(DataSource source)
        {
            var lastPriority = _sources.Keys[_sources.Keys.Count - 1];
            return AddSource(source, lastPriority);
        }

        /// <summary>
        /// Adds the specified source to the sources, with the specified priority.
        /// </summary>
        /// <param name="source">The source to be added</param>
        /// <param name="priority">The priority of the source</param>
        /// <returns>true if the source was added, or false if the CombinedSource already contains this source</returns>
        /// <exception cref="ArgumentException"></exception>
        public bool AddSource(DataSource source, int priority)
        {
            if (!_sources.ContainsValue(source))
            {
                _sources.Add(priority, source);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the source with the specified Id from the list of sources.
        /// </summary>
        /// <param name="sourceId">The Id of the source to remove</param>
        /// <returns>true if the source was found and removed, false if the source wasn't found in the sources list</returns>
        public bool RemoveSource(string sourceId)
        {
            foreach(var source in _sources)
            {
                if (source.Value.GetId() == sourceId)
                {
                    _sources.Remove(source.Key);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Changes the priority of an already-added source
        /// </summary>
        /// <param name="sourceId">The string-id of the source to modify</param>
        /// <param name="newPriority">The new priority that the source will have</param>
        /// <returns>true if the priority was changed, false if the source was not found</returns>
        public bool ChangePriority(string sourceId, int newPriority)
        {
            foreach(var source in _sources)
            {
                if (source.Value.GetId() == sourceId)
                {
                    _sources.Remove(source.Key);

                    _sources.Add(newPriority, source.Value);

                    return true;
                }
            }

            return false;
        }

        public override bool IsValidAirwayIdentifier(string airwayIdentifier)
        {
            foreach (var source in _sources)
            {
                if (source.Value.IsValidAirwayIdentifier(airwayIdentifier))
                {
                    return true;
                }
            }

            return false;
        }

        public override List<Fix> GetFixesByIdentifier(string identifier)
        {
            var fixes = new List<Fix>();

            int lastIndexToCheck = 0;

            foreach(var source in _sources.Values)
            {
                foreach(var fix in source.GetFixesByIdentifier(identifier))
                {
                    // before adding each fix in the list, we have to check if this fix is already in the list
                    for (int i = 0; i < lastIndexToCheck; i++)
                    {
                        if (GeoPoint.Distance(fixes[i].Location, fix.Location) < Length.FromMeters(1000))
                        {
                            goto nextFixInSource;
                        }
                    }

                    fixes.Add(fix);

                    nextFixInSource: { }
                }

                lastIndexToCheck = fixes.Count;
            }

            return fixes;
        }

        public override Localizer GetLocalizerFromAirportRunway(string airportIdentifier, string runwayIdentifier)
        {
            foreach (var source in _sources.Values)
            {
                var localizer = source.GetLocalizerFromAirportRunway(airportIdentifier, runwayIdentifier);

                if (localizer != null)
                {
                    return localizer;
                }
            }

            return null;
        }

        public override Airport GetClosestAirportWithinRadius(GeoPoint position, Length radius)
        {
            double closestDistance = double.MaxValue;
            Airport closestAirport = null;

            foreach (var source in _sources.Values)
            {
                Airport currentSourceAirport = source.GetClosestAirportWithinRadius(position, radius);
                double currentDistance = GeoPoint.Distance(position, currentSourceAirport.Location).Meters;

                if (currentDistance < closestDistance)
                {
                    if (closestAirport != null && closestAirport.Identifier == currentSourceAirport.Identifier)
                    {
                        continue;
                    }

                    closestDistance = currentDistance;
                    closestAirport = currentSourceAirport;
                }
            }

            return closestAirport;
        }

        public IEnumerator<KeyValuePair<int, DataSource>> GetEnumerator()
        {
            return _sources.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _sources.GetEnumerator();
        }

        public override Runway GetRunwayFromAirportRunwayIdentifier(string airportIdentifier, string runwayIdentifier)
        {
            foreach (var source in _sources.Values)
            {
                var runway = source.GetRunwayFromAirportRunwayIdentifier(airportIdentifier, runwayIdentifier);

                if (runway != null)
                {
                    return runway;
                }
            }

            return null;
        }

        public override Airway GetAirwayFromIdentifierAndFixes(string airwayIdentifier, Fix startFix, Fix endFix)
        {
            foreach (var source in _sources.Values)
            {
                var airway = source.GetAirwayFromIdentifierAndFixes(airwayIdentifier, startFix, endFix);

                if (airway != null)
                {
                    return airway;
                }
            }

            return null;
        }

        public bool HasSourceType<T>()
        {
            foreach (var source in _sources.Values)
            {
                if (source is T)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the first instance of a source of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the DataSource required</typeparam>
        /// <returns></returns>
        public T GetSourceType<T>() where T : DataSource
        {
            foreach (var source in _sources.Values)
            {
                if (source is T)
                {
                    return (T)source;
                }
            }

            return null;
        }

        public override Airport GetAirportByIdentifier(string airportIdentifier)
        {
            foreach (var source in _sources.Values)
            {
                var airport = source.GetAirportByIdentifier(airportIdentifier);

                if (airport != null)
                {
                    return airport;
                }
            }

            return null;
        }

        public override Sid GetSidByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            foreach (var source in _sources.Values)
            {
                var airport = source.GetSidByAirportAndIdentifier(airportIdentifier, sidIdentifier);

                if (airport != null)
                {
                    return airport;
                }
            }

            return null;
        }

        public override Star GetStarByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            foreach (var source in _sources.Values)
            {
                var airport = source.GetStarByAirportAndIdentifier(airportIdentifier, sidIdentifier);

                if (airport != null)
                {
                    return airport;
                }
            }

            return null;
        }
    }
}