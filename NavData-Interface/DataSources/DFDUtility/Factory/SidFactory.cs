using AviationCalcUtilNet.Units;
using NavData_Interface.Objects.LegCollections.Procedures;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal class SidFactory : TerminalProcedureFactory
    {
        public static Sid Factory(SqliteDataReader reader, SqliteConnection connection)
        {
            var factory = new SidFactory(reader, connection);
            try
            {
                var data = factory.GatherData();
                return new Sid(data.airportIdentifier, data.routeIdentifier, data.firstTransitions, data.commonLegs, data.secondTransitions, data.transitionAltitude);
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        private SidFactory(SqliteDataReader reader, SqliteConnection connection) : base(reader, connection) { }
    }
}
