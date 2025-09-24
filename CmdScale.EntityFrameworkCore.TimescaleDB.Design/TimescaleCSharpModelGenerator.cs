using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
#pragma warning disable EF1001

    public class TimescaleDatabaseModelFactory(IDiagnosticsLogger<DbLoggerCategory.Scaffolding> logger) : NpgsqlDatabaseModelFactory(logger)
    {
        private sealed record HypertableInfo(
            string TimeColumnName,
            string ChunkTimeInterval,
            bool CompressionEnabled,
            List<string> ChunkSkipColumns,
            List<Dimension> AdditionalDimensions
        );

        private sealed record ReorderPolicyInfo(
            string IndexName, 
            DateTime? InitialStart,
            string? ScheduleInterval,
            string? MaxRuntime,
            int? MaxRetries,
            string? RetryPeriod
        );

        public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
        {
            DatabaseModel databaseModel = base.Create(connection, options);
            Dictionary<(string, string), HypertableInfo> hypertables = GetHypertables(connection);
            Dictionary<(string, string), ReorderPolicyInfo> reorderPolicies = GetReorderPolicies(connection);

            // Annotate the tables in the model
            foreach (DatabaseTable table in databaseModel.Tables)
            {
                if (table?.Schema == null) continue;

                (string Schema, string Name) tableKey = (table.Schema, table.Name);

                // Annotations for Hypertables
                if (hypertables.TryGetValue(tableKey, out HypertableInfo? info))
                {
                    table[HypertableAnnotations.IsHypertable] = true;
                    table[HypertableAnnotations.HypertableTimeColumn] = info.TimeColumnName;
                    table[HypertableAnnotations.ChunkTimeInterval] = info.ChunkTimeInterval;
                    table[HypertableAnnotations.EnableCompression] = info.CompressionEnabled;

                    if (info.ChunkSkipColumns.Count > 0)
                    {
                        table[HypertableAnnotations.ChunkSkipColumns] = string.Join(",", info.ChunkSkipColumns);
                    }

                    if (info.AdditionalDimensions.Count > 0)
                    {
                        table[HypertableAnnotations.AdditionalDimensions] = JsonSerializer.Serialize(info.AdditionalDimensions);
                    }
                }

                // Annotate for Reorder Policies
                if (reorderPolicies.TryGetValue(tableKey, out ReorderPolicyInfo? policyInfo))
                {
                    table[ReorderPolicyAnnotations.HasReorderPolicy] = true;
                    table[ReorderPolicyAnnotations.IndexName] = policyInfo.IndexName;

                    if (policyInfo.InitialStart.HasValue)
                    {
                        table[ReorderPolicyAnnotations.InitialStart] = policyInfo.InitialStart.Value;
                    }

                    // Set annotations only if they differ from TimescaleDB defaults
                    if (policyInfo.ScheduleInterval != DefaultValues.ReorderPolicyScheduleInterval)
                    {
                        table[ReorderPolicyAnnotations.ScheduleInterval] = policyInfo.ScheduleInterval;
                    }

                    if (policyInfo.MaxRuntime != DefaultValues.ReorderPolicyMaxRuntime)
                    {
                        table[ReorderPolicyAnnotations.MaxRuntime] = policyInfo.MaxRuntime;
                    }

                    if (policyInfo.MaxRetries != DefaultValues.ReorderPolicyMaxRetries)
                    {
                        table[ReorderPolicyAnnotations.MaxRetries] = policyInfo.MaxRetries;
                    }

                    if (policyInfo.RetryPeriod != DefaultValues.ReorderPolicyRetryPeriod)
                    {
                        table[ReorderPolicyAnnotations.RetryPeriod] = policyInfo.RetryPeriod;
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
                Dictionary<(string, string), bool> compressionSettings = [];

                // Get compression settings for hypertables
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT hypertable_schema, hypertable_name, compression_enabled FROM timescaledb_information.hypertables;";
                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        compressionSettings[(reader.GetString(0), reader.GetString(1))] = reader.GetBoolean(2);
                    }
                }


                // Get main hypertable settings
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            hypertable_schema,
                            hypertable_name,
                            column_name,
                            dimension_number,
                            num_partitions,
                            EXTRACT(EPOCH FROM time_interval) * 1000 AS time_interval_microseconds
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
                            // long chunkTimeInterval = (long)reader.GetDouble(5);
                            long chunkInterval = reader.IsDBNull(5) ? DefaultValues.ChunkTimeIntervalLong : (long)reader.GetDouble(5);
                            bool compressionEnabled = compressionSettings.TryGetValue(key, out bool enabled) && enabled;

                            hypertables[key] = new HypertableInfo(
                                TimeColumnName: columnName,
                                ChunkTimeInterval: chunkInterval.ToString(),
                                CompressionEnabled: compressionEnabled,
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
                                    // Space dimension
                                    dimension = Dimension.CreateHash(columnName, reader.GetInt32(4));
                                }
                                else if (!reader.IsDBNull(5))
                                {
                                    // Time dimension
                                    long interval = (long)reader.GetDouble(5);
                                    dimension = Dimension.CreateRange(columnName, interval.ToString());
                                }
                                else continue;

                                info.AdditionalDimensions.Add(dimension);
                            }
                        }
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

        private static Dictionary<(string, string), ReorderPolicyInfo> GetReorderPolicies(DbConnection connection)
        {
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                Dictionary<(string, string), ReorderPolicyInfo> reorderPolicies = [];
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            j.hypertable_schema,
                            j.hypertable_name,
                            j.config ->> 'index_name' AS index_name,
                            j.initial_start,
                            j.schedule_interval::text,
                            j.max_runtime::text,
                            j.max_retries,
                            j.retry_period::text
                        FROM timescaledb_information.jobs AS j
                        WHERE j.proc_name = 'policy_reorder';";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string indexName = reader.GetString(2);
                        DateTime? initialStart = reader.IsDBNull(3) ? null : reader.GetDateTime(3);

                        string? scheduleInterval = reader.IsDBNull(4) ? null : reader.GetString(4);
                        string? maxRuntime = reader.IsDBNull(5) ? null : reader.GetString(5);
                        int? maxRetries = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        string? retryPeriod = reader.IsDBNull(7) ? null : reader.GetString(7);

                        if (!string.IsNullOrEmpty(indexName))
                        {
                            reorderPolicies[(schema, name)] = new ReorderPolicyInfo(
                                indexName,
                                initialStart,
                                scheduleInterval,
                                maxRuntime,
                                maxRetries,
                                retryPeriod
                            );
                        }
                    }
                }
                return reorderPolicies;
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
