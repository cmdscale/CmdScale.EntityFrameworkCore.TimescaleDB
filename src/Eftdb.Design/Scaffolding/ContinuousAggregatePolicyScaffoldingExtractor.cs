using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts continuous aggregate policy metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    public sealed class ContinuousAggregatePolicyScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record ContinuousAggregatePolicyInfo(
            string? StartOffset,
            string? EndOffset,
            string? ScheduleInterval,
            DateTime? InitialStart,
            bool? IncludeTieredData,
            int? BucketsPerBatch,
            int? MaxBatchesPerExecution,
            bool? RefreshNewestFirst
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
                Dictionary<(string, string), ContinuousAggregatePolicyInfo> policies = [];

                using (DbCommand command = connection.CreateCommand())
                {
                    // Query continuous aggregate policies from TimescaleDB jobs table
                    command.CommandText = @"
                        SELECT
                            ca.user_view_schema,
                            ca.user_view_name,
                            j.config,
                            j.schedule_interval::text,
                            j.initial_start
                        FROM timescaledb_information.jobs j
                        INNER JOIN _timescaledb_catalog.continuous_agg ca
                            ON (j.config->>'mat_hypertable_id')::integer = ca.mat_hypertable_id
                        WHERE j.proc_name = 'policy_refresh_continuous_aggregate';";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string viewSchema = reader.GetString(0);
                        string viewName = reader.GetString(1);
                        string? configJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                        string? scheduleInterval = reader.IsDBNull(3) ? null : reader.GetString(3);
                        DateTime? initialStart = reader.IsDBNull(4) ? null : reader.GetDateTime(4);

                        // Parse the JSONB config to extract policy parameters
                        string? startOffset = null;
                        string? endOffset = null;
                        bool? includeTieredData = null;
                        int? bucketsPerBatch = null;
                        int? maxBatchesPerExecution = null;
                        bool? refreshNewestFirst = null;

                        if (!string.IsNullOrWhiteSpace(configJson))
                        {
                            using JsonDocument doc = JsonDocument.Parse(configJson);
                            JsonElement root = doc.RootElement;

                            // Extract start_offset
                            if (root.TryGetProperty("start_offset", out JsonElement startOffsetElement))
                            {
                                startOffset = IntervalParsingHelper.ParseIntervalOrInteger(startOffsetElement);
                            }

                            // Extract end_offset
                            if (root.TryGetProperty("end_offset", out JsonElement endOffsetElement))
                            {
                                endOffset = IntervalParsingHelper.ParseIntervalOrInteger(endOffsetElement);
                            }

                            // Extract include_tiered_data (optional)
                            if (root.TryGetProperty("include_tiered_data", out JsonElement includeTieredDataElement)
                                && (includeTieredDataElement.ValueKind == JsonValueKind.True || includeTieredDataElement.ValueKind == JsonValueKind.False))
                            {
                                includeTieredData = includeTieredDataElement.GetBoolean();
                            }

                            // Extract buckets_per_batch (optional, defaults to 1)
                            if (root.TryGetProperty("buckets_per_batch", out JsonElement bucketsPerBatchElement)
                                && bucketsPerBatchElement.ValueKind == JsonValueKind.Number)
                            {
                                bucketsPerBatch = bucketsPerBatchElement.GetInt32();
                            }

                            // Extract max_batches_per_execution (optional, defaults to 0)
                            if (root.TryGetProperty("max_batches_per_execution", out JsonElement maxBatchesElement)
                                && maxBatchesElement.ValueKind == JsonValueKind.Number)
                            {
                                maxBatchesPerExecution = maxBatchesElement.GetInt32();
                            }

                            // Extract refresh_newest_first (optional, defaults to true)
                            if (root.TryGetProperty("refresh_newest_first", out JsonElement refreshNewestFirstElement)
                                && (refreshNewestFirstElement.ValueKind == JsonValueKind.True || refreshNewestFirstElement.ValueKind == JsonValueKind.False))
                            {
                                refreshNewestFirst = refreshNewestFirstElement.GetBoolean();
                            }
                        }

                        policies[(viewSchema, viewName)] = new ContinuousAggregatePolicyInfo(
                            StartOffset: startOffset,
                            EndOffset: endOffset,
                            ScheduleInterval: scheduleInterval,
                            InitialStart: initialStart,
                            IncludeTieredData: includeTieredData,
                            BucketsPerBatch: bucketsPerBatch,
                            MaxBatchesPerExecution: maxBatchesPerExecution,
                            RefreshNewestFirst: refreshNewestFirst
                        );
                    }
                }

                // Convert to object dictionary to match interface
                return policies.ToDictionary(
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
    }
}
