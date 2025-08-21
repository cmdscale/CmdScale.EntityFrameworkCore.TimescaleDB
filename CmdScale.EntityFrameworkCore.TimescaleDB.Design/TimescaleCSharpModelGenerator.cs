using CmdScale.EntityFrameworkCore.TimescaleDB.Annotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
#pragma warning disable EF1001

    public class TimescaleDatabaseModelFactory(IDiagnosticsLogger<DbLoggerCategory.Scaffolding> logger) : NpgsqlDatabaseModelFactory(logger)
    {
        public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
        {
            DatabaseModel databaseModel = base.Create(connection, options);

            // Query for TimescaleDB hypertables
            Dictionary<(string, string), string> hypertables = GetHypertables(connection);

            // Annotate the tables in the model
            foreach (DatabaseTable table in databaseModel.Tables)
            {
                if (table?.Schema != null && hypertables.TryGetValue((table.Schema, table.Name), out string? timeColumn))
                {
                    table[HypertableAnnotations.IsHypertable] = true;
                    table[HypertableAnnotations.HypertableTimeColumn] = timeColumn;
                }
            }

            return databaseModel;
        }

        private static Dictionary<(string, string), string> GetHypertables(DbConnection connection)
        {
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                Dictionary<(string, string), string> hypertables = [];

                using DbCommand command = connection.CreateCommand();
                command.CommandText = @"
                        SELECT hypertable_schema, hypertable_name, primary_dimension
                        FROM timescaledb_information.hypertables;";

                using DbDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string schema = reader.GetString(0);
                    string name = reader.GetString(1);
                    string timeColumn = reader.GetString(2);
                    hypertables[(schema, name)] = timeColumn;
                }

                return hypertables;
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }
    }
#pragma warning restore EF1001
}
