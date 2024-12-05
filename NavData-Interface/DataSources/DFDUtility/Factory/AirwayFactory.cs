using NavData_Interface.Objects.Fixes.Waypoints;
using NavData_Interface.Objects.LegCollections.Airways;
using NavData_Interface.Objects.LegCollections.Legs;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    public class AirwayFactory
    {
        internal static Airway Factory(SqliteDataReader reader)
        {
            List<AirwayPoint> points = new List<AirwayPoint>();

            while (reader.Read())
            {
                Waypoint waypoint = new Waypoint(
                    reader["waypoint_identifier"].ToString(),
                    SQLHelper.locationFromColumns(
                        reader,
                        "waypoint_latitude",
                        "waypoint_longitude"));

                WaypointDescription description = SQLHelper.waypointDescriptionFromDescriptionString(reader["waypoint_description_code"].ToString());

                points.Add(new AirwayPoint(waypoint, description));
            }

            Airway airway;

            try
            {
                airway = new Airway(points);
            } catch (Exception e)
            {
                return null;
            }

            return airway;
        }
    }
}
