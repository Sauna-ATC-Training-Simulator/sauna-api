using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.Fixes;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal static class RunwayFactory
    {
        internal static Runway Factory(SqliteDataReader reader)
        {
            GeoPoint location = SQLHelper.locationFromColumns(reader, "runway_latitude", "runway_longitude");

            var airportIdentifier = reader["airport_identifier"].ToString();
            var runwayIdentifier = reader["runway_identifier"].ToString().TrimStart('R', 'W');
            // TODO: 
            // var gradient = Angle.FromPercentage(Double.Parse(reader["runway_gradient"].ToString()));
            var magBearing = Bearing.FromDegrees(Double.Parse(reader["runway_magnetic_bearing"].ToString()));
            var truBearing = Bearing.FromDegrees(Double.Parse(reader["runway_true_bearing"].ToString()));
            var thrElevation = Length.FromFeet(Int32.Parse(reader["landing_threshold_elevation"].ToString()));
            var thrLength = Length.FromFeet(Int32.Parse(reader["displaced_threshold_distance"].ToString()));
            var thrCrossingHeight = Length.FromFeet(Int32.Parse(reader["threshold_crossing_height"].ToString()));
            var length = Length.FromFeet(Int32.Parse(reader["runway_length"].ToString()));
            var width = Length.FromFeet(Int32.Parse(reader["runway_width"].ToString()));

            return new Runway(
                runwayIdentifier,
                location,
                airportIdentifier,
                Angle.FromDegrees(0),
                // gradient,
                magBearing,
                truBearing,
                thrElevation,
                thrLength,
                thrCrossingHeight,
                length,
                width
                );
        }
    }
}
