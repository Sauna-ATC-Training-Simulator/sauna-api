﻿using AviationCalcUtilNet.GeoTools;
using NavData_Interface;
using NavData_Interface.DataSources;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fix;
using SaunaSim.Core.Data.NavData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace SaunaSim.Core.Data
{
    public static class DataHandler
    {
        private static NavDataInterface _navigraphInterface;
        private static string _uuid;
        private static object _navigraphInterfaceLock = new object();

        private static List<NavDataInterface> _sctFileInterfaces = new List<NavDataInterface>();
        private static object _sctFileInterfacesLock = new object();

        private static NavDataInterface _customDataInterface = new NavDataInterface(new CustomNavDataSource());
        private static object _customDataLock = new object();

        public static bool HasNavigraphDataLoaded()
        {
            lock (_navigraphInterfaceLock)
            {
                return _navigraphInterface != null;
            }
        }

        public static string GetNavigraphFileUuid()
        {
            lock (_navigraphInterfaceLock)
            {
                return _uuid;
            }
        }

        public static List<string> GetSectorFilesLoaded()
        {
            List<string> retList = new List<string>();
            lock (_sctFileInterfacesLock)
            {
                foreach (NavDataInterface navdataInterface in _sctFileInterfaces)
                {
                    if (navdataInterface.Data_source is SCTSource sctSource)
                    {
                        retList.Add(sctSource.FileName);
                    }
                }
            }

            return retList;
        }

        public static void LoadSectorFile(string filename)
        {
            lock (_sctFileInterfacesLock)
            {
                foreach (NavDataInterface navdataInterface in _sctFileInterfaces)
                {
                    if (navdataInterface.Data_source is SCTSource sctSource)
                    {
                        if (sctSource.FileName == filename)
                        {
                            return;
                        }
                    }
                }
                _sctFileInterfaces.Add(new NavDataInterface(new SCTSource(filename)));
            }
        }

        public static void LoadNavigraphDataFile(string fileName, string uuid)
        {
            lock (_navigraphInterfaceLock)
            {
                _navigraphInterface = new NavDataInterface(new DFDSource(fileName));
                _uuid = uuid;
            }
        }

        public static void AddPublishedHold(PublishedHold hold)
        {
            lock (_customDataLock)
            {
                if (_customDataInterface.Data_source is CustomNavDataSource customSource)
                {
                    customSource.AddPublishedHold(hold);
                }
            }
        }

        public static PublishedHold GetPublishedHold(string wp, double lat, double lon)
        {
            Fix fix = GetClosestWaypointByIdentifier(wp, lat, lon);

            lock (_customDataLock)
            {
                if (fix != null && _customDataInterface.Data_source is CustomNavDataSource customSource)
                {
                    return customSource.GetPublishedHold(fix);
                }
            }

            return null;
        }

        public static void AddLocalizer(Localizer wp)
        {
            lock (_customDataLock)
            {
                if (_customDataInterface.Data_source is CustomNavDataSource customSource)
                {
                    customSource.AddLocalizer(wp);
                }
            }
        }

        public static Localizer GetLocalizer(string airportIdent, string rwyIdent)
        {
            lock (_customDataLock)
            {
                return _customDataInterface.Data_source.GetLocalizerFromAirportRunway(airportIdent, rwyIdent);
            }
        }

        public static Fix GetClosestWaypointByIdentifier(string wpId, double lat, double lon)
        {
            Fix closestFix = null;
            double closestDistance = double.MaxValue;

            lock (_navigraphInterfaceLock)
            {
                Fix navigraphFix = _navigraphInterface.GetClosestFixByIdentifier(new GeoPoint(lat, lon), wpId);
                if (navigraphFix != null)
                {
                    closestFix = navigraphFix;
                    closestDistance = GeoPoint.DistanceM(new GeoPoint(lat, lon), closestFix.Location);
                }
            }

            lock (_sctFileInterfacesLock)
            {
                foreach (NavDataInterface navdataInterface in _sctFileInterfaces)
                {
                    Fix sctFix = navdataInterface.GetClosestFixByIdentifier(new GeoPoint(lat, lon), wpId);
                    if (sctFix != null)
                    {
                        double distance = GeoPoint.DistanceM(new GeoPoint(lat, lon), sctFix.Location);

                        if (distance < closestDistance - 1000)
                        {
                            closestFix = sctFix;
                            closestDistance = distance;
                        }
                    }
                }
            }

            lock (_customDataLock)
            {
                Fix customFix = _customDataInterface.GetClosestFixByIdentifier(new GeoPoint(lat, lon), wpId);
                if (customFix != null)
                {
                    double distance = GeoPoint.DistanceM(new GeoPoint(lat, lon), customFix.Location);

                    if (distance < closestDistance - 1000)
                    {
                        closestFix = customFix;
                        closestDistance = distance;
                    }
                }
            }

            return closestFix;
        }
    }
}
