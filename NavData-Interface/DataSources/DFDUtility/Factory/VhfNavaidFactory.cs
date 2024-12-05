using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.Fixes.Navaids;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal class VhfNavaidFactory
    {
        internal static VhfNavaid Factory(SqliteDataReader reader)
        {
            var dme_location = SQLHelper.locationFromColumns(reader, "dme_latitude", "dme_longitude");

            dme_location.Alt = Length.FromFeet(Double.Parse(reader["dme_elevation"].ToString()));

            var navaid = new VhfNavaid
                (
                SQLHelper.locationFromColumns(reader, "vor_latitude", "vor_longitude"),
                reader["area_code"].ToString(),
                reader["airport_identifier"].ToString(),
                reader["icao_code"].ToString(),
                reader["vor_identifier"].ToString(),
                reader["vor_name"].ToString(),
                Double.Parse(reader["vor_frequency"].ToString()),
                reader["dme_ident"].ToString(),
                dme_location,
                Length.FromNauticalMiles(Int32.Parse(reader["range"].ToString()))
                );

            return navaid;
        }
    }
}
