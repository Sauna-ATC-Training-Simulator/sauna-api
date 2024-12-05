using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface;
using NavData_Interface.DataSources;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fixes;
using NavData_Interface.Objects.LegCollections.Airways;
using NavData_Interface.Objects.LegCollections.Procedures;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SaunaSim.Core.Data
{
    public static class DataHandler
    {
        private static CombinedSource _navdataSource;
        private static ReaderWriterLockSlim _navdataMutex = new ReaderWriterLockSlim();

        public static readonly string FAKE_AIRPORT_NAME = "_FAKE_AIRPORT";

        private static readonly int NAVIGRAPH_PRIORITY = 0;
        private static readonly int SCT_PRIORITY = 1;
        private static readonly int MEM_PRIORITY = 2;

        static DataHandler()
        {
            _navdataSource = new CombinedSource("_combined_source_sauna_api");
            _navdataSource.AddSource(new InMemorySource("sauna_api_in_memory_navdata_source"), MEM_PRIORITY);
        }

        public static bool HasNavigraphDataLoaded()
        {
            try
            {
                _navdataMutex.EnterReadLock();
                return _navdataSource.HasSourceType<DFDSource>();
            } finally
            {
                _navdataMutex.ExitReadLock();
            }
        }

        public static string GetNavigraphFileUuid()
        {
            try
            {
                _navdataMutex.EnterReadLock();
                // Find DFD Source
                foreach (KeyValuePair<int, DataSource> pair in _navdataSource)
                {
                    if (pair.Value is DFDSource dfdSource)
                    {
                        return dfdSource.GetId();
                    }
                }
                return "";
            } finally
            {
                _navdataMutex.ExitReadLock();
            }
        }

        public static List<string> GetSectorFilesLoaded()
        {
            List<string> retList = new List<string>();

            try
            {
                _navdataMutex.EnterReadLock();
                foreach (KeyValuePair<int, DataSource> pair in _navdataSource)
                {
                    if (pair.Value is SCTSource sctSource)
                    {
                        retList.Add(sctSource.FileName);
                    }
                }

                return retList;
            } finally
            {
                _navdataMutex.ExitReadLock();
            }
        }

        public static void LoadSectorFile(string filename)
        {
            try
            {
                _navdataMutex.EnterWriteLock();
                _navdataSource.AddSource(new SCTSource(filename), SCT_PRIORITY);
            } finally
            {
                _navdataMutex.ExitWriteLock();
            }
        }

        public static void LoadNavigraphDataFile(string fileName, string uuid)
        {
            try
            {
                _navdataMutex.EnterWriteLock();
                _navdataSource.AddSource(new DFDSource(fileName, uuid), NAVIGRAPH_PRIORITY);
            } finally
            {
                _navdataMutex.ExitWriteLock();
            }
        }

        public static void AddAirport(Airport airport)
        {
            try
            {
                _navdataMutex.EnterWriteLock();
                _navdataSource.GetSourceType<InMemorySource>()?.AddAirport(airport);
            } finally
            {
                _navdataMutex.ExitWriteLock();
            }
        }

        public static void AddPublishedHold(PublishedHold hold)
        {
            try
            {
                _navdataMutex.EnterWriteLock();
                _navdataSource.GetSourceType<InMemorySource>()?.AddPublishedHold(hold);
            } finally
            {
                _navdataMutex.ExitWriteLock();
            }
        }

        public static PublishedHold GetPublishedHold(string wp, GeoPoint point)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                Fix fix = _navdataSource.GetClosestFixByIdentifier(point, wp);

                if (fix != null)
                {
                    var hold = _navdataSource.GetSourceType<InMemorySource>()?.GetPublishedHold(fix);
                    return hold;
                }

                return null;
            } finally
            {
                _navdataMutex.ExitReadLock();
            }
        }

        public static void AddLocalizer(Localizer wp)
        {
            try
            {
                _navdataMutex.EnterWriteLock();
                _navdataSource.GetSourceType<InMemorySource>()?.AddLocalizer(wp);
            } finally
            {
                _navdataMutex.ExitWriteLock();
            }
        }

        public static Airport GetAirportByIdentifier(string airportIdent)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var airport = _navdataSource.GetAirportByIdentifier(airportIdent.ToUpper());
                return airport;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Localizer GetLocalizer(string airportIdent, string rwyIdent)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var airport = _navdataSource.GetLocalizerFromAirportRunway(airportIdent.ToUpper(), rwyIdent.ToUpper());
                return airport;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Fix GetClosestWaypointByIdentifier(string wpId, GeoPoint point)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var airport = _navdataSource.GetClosestFixByIdentifier(point, wpId);
                return airport;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Sid GetSidByAirportAndIdentifier(string aiportIdentifier, string sidIdentifier)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var sid = _navdataSource.GetSidByAirportAndIdentifier(aiportIdentifier, sidIdentifier);
                return sid;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Sid GetSidByAirportAndIdentifier(Fix airport, string sidIdentifier)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var sid = _navdataSource.GetSidByAirportAndIdentifier(airport.Identifier, sidIdentifier);
                return sid;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Airway GetAirwayFromIdentifierAndFixes(string airwayIdentifier, Fix startFix, Fix endFix)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var airway = _navdataSource.GetAirwayFromIdentifierAndFixes(airwayIdentifier, startFix, endFix);
                return airway;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static bool IsValidAirwayIdentifier(string airwayIdentifier)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var result = _navdataSource.IsValidAirwayIdentifier(airwayIdentifier);
                return result;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Star GetStarByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var sid = _navdataSource.GetStarByAirportAndIdentifier(airportIdentifier, sidIdentifier);
                return sid;
            } finally { _navdataMutex.ExitReadLock(); }
        }

        public static Star GetStarByAirportAndIdentifier(Fix airport, string sidIdentifier)
        {
            try
            {
                _navdataMutex.EnterReadLock();
                var sid = _navdataSource.GetStarByAirportAndIdentifier(airport.Identifier, sidIdentifier);
                return sid;
            } finally { _navdataMutex.ExitReadLock(); }
        }
    }
}
