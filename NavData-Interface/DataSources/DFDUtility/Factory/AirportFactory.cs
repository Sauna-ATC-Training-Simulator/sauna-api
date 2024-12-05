using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.Fixes;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal static class AirportFactory
    {
        internal static Airport Factory(SqliteDataReader reader)
        {
            var longest_runway_surface = RunwaySurfaceCode.Undefined;
            
            switch (reader["longest_runway_surface_code"])
            {
                case "H":
                    longest_runway_surface = RunwaySurfaceCode.Hard;
                    break;
                case "S":
                    longest_runway_surface = RunwaySurfaceCode.Soft;
                    break;
                case "W":
                    longest_runway_surface = RunwaySurfaceCode.Water;
                    break;
                case "U":
                    longest_runway_surface = RunwaySurfaceCode.Undefined;
                    break;
            }

            var elevation_raw = reader["elevation"].ToString();
            var transition_altitude_raw = reader["transition_altitude"].ToString();
            var transition_level_raw = reader["transition_level"].ToString();
            var speed_limit_raw = reader["speed_limit"].ToString();
            var speed_limit_altitude_raw = reader["speed_limit_altitude"].ToString();

            var elevation = elevation_raw == "" ? Length.FromFeet(0) : Length.FromFeet(Int32.Parse(elevation_raw));
            var transition_altitude = transition_altitude_raw == "" ? Length.FromFeet(0) : Length.FromFeet(Int32.Parse(transition_altitude_raw));
            var transition_level = transition_level_raw == "" ? Length.FromFeet(0) : Length.FromFeet(Int32.Parse(transition_level_raw));
            var speed_limit = speed_limit_raw == "" ? Velocity.FromKnots(0) : Velocity.FromKnots(Int32.Parse(speed_limit_raw));
            var speed_limit_altitude = speed_limit_altitude_raw == "" ? Length.FromFeet(0) : Length.FromFeet(Int32.Parse(speed_limit_altitude_raw));

            return new Airport(
                reader["airport_identifier"].ToString(),
                SQLHelper.locationFromColumns(reader, "airport_ref_latitude", "airport_ref_longitude"),
                reader["area_code"].ToString()?? "",
                reader["icao_code"].ToString()?? "",
                reader["airport_identifier_3letter"].ToString()?? "",
                reader["airport_name"].ToString()?? "",
                reader["ifr_capability"].ToString() == "Y",
                longest_runway_surface,
                elevation,
                transition_altitude,
                transition_level,
                speed_limit,
                speed_limit_altitude,
                reader["iata_ata_designator"].ToString()?? ""
                );
        }
    }
}
