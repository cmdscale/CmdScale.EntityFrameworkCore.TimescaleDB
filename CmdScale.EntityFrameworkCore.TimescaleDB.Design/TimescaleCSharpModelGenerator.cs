using CmdScale.EntityFrameworkCore.TimescaleDB.Annotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
using NpgsqlTypes;
using System.Data;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
#pragma warning disable EF1001

    public class TimescaleDatabaseModelFactory(IDiagnosticsLogger<DbLoggerCategory.Scaffolding> logger) : NpgsqlDatabaseModelFactory(logger)
    {
        private sealed record HypertableInfo(
            string TimeColumnName,
            string ChunkTimeInterval,
            bool CompressionEnabled,
            List<string> ChunkSkipColumns
        );

        public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
        {
            DatabaseModel databaseModel = base.Create(connection, options);

            // Query for TimescaleDB hypertables
            Dictionary<(string, string), HypertableInfo> hypertables = GetHypertables(connection);

            // Annotate the tables in the model
            foreach (DatabaseTable table in databaseModel.Tables)
            {
                if (table?.Schema != null && hypertables.TryGetValue((table.Schema, table.Name), out HypertableInfo? info))
                {
                    table[HypertableAnnotations.IsHypertable] = true;
                    table[HypertableAnnotations.HypertableTimeColumn] = info.TimeColumnName;
                    table[HypertableAnnotations.ChunkTimeInterval] = info.ChunkTimeInterval;
                    table[HypertableAnnotations.EnableCompression] = info.CompressionEnabled;

                    if (info.ChunkSkipColumns.Count > 0)
                    {
                        table[HypertableAnnotations.ChunkSkipColumns] = string.Join(",", info.ChunkSkipColumns);
                    }
                }
            }

            return databaseModel;
        }

        private static Dictionary<(string, string), HypertableInfo> GetHypertables(DbConnection connection)
        {
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                Dictionary<(string, string), HypertableInfo> hypertables = [];

                // Get main hypertable settings
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            h.hypertable_schema,
                            h.hypertable_name,
                            d.column_name AS time_column_name,
                            EXTRACT(EPOCH FROM d.time_interval) * 1000 AS chunk_time_interval_microseconds,
                            h.compression_enabled
                        FROM timescaledb_information.hypertables h
                        JOIN timescaledb_information.dimensions d
                          ON h.hypertable_schema = d.hypertable_schema AND h.hypertable_name = d.hypertable_name
                        WHERE d.dimension_type = 'Time';";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string timeColumn = reader.GetString(2);
                        long chunkTimeInterval = (long)reader.GetDouble(3);
                        bool compressionEnabled = reader.GetBoolean(4);

                        hypertables[(schema, name)] = new HypertableInfo(
                            TimeColumnName: timeColumn,
                            ChunkTimeInterval: chunkTimeInterval.ToString(),
                            CompressionEnabled: compressionEnabled,
                            ChunkSkipColumns: []
                        );
                    }
                }

                // Get chunk skipping columns and add them to our dictionary
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            h.schema_name,
                            h.table_name,
                            ccs.column_name
                        FROM _timescaledb_catalog.chunk_column_stats AS ccs
                        JOIN _timescaledb_catalog.hypertable AS h ON ccs.hypertable_id = h.id;";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string columnName = reader.GetString(2);

                        if (hypertables.TryGetValue((schema, name), out HypertableInfo? info))
                        {
                            info.ChunkSkipColumns.Add(columnName);
                        }
                    }
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
