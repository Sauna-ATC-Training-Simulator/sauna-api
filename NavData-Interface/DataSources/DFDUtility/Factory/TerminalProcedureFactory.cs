using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface.Objects;
using NavData_Interface.Objects.Fixes;
using NavData_Interface.Objects.Fixes.Navaids;
using NavData_Interface.Objects.Fixes.Waypoints;
using NavData_Interface.Objects.LegCollections.Legs;
using NavData_Interface.Objects.LegCollections.Procedures;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal abstract class TerminalProcedureFactory
    {
        protected List<Transition> _firstTransitions = new List<Transition>();
        protected List<Transition> _secondTransitions = new List<Transition>();
        protected List<Leg> _commonLegs = new List<Leg>();
        protected SqliteDataReader _reader = null;
        protected SqliteConnection _connection = null;
        Length _transitionAltitude = null;

        public TerminalProcedureFactory(SqliteDataReader reader, SqliteConnection connection)
        {
            _reader = reader;
            _connection = connection;
        }

        protected void handleRow()
        {
            switch (_reader["route_type"].ToString())
            {
                case "4":
                    // This leg is for a runway transition. Read the whole thing
                    {
                        var transitionIdentifier = _reader["transition_identifier"].ToString();
                        var transition = ReadTransition(transitionIdentifier);
                        _firstTransitions.Add(transition);
                        return;
                    }
                case "5":
                    // This leg is for the common portion. Read just this leg and add it
                    if (_firstTransitions.Count == 0 && _reader["transition_altitude"].ToString() != "")
                    {
                        // When there are no runway transitions, the first leg of the common part will have
                        // A non-null transition_altitude, which is the transition altitude for this STAR.
                        _transitionAltitude = Length.FromFeet(Int32.Parse(_reader["transition_altitude"].ToString()));
                    }
                    _commonLegs.Add(ReadLeg());
                    //_reader.Read();
                    return;
                case "6":
                    {
                        var transitionIdentifier = _reader["transition_identifier"].ToString();
                        var transition = ReadTransition(transitionIdentifier);
                        _secondTransitions.Add(transition);
                        return;
                    }

                // The following are possible entries
                // They correspond to rwyTransition, common route and transition
                // For other types of STARs
                // We handle them the same way
                case "1":
                    goto case "4";
                case "2":
                    goto case "5";
                case "3":
                    goto case "6";
                case "7":
                    goto case "4";
                case "8":
                    goto case "5";
                case "9":
                    goto case "6";
                case "F":
                    goto case "4";
                case "M":
                    goto case "5";
                case "S":
                    goto case "6";
            }
        }

        protected (string airportIdentifier, string routeIdentifier, List<Transition> firstTransitions, List<Leg> commonLegs, List<Transition> secondTransitions, Length transitionAltitude) GatherData()
        { 
            // if you're here cause there's an infinite loop in this function
            // try _reader.read() instead of HasRows
            // idk documentation is kinda unclear
            _reader.Read();

            var airportIdentifier = _reader["airport_identifier"].ToString();
            var routeIdentifier = _reader["procedure_identifier"].ToString();

            do
            {
                handleRow();
            } while (_reader.Read());

            _reader.Close();

            // Make sure we don't have stale references here
            var returnFirstTransitions = _firstTransitions;
            _firstTransitions = null;
            var returnSecondTransitions = _secondTransitions;
            _secondTransitions = null;
            var returnCommonLegs = _commonLegs;
            _commonLegs = null;
            
            _reader = null;
            _connection = null;

            return (airportIdentifier, routeIdentifier, returnFirstTransitions, returnCommonLegs, returnSecondTransitions, _transitionAltitude);
        }

        protected Transition ReadTransition(string transitionIdentifier)
        {
            // The transition altitude for this SID is always on the first leg of each transition. Store it now
            // This COULD be null if it's determined by ATC
            Length transitionAltitude = null;

            if (_reader["transition_altitude"].GetType() != typeof(DBNull))
            {
                transitionAltitude = Length.FromFeet((long)_reader["transition_altitude"]);
            }

            var legs = new List<Leg>();

            do
            {
                legs.Add(ReadLeg());
            } while (_reader.Read() && _reader["transition_identifier"].ToString() == transitionIdentifier);

            return new Transition(legs, transitionIdentifier, transitionAltitude);
        }

        protected Leg ReadLeg()
        {
            var waypointIdentifier = _reader["waypoint_identifier"].ToString();

            Waypoint waypoint = null;
            WaypointDescription waypointDescription = null;

            if (waypointIdentifier != "")
            {
                waypoint = new Waypoint(waypointIdentifier, SQLHelper.locationFromColumns(_reader));
                waypointDescription = SQLHelper.waypointDescriptionFromDescriptionString(_reader["waypoint_description_code"].ToString());
            }

            RequiredTurnDirectionType? turnDirection = null;

            if (_reader["turn_direction"] != null)
            {
                // The turn is more than 90 degrees. Possibilities are L, R and E
                // E just means either direction is OK. In that case we keep turnDirection null.
                if (_reader["turn_direction"].ToString() == "L")
                {
                    turnDirection = RequiredTurnDirectionType.LEFT;
                } else if (_reader["turn_direction"].ToString() == "R") 
                {
                    turnDirection = RequiredTurnDirectionType.RIGHT;
                }
            }

            var legType = Leg.parseLegType(_reader["path_termination"].ToString());

            Navaid recommendedNavaid = null;
            Bearing theta = null;

            if (_reader["recommanded_navaid"].ToString() != "")
            {
                try
                {
                    // Get the recommended navaid from the DB
                    // The id is of the format tablename|id
                    var fullId = _reader["recommanded_id"].ToString().Split('|');

                    var table = fullId[0];
                    var id = fullId[1];

                    var cmd = new SqliteCommand()
                    {
                        Connection = _connection,
                        CommandText = $"SELECT * FROM {table} WHERE id = @id"
                    };

                    cmd.Parameters.AddWithValue("@id", id);

                    // Depending on the table, we need to choose the right factory
                    if (table == "tbl_vhfnavaids")
                    {
                        recommendedNavaid = VhfNavaidFactory.Factory(cmd.ExecuteReader());
                    }
                    else if (table == "tbl_terminal_ndbnavaids")
                    {
                        recommendedNavaid = TerminalNdbFactory.Factory(cmd.ExecuteReader());
                    }
                    else if (table == "tbl_enroute_ndbnavaids")
                    {
                        recommendedNavaid = NdbFactory.Factory(cmd.ExecuteReader());
                    }
                } catch (Exception e)
                {
                    // Probably doesn't have ids.
                    var navaidIdentifier = _reader["recommanded_navaid"].ToString();
                    GeoPoint location = SQLHelper.locationFromColumns(_reader, "recommanded_navaid_latitude", "recommanded_navaid_longitude");

                    recommendedNavaid = new VhfNavaid(location, "", "", "", navaidIdentifier, navaidIdentifier, 199.998, "", null, Length.FromNauticalMiles(200));
                } finally
                {
                    if (_reader["theta"].GetType() != typeof(DBNull))
                    {
                        theta = Bearing.FromDegrees((double)_reader["theta"]);
                    }
                }
            }

            Length arcRadius = null;

            if (_reader["arc_radius"].GetType() != typeof(DBNull))
            {
                arcRadius = Length.FromNauticalMiles((double)_reader["arc_radius"]);
            }

            var magneticCourseRaw = _reader["magnetic_course"].ToString();

            Bearing outBoundMagneticCourse = null;

            if (magneticCourseRaw != "")
            {
                outBoundMagneticCourse = Bearing.FromDegrees(Double.Parse(magneticCourseRaw));
            }

            var altRestrictionType = _reader["altitude_description"]?.ToString();

            Length upperAlt = null;
            Length lowerAlt = null;

            int altitude1 = 0, altitude2 = 0;

            if (_reader["altitude1"].ToString() != "")
            {

                if (_reader["altitude1"].ToString().StartsWith("FL") == true)
                {
                    altitude1 = 100 * Int32.Parse(_reader["altitude1"].ToString().Substring(2, 3));
                }
                else
                {
                    altitude1 = Int32.Parse(_reader["altitude1"].ToString());
                }
            }

            if (_reader["altitude2"].ToString() != "")
            {
                
                if (_reader["altitude2"].ToString().StartsWith("FL") == true)
                {
                    altitude2 = 100 * Int32.Parse(_reader["altitude2"].ToString().Substring(2, 3));
                } else
                {
                    altitude2 = Int32.Parse(_reader["altitude2"].ToString());
                }
            }

            if (altRestrictionType == "+")
            {
                // At or above. The altitude given is the lower restriction.
                lowerAlt = Length.FromFeet(altitude1);
            } else if (altRestrictionType == "-")
            {
                // At or below. The altitude given is the upper restriction.
                upperAlt = Length.FromFeet(altitude1);
            } else if (String.IsNullOrEmpty(altRestrictionType) && !String.IsNullOrEmpty(_reader["altitude1"].ToString()))
            {
                // If the restrictionType is null, and we have an altitude, it's an 'at' restriction
                upperAlt = Length.FromFeet(altitude1);
                lowerAlt = Length.FromFeet(altitude1);
            } else if (altRestrictionType == "B")
            {
                // 'Between' restriction. Altitude1 is always the upper and Altitude2 is always the lower.
                upperAlt = Length.FromFeet(altitude1);
                lowerAlt = Length.FromFeet(altitude2);
            } else if (altRestrictionType == "C")
            {
                // 'Termination at or above'. Altitude2 has the lower altitude. Used for 'X to altitude' legs.
                // Seemingly never actually used in any procedure??? We'll handle it anyway.
                upperAlt = Length.FromFeet(altitude2);
            } 

            SpeedRestrictionType? speedRestrictionType = null;
            var speedRestrictionTypeRaw = _reader["speed_limit_description"].ToString();

            Velocity speedLimit = null;

            if (_reader["speed_limit"].ToString() != "")
            {
                speedLimit = Velocity.FromKnots(Int32.Parse(_reader["speed_limit"].ToString()));
            }

            if (speedRestrictionTypeRaw == "" && _reader["speed_limit"].ToString() != "")
            {
                speedRestrictionType = SpeedRestrictionType.AT;
            } else if (speedRestrictionTypeRaw == "+")
            {
                speedRestrictionType = SpeedRestrictionType.ABOVE;
            } else if (speedRestrictionTypeRaw == "-")
            {
                speedRestrictionType = SpeedRestrictionType.BELOW;
            }

            Fix centerFix = null;

            var centerWaypointIdent = _reader["center_waypoint"].ToString();
            
            if (centerWaypointIdent != "")
            {
                GeoPoint centerWaypointLocation = SQLHelper.locationFromColumns(_reader, "center_waypoint_latitude", "center_waypoint_longitude");

                centerFix = new Waypoint(centerWaypointIdent, centerWaypointLocation);
            }

            double? legLength = null;
            HoldLegLengthTypeEnum? legLengthType = null;

            var legLengthRaw = _reader["route_distance_holding_distance_time"].ToString();
            var legLengthTypeRaw = _reader["distance_time"].ToString();


            if (legLengthRaw != "")
            {
                legLength = Double.Parse(legLengthRaw);
                
                if (legLengthTypeRaw == "D")
                {
                    legLengthType = HoldLegLengthTypeEnum.DISTANCE;
                } else
                {
                    legLengthType = HoldLegLengthTypeEnum.TIME;
                }
            }

            return new Leg(
                legType,
                speedLimit,
                speedRestrictionType,
                lowerAlt,
                upperAlt,
                waypoint,
                waypointDescription,
                centerFix,
                recommendedNavaid,
                theta,
                outBoundMagneticCourse,
                turnDirection,
                arcRadius,
                legLengthType,
                legLength
                );
        }
    }
}
