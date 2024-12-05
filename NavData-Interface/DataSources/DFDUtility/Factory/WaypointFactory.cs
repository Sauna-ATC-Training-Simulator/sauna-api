using NavData_Interface.Objects.Fixes.Waypoints;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal static class WaypointFactory
    {
        public static Waypoint Factory(SqliteDataReader reader)
        {
            var waypoint = new Waypoint(
                    reader["waypoint_identifier"].ToString(),
                    reader["waypoint_name"]?.ToString() ?? reader["waypoint_identifier"].ToString(),
                    SQLHelper.locationFromColumns(reader),
                    reader["area_code"].ToString(),
                    reader["icao_code"].ToString()
                );
            return waypoint;
        }
    }
}
