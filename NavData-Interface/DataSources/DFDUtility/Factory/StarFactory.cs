using NavData_Interface.Objects.LegCollections.Procedures;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NavData_Interface.DataSources.DFDUtility.Factory
{
    internal class StarFactory : TerminalProcedureFactory
    {
        public static Star Factory(SqliteDataReader reader, SqliteConnection connection)
        {
            var factory = new StarFactory(reader, connection);

            try
            {
                var data = factory.GatherData();
                return new Star(data.airportIdentifier, data.routeIdentifier, data.firstTransitions, data.commonLegs, data.secondTransitions, data.transitionAltitude);
            } catch (Exception ex)
            {
                return null;
            }
            
        }

        private StarFactory(SqliteDataReader reader, SqliteConnection connection) : base(reader, connection) { }
    }
}
