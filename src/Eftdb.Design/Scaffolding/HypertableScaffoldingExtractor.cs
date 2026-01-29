using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using System.Data;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts hypertable metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    public sealed class HypertableScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record HypertableInfo(
            string TimeColumnName,
            string ChunkTimeInterval,
            bool CompressionEnabled,
            List<string> CompressionSegmentBy,
            List<string> CompressionOrderBy,
            List<string> ChunkSkipColumns,
            List<Dimension> AdditionalDimensions
        );

        public Dictionary<(string Schema, string TableName), object> Extract(DbConnection connection)
        {
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                Dictionary<(string, string), HypertableInfo> hypertables = [];
                Dictionary<(string, string), bool> compressionSettings = GetCompressionSettings(connection);

                GetHypertableSettings(connection, hypertables, compressionSettings);
                GetChunkSkipColumns(connection, hypertables);
                GetCompressionConfiguration(connection, hypertables);

                // Convert to object dictionary to match interface
                return hypertables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value
                );
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }

        private static Dictionary<(string, string), bool> GetCompressionSettings(DbConnection connection)
        {
            Dictionary<(string, string), bool> compressionSettings = [];
            using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT hypertable_schema, hypertable_name, compression_enabled FROM timescaledb_information.hypertables;";
            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                compressionSettings[(reader.GetString(0), reader.GetString(1))] = reader.GetBoolean(2);
            }
            return compressionSettings;
        }

        private static void GetHypertableSettings(
            DbConnection connection,
            Dictionary<(string, string), HypertableInfo> hypertables,
            Dictionary<(string, string), bool> compressionSettings)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    hypertable_schema,
                    hypertable_name,
                    column_name,
                    dimension_number,
                    num_partitions,
                    EXTRACT(EPOCH FROM time_interval) * 1000 AS time_interval_microseconds,
                    integer_interval
                FROM timescaledb_information.dimensions
                ORDER BY hypertable_schema, hypertable_name, dimension_number;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);
                string columnName = reader.GetString(2);
                int dimensionNumber = reader.GetInt32(3);

                (string schema, string name) key = (schema, name);

                // If it's the first dimension, it defines the primary hypertable settings
                if (dimensionNumber == 1)
                {
                    long chunkInterval = reader.IsDBNull(5) ? DefaultValues.ChunkTimeIntervalLong : (long)reader.GetDouble(5);
                    bool compressionEnabled = compressionSettings.TryGetValue(key, out bool enabled) && enabled;

                    hypertables[key] = new HypertableInfo(
                        TimeColumnName: columnName,
                        ChunkTimeInterval: chunkInterval.ToString(),
                        CompressionEnabled: compressionEnabled,
                        CompressionSegmentBy: [],
                        CompressionOrderBy: [],
                        ChunkSkipColumns: [],
                        AdditionalDimensions: []
                    );
                }
                // For all other dimensions, add them to the AdditionalDimensions list
                else
                {
                    if (hypertables.TryGetValue(key, out HypertableInfo? info))
                    {
                        Dimension dimension;

                        if (!reader.IsDBNull(4) && reader.GetInt32(4) > 0)
                        {
                            // Hash dimension (space partitioning)
                            dimension = Dimension.CreateHash(columnName, reader.GetInt32(4));
                        }
                        else if (!reader.IsDBNull(5))
                        {
                            // Time-based range dimension
                            long interval = (long)reader.GetDouble(5);
                            dimension = Dimension.CreateRange(columnName, interval.ToString());
                        }
                        else if (!reader.IsDBNull(6))
                        {
                            // Integer-based range dimension
                            long integerInterval = reader.GetInt64(6);
                            dimension = Dimension.CreateRange(columnName, integerInterval.ToString());
                        }
                        else continue;

                        info.AdditionalDimensions.Add(dimension);
                    }
                }
            }
        }

        private static void GetChunkSkipColumns(DbConnection connection, Dictionary<(string, string), HypertableInfo> hypertables)
        {
            using DbCommand command = connection.CreateCommand();
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

        private static void GetCompressionConfiguration(DbConnection connection, Dictionary<(string, string), HypertableInfo> hypertables)
        {
            using DbCommand command = connection.CreateCommand();

            // This view provides the column-level details for compression.
            // segmentby_column_index is not null for segment columns.
            // orderby_column_index is not null for order columns.
            command.CommandText = @"
                SELECT 
                    hypertable_schema, 
                    hypertable_name, 
                    attname, 
                    segmentby_column_index, 
                    orderby_column_index, 
                    orderby_asc, 
                    orderby_nullsfirst 
                FROM timescaledb_information.compression_settings 
                ORDER BY hypertable_schema, hypertable_name, segmentby_column_index, orderby_column_index;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);
                string columnName = reader.GetString(2);

                // Find the corresponding hypertable info
                if (!hypertables.TryGetValue((schema, name), out HypertableInfo? info))
                {
                    continue;
                }

                // Handle SegmentBy
                if (!reader.IsDBNull(3)) // segmentby_column_index
                {
                    info.CompressionSegmentBy.Add(columnName);
                }

                // Handle OrderBy
                if (!reader.IsDBNull(4)) // orderby_column_index
                {
                    bool isAscending = reader.GetBoolean(5);
                    bool isNullsFirst = reader.GetBoolean(6);

                    string direction = isAscending ? "ASC" : "DESC";
                    string nulls = isNullsFirst ? "NULLS FIRST" : "NULLS LAST";

                    // Reconstruct the full string format: "colName DESC NULLS LAST"
                    info.CompressionOrderBy.Add($"{columnName} {direction} {nulls}");
                }
            }
        }
    }
}
