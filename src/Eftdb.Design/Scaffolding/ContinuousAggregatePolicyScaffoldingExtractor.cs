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
            string? Timezone,
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
                    // The config column contains JSONB with start_offset, end_offset, and other policy parameters
                    command.CommandText = @"
                        SELECT
                            ca.view_schema,
                            ca.view_name,
                            j.config,
                            j.schedule_interval::text,
                            j.initial_start,
                            j.timezone
                        FROM timescaledb_information.jobs j
                        INNER JOIN timescaledb_information.continuous_aggregates ca
                            ON j.hypertable_schema = ca.materialization_hypertable_schema
                            AND j.hypertable_name = ca.materialization_hypertable_name
                        WHERE j.proc_name = 'policy_refresh_continuous_aggregate';";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string viewSchema = reader.GetString(0);
                        string viewName = reader.GetString(1);
                        string? configJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                        string? scheduleInterval = reader.IsDBNull(3) ? null : reader.GetString(3);
                        DateTime? initialStart = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                        string? timezone = reader.IsDBNull(5) ? null : reader.GetString(5);

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
                                startOffset = ParseIntervalOrInteger(startOffsetElement);
                            }

                            // Extract end_offset
                            if (root.TryGetProperty("end_offset", out JsonElement endOffsetElement))
                            {
                                endOffset = ParseIntervalOrInteger(endOffsetElement);
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
                            Timezone: timezone,
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

        /// <summary>
        /// Parses an interval or integer value from JSONB.
        /// TimescaleDB stores intervals as strings (e.g., "1 mon", "7 days")
        /// or integers for integer-based time columns.
        /// </summary>
        private static string? ParseIntervalOrInteger(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString() ?? string.Empty;
                // TimescaleDB stores intervals in PostgreSQL format (e.g., "1 mon", "7 days", "01:00:00")
                // We need to normalize these to a format that matches what users would write
                return NormalizeInterval(value);
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                // Integer-based time column
                return element.GetInt64().ToString();
            }

            return null;
        }

        /// <summary>
        /// Normalizes PostgreSQL interval format to user-friendly format.
        /// </summary>
        /// <remarks>
        /// PostgreSQL stores intervals in formats like:
        /// - "1 mon" for 1 month
        /// - "7 days" for 7 days
        /// - "01:00:00" for 1 hour
        /// We normalize these to match the format users would use in Fluent API:
        /// - "1 month"
        /// - "7 days"
        /// - "1 hour"
        /// </remarks>
        private static string NormalizeInterval(string pgInterval)
        {
            if (string.IsNullOrWhiteSpace(pgInterval))
            {
                return pgInterval;
            }

            string normalized = pgInterval.Trim();

            // Replace "mon" with "month"
            normalized = normalized.Replace(" mon", " month");

            // Convert time-only intervals (HH:MM:SS) to hour/minute format
            if (TimeSpan.TryParse(normalized, out TimeSpan timeSpan))
            {
                if (timeSpan.TotalMinutes < 60 && timeSpan.Minutes > 0 && timeSpan.Hours == 0)
                {
                    return $"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}";
                }
                if (timeSpan.TotalHours < 24 && timeSpan.Hours > 0)
                {
                    return $"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")}";
                }
                // For days, use the total days
                if (timeSpan.Days > 0)
                {
                    return $"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")}";
                }
            }

            return normalized;
        }
    }
}
