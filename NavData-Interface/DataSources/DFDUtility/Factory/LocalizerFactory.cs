using AviationCalcUtilNet.Units;
using AviationCalcUtilNet.Geo;
using NavData_Interface.Objects;
using System;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal static class LocalizerFactory
    {
        internal static Localizer Factory(SqliteDataReader reader)
        {
            var area_code = reader["area_code"].ToString();
            var icao_code = reader["icao_code"].ToString();
            var airport_identifier = reader["airport_identifier"].ToString();
            var runway_identifier = reader["runway_identifier"].ToString();
            var loc_identifier = reader["llz_identifier"].ToString();
            var loc_location = SQLHelper.locationFromColumns(reader, "llz_latitude", "llz_longitude");
            var loc_frequency = Double.Parse(reader["llz_frequency"].ToString());
            var loc_bearing = Bearing.FromDegrees(Double.Parse(reader["llz_bearing"].ToString()));
            var loc_width = Angle.FromDegrees(Double.Parse(reader["llz_width"].ToString()));
            var loc_category_raw = reader["ils_mls_gls_category"].ToString();

            var loc_category = IlsCategory.LocalizerOnly;

            switch (loc_category_raw)
            {
                case "0":
                    loc_category = IlsCategory.LocalizerOnly;
                    break;
                case "1":
                    loc_category = IlsCategory.CATI;
                    break;
                case "2":
                    loc_category = IlsCategory.CATII;
                    break;
                case "3":
                    loc_category = IlsCategory.CATIII;
                    break;
                case "I":
                    loc_category = IlsCategory.IGS;
                    break;
                case "L":
                case "A":
                    loc_category = IlsCategory.LDA;
                    break;
                case "S":
                case "F":
                    loc_category = IlsCategory.SDF;
                    break;
            }

            var station_declination = Double.Parse(reader["station_declination"].ToString());

            Glideslope glideslope = null;

            try
            {
                var gs_location = SQLHelper.locationFromColumns(reader, "gs_latitude", "gs_longitude");
                var gs_angle = Angle.FromDegrees(Double.Parse(reader["gs_angle"].ToString()));
                var gs_elevation = Length.FromFeet(Int32.Parse(reader["gs_elevation"].ToString()));

                glideslope = new Glideslope(
                    gs_location,
                    gs_angle,
                    gs_elevation);
            } catch (Exception ex)
            {
            }

            return new Localizer(
                area_code,
                icao_code,
                airport_identifier,
                runway_identifier,
                loc_identifier,
                loc_location,
                loc_frequency,
                loc_bearing,
                loc_width,
                loc_category,
                glideslope,
                station_declination
                );
        }
    }
}
