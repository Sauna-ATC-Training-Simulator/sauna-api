using System;
using System.Collections.Generic;
using System.Text;
using NavData_Interface.Objects.Fixes;
using AviationCalcUtilNet.GeoTools;
using NavData_Interface.Objects.Fixes.Navaids;
using NavData_Interface.DataSources.DFDUtility;
using NavData_Interface.DataSources.DFDUtility.Factory;
using NavData_Interface.Objects;
using AviationCalcUtilNet.MathTools;
using NavData_Interface.Objects.Fixes.Waypoints;
using NavData_Interface.Objects.LegCollections.Airways;
using AviationCalcUtilNet.Units;
using AviationCalcUtilNet.Geo;
using Microsoft.Data.Sqlite;
using NavData_Interface.Objects.LegCollections.Procedures;

namespace NavData_Interface.DataSources
{
    public class DFDSource : DataSource
    {
        private SqliteConnection _connection;

        public string Airac_version { get; }

        public string Uuid { get; }

        public override string GetId()
        {
            return Uuid;
        }

        public DFDSource(string filePath) : this(filePath, "")
        {

        }

        /// <summary>
        /// Creates a new DFD data source.
        /// </summary>
        /// <param name="filePath">The path to the DFD file.</param>
        /// <exception cref="System.IO.FileNotFoundException">If the provided file wasn't found</exception>
        public DFDSource(string filePath, string uuid)
        {
            Uuid = uuid;

            var connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            
            try
            {
                _connection.Open();
            } catch (SqliteException e) { 
                if (e.SqliteErrorCode == 14)
                {
                    throw new System.IO.FileNotFoundException(filePath);
                }
            }

            try
            {
                var cmd = new SqliteCommand()
                {
                    Connection = _connection,
                    CommandText = "SELECT * FROM tbl_header"
                };

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Airac_version = reader["current_airac"].ToString();
                    } else
                    {
                        throw new FormatException("The navigation data file has an incorrect format.");
                    }
                }
            } catch (Exception e)
            {
                throw new Exception("The navigation data file is invalid.", e);
            }
        }

        public override Localizer GetLocalizerFromAirportRunway(string airportIdentifier, string runwayIdentifier)
        {
            var foundLocs = GetObjectsWithQuery<Localizer>(LocalizerLookupByAirportRunway(airportIdentifier, runwayIdentifier), reader => LocalizerFactory.Factory(reader));

            if (foundLocs.Count < 1)
            {
                return null;
            }
            
            return foundLocs[0];
        }

        private SqliteCommand LocalizerLookupByAirportRunway(string airportIdentifier, string runwayIdentifier)
        {
            runwayIdentifier = "RW" + runwayIdentifier;

            var cmd = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = "SELECT * from tbl_localizers_glideslopes WHERE airport_identifier = @airportIdentifier AND runway_identifier = @runwayIdentifier"
            };

            cmd.Parameters.AddWithValue("@airportIdentifier", airportIdentifier);
            cmd.Parameters.AddWithValue("@runwayIdentifier", runwayIdentifier);

            return cmd;
        }

        private SqliteCommand AirportLookupByIdentifier(string identifier)
        {
            var cmd = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = "SELECT * FROM tbl_airports WHERE airport_identifier = @identifier",
            };

            cmd.Parameters.AddWithValue("@identifier", identifier);

            return cmd;
        }

        public override Airport GetAirportByIdentifier(string airportIdentifier)
        {
            var airports = GetObjectsWithQuery<Airport>(AirportLookupByIdentifier(airportIdentifier), reader => AirportFactory.Factory(reader));

            if (airports.Count == 1)
            {
                return airports[0];
            } else if (airports.Count > 1)
            {
                Console.Error.WriteLine($"Found two airport results for {airportIdentifier}. This should never happen!");
            }

            return null;
        }

        private SqliteCommand WaypointLookupByIdentifier(bool isTerminal, string identifier)
        {
            var table = isTerminal ? "tbl_terminal_waypoints" : "tbl_enroute_waypoints";

            var cmd = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = $"SELECT * FROM {table} WHERE waypoint_identifier = @identifier"
            };

            cmd.Parameters.AddWithValue("@identifier", identifier);

            return cmd;
        }

        public List<Waypoint> GetWaypointsByIdentifier(string identifier)
        {
            // We need to combine enroute + terminal waypoints
            
            List<Waypoint> waypoints = GetObjectsWithQuery<Waypoint>(WaypointLookupByIdentifier(true, identifier), reader => WaypointFactory.Factory(reader));
            foreach (var waypoint in GetObjectsWithQuery<Waypoint>(WaypointLookupByIdentifier(false, identifier), reader => WaypointFactory.Factory(reader)))
            {
                waypoints.Add(waypoint);
            }

            return waypoints;
        }

        private SqliteCommand VhfNavaidLookupByIdentifier(string identifier)
        {
            var cmd = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = "SELECT * from tbl_vhfnavaids WHERE vor_identifier = @identifier OR dme_ident = @identifier"
            };
            
            cmd.Parameters.AddWithValue("@identifier", identifier);

            return cmd;
        }

        public List<VhfNavaid> GetVhfNavaidsByIdentifier(string identifier)
        {
            List<VhfNavaid> navaids = GetObjectsWithQuery<VhfNavaid>(
                VhfNavaidLookupByIdentifier(identifier), 
                reader => VhfNavaidFactory.Factory(reader));

            return navaids;
        }

        public SqliteCommand NdbLookupByIdentifier(bool isTerminal, string identifier)
        {
            var table = isTerminal ? "tbl_terminal_ndbnavaids" : "tbl_enroute_ndbnavaids";

            var cmd = new SqliteCommand(){
                Connection = _connection,
                CommandText = $"SELECT * FROM {table} WHERE ndb_identifier = @identifier"
                };

            cmd.Parameters.AddWithValue("@identifier", identifier);

            return cmd;
        }

        public SqliteCommand AirportsFilterByDistance(GeoPoint position, Length radius)
        {
            radius = Length.FromMeters(Math.Min(radius.Meters, Length.FromNauticalMiles(100).Meters));

            // Figure out where our extremities are
            Longitude leftLon, rightLon;
            Latitude bottomLat, topLat;

            {
                var leftPoint = (GeoPoint)position.Clone();
                leftPoint.MoveBy(Bearing.FromDegrees(270), radius);
                leftLon = leftPoint.Lon;

                var rightPoint = (GeoPoint)position.Clone();
                rightPoint.MoveBy(Bearing.FromDegrees(90), radius);
                rightLon = rightPoint.Lon;

                var bottomPoint = (GeoPoint)position.Clone();
                bottomPoint.MoveBy(Bearing.FromDegrees(180), radius);
                bottomLat = bottomPoint.Lat;

                var topPoint = (GeoPoint)position.Clone();
                topPoint.MoveBy(Bearing.FromDegrees(360), radius);
                topLat = topPoint.Lat;
            }

            Latitude maxLat = Latitude.FromRadians(Math.Acos(radius.Meters / (GeoUtil.EARTH_RADIUS.Meters * Math.PI)));
            if (topLat > maxLat)
            {
                leftLon = Longitude.FromDegrees(-180);
                rightLon = Longitude.FromDegrees(180);
                topLat = Latitude.FromDegrees(90);
            }
            else if (bottomLat < new Latitude(-maxLat.Angle))
            {
                leftLon = Longitude.FromDegrees(-180);
                rightLon = Longitude.FromDegrees(180);
                bottomLat = Latitude.FromDegrees(-90);
            }

            if (rightLon <= leftLon)
            {
                // SELECT * from (airports) WHERE ((latitude) BETWEEN bottomlat AND topLAT) AND (longitude >= leftlon OR longitude <= right)
                var cmd = new SqliteCommand()
                {
                    Connection = _connection,
                    CommandText =
                        "SELECT * FROM tbl_airports WHERE (airport_ref_latitude BETWEEN @bottomLat AND @topLat) AND (airport_ref_longitude >= @leftLon OR airport_ref_longitude <= @rightLon)"
                };

                cmd.Parameters.AddWithValue("@bottomLat", bottomLat.Degrees);
                cmd.Parameters.AddWithValue("@topLat", topLat.Degrees);
                cmd.Parameters.AddWithValue("@leftLon", leftLon.Degrees);
                cmd.Parameters.AddWithValue("@rightLon", rightLon.Degrees);

                return cmd;
            }
            else
            {
                // SELECT * FROM (airports) WHERE ((latitude) BETWEEN bottomlat AND topLAT) AND (longitude) BETWEEN leftlon AND rightlon)
                var cmd = new SqliteCommand()
                {
                    Connection = _connection,
                    CommandText =
                        "SELECT * FROM tbl_airports WHERE (airport_ref_latitude BETWEEN @bottomLat AND @topLat) AND (airport_ref_longitude BETWEEN @leftLon AND @rightLon)"
                };

                cmd.Parameters.AddWithValue("@bottomLat", bottomLat.Degrees);
                cmd.Parameters.AddWithValue("@topLat", topLat.Degrees);
                cmd.Parameters.AddWithValue("@leftLon", leftLon.Degrees);
                cmd.Parameters.AddWithValue("@rightLon", rightLon.Degrees);

                return cmd;
            }
        }

        public List<Ndb> GetNdbsByIdentifier(string identifier)
        {
            // We need to combine enroute + terminal NDBs

            List<Ndb> ndbs = GetObjectsWithQuery<Ndb>(NdbLookupByIdentifier(true, identifier), reader => NdbFactory.Factory(reader));
            foreach (var ndb in GetObjectsWithQuery<Ndb>(NdbLookupByIdentifier(false, identifier), reader => NdbFactory.Factory(reader)))
            {
                ndbs.Add(ndb);
            }

            return ndbs;
        }

        public List<Navaid> GetNavaidsByIdentifier(string identifier)
        {
            // We get all VHF Navaids + all NDBs with this ident

            var navaids = new List<Navaid>();

            foreach (var vhfNavaid in GetVhfNavaidsByIdentifier(identifier))
            {
                navaids.Add(vhfNavaid);
            }

            foreach (var ndbNavaid in GetNdbsByIdentifier(identifier))
            {
                navaids.Add(ndbNavaid);
            }

            return navaids;
        }

        internal List<T> GetObjectsWithQuery<T>(SqliteCommand cmd, Func<SqliteDataReader, T> objectFactory)
        {
            var objects = new List<T>();

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var obj = objectFactory(reader);
                    objects.Add(obj);
                }
            }

            return objects;
        }

        public override List<Fix> GetFixesByIdentifier(string identifier)
        {
            List<Fix> foundFixes = new List<Fix>();

            foreach (var waypoint in this.GetWaypointsByIdentifier(identifier))
            {
                foundFixes.Add(waypoint);
            }

            foreach (var navaid in this.GetNavaidsByIdentifier(identifier))
            {
                foundFixes.Add(navaid);
            }

            var airport = GetAirportByIdentifier(identifier);

            if (airport != null)
            {
                foundFixes.Add(airport);
            }

            return foundFixes;
        }

        /// <summary>
        /// Gets the closest airport within a square.
        /// </summary>
        /// <param name="position">The centre of the square</param>
        /// <param name="radius">The distance from the centre to each side of the square</param>
        /// <returns>The closest airport within the square, or null if none found</returns>
        public override Airport GetClosestAirportWithinRadius(GeoPoint position, Length radius)
        {
            List<Airport> airports = GetObjectsWithQuery<Airport>(AirportsFilterByDistance(position, radius), reader => AirportFactory.Factory(reader));

            Airport closestAirport = null;
            double bestDistance = double.MaxValue;
            foreach (var airport in airports)
            {
                var distance = GeoPoint.Distance(airport.Location, position).Meters;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closestAirport = airport;
                }
            }
            return closestAirport;
        }

        private SqliteCommand RunwayLookupByAirportIdentifier(string airportIdentifier, string runwayIdentifier)
        {
            SqliteCommand command = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = $"SELECT * FROM tbl_runways WHERE airport_identifier == @airport AND runway_identifier == RW@runway"
            };

            command.Parameters.AddWithValue("@airport", airportIdentifier);
            command.Parameters.AddWithValue("@runway", runwayIdentifier);

            return command;
        }

        public override Runway GetRunwayFromAirportRunwayIdentifier(string airportIdentifier, string runwayIdentifier)
        {
            List<Runway> runways = GetObjectsWithQuery<Runway>(
                RunwayLookupByAirportIdentifier(airportIdentifier, runwayIdentifier),
                reader => RunwayFactory.Factory(reader));

            if (runways.Count == 0)
            {
                return null;
            }

            return runways[0];
        }

        private SqliteCommand AirwayLookupByIdentifier(string airwayIdentifier)
        {
            SqliteCommand command = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = "SELECT * FROM tbl_enroute_airways WHERE route_identifier == @airway"
            };

            command.Parameters.AddWithValue("@airway", airwayIdentifier);

            return command;
        }

        public override Airway GetAirwayFromIdentifierAndFixes(string airwayIdentifier, Fix startFix, Fix endFix)
        {
            var reader = AirwayLookupByIdentifier(airwayIdentifier).ExecuteReader();

            var airway = AirwayFactory.Factory(reader);

            airway.SelectSection(startFix, endFix);

            return airway;
        }

        public override bool IsValidAirwayIdentifier(string airwayIdentifier)
        {
            var reader = AirwayLookupByIdentifier(airwayIdentifier).ExecuteReader();

            return reader.HasRows;
        }

        private SqliteCommand SidLookupByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            SqliteCommand command = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = $"SELECT * FROM tbl_sids WHERE airport_identifier == @airport AND procedure_identifier = @sid"
            };

            command.Parameters.AddWithValue("@airport", airportIdentifier);
            command.Parameters.AddWithValue("@sid", sidIdentifier);

            return command;
        }

        public override Sid GetSidByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            var reader = SidLookupByAirportAndIdentifier(airportIdentifier, sidIdentifier).ExecuteReader();

            var sid = SidFactory.Factory(reader, _connection);

            return sid;
        }

        private SqliteCommand StarLookupByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            SqliteCommand command = new SqliteCommand()
            {
                Connection = _connection,
                CommandText = "SELECT * FROM tbl_stars WHERE airport_identifier == @airport AND procedure_identifier = @sid"
            };

            command.Parameters.AddWithValue("@airport", airportIdentifier);
            command.Parameters.AddWithValue("@sid", sidIdentifier);

            return command;
        }

        public override Star GetStarByAirportAndIdentifier(string airportIdentifier, string sidIdentifier)
        {
            var reader = StarLookupByAirportAndIdentifier(airportIdentifier, sidIdentifier).ExecuteReader();

            var sid = StarFactory.Factory(reader, _connection);

            return sid;
        }
    }
}