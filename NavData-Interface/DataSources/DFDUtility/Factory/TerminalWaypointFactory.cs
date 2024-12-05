using NavData_Interface.Objects.Fixes.Waypoints;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal static class TerminalWaypointFactory
    {
        public static Waypoint Factory(SqliteDataReader reader)
        {
            var waypoint = new Waypoint(
                    reader["waypoint_identifier"].ToString(),
                    reader["waypoint_name"].ToString(),
                    SQLHelper.locationFromColumns(reader, "waypoint_latitude", "waypoint_longitude"),
                    reader["area_code"].ToString(),
                    reader["icao_code"].ToString(),
                    reader["region_code"].ToString()
                );
            return waypoint;
        }
    }
}
