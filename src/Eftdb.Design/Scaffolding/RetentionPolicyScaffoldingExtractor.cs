using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts retention policy metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    public sealed class RetentionPolicyScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record RetentionPolicyInfo(
            string? DropAfter,
            string? DropCreatedBefore,
            DateTime? InitialStart,
            string? ScheduleInterval,
            string? MaxRuntime,
            int? MaxRetries,
            string? RetryPeriod
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
                Dictionary<(string, string), RetentionPolicyInfo> retentionPolicies = [];

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            j.hypertable_schema,
                            j.hypertable_name,
                            j.config,
                            j.initial_start,
                            j.schedule_interval::text,
                            j.max_runtime::text,
                            j.max_retries,
                            j.retry_period::text
                        FROM timescaledb_information.jobs AS j
                        WHERE j.proc_name = 'policy_retention';";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string? configJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                        DateTime? initialStart = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        string? scheduleInterval = reader.IsDBNull(4) ? null : reader.GetString(4);
                        string? maxRuntime = reader.IsDBNull(5) ? null : reader.GetString(5);
                        int? maxRetries = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        string? retryPeriod = reader.IsDBNull(7) ? null : reader.GetString(7);

                        // Parse the JSONB config to extract drop_after or drop_created_before
                        string? dropAfter = null;
                        string? dropCreatedBefore = null;

                        if (!string.IsNullOrWhiteSpace(configJson))
                        {
                            using JsonDocument doc = JsonDocument.Parse(configJson);
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("drop_after", out JsonElement dropAfterElement))
                            {
                                dropAfter = IntervalParsingHelper.ParseIntervalOrInteger(dropAfterElement);
                            }

                            if (root.TryGetProperty("drop_created_before", out JsonElement dropCreatedBeforeElement))
                            {
                                dropCreatedBefore = IntervalParsingHelper.ParseIntervalOrInteger(dropCreatedBeforeElement);
                            }
                        }

                        // A retention policy must have either drop_after or drop_created_before
                        if (string.IsNullOrWhiteSpace(dropAfter) && string.IsNullOrWhiteSpace(dropCreatedBefore))
                        {
                            continue;
                        }

                        retentionPolicies[(schema, name)] = new RetentionPolicyInfo(
                            DropAfter: dropAfter,
                            DropCreatedBefore: dropCreatedBefore,
                            InitialStart: initialStart,
                            ScheduleInterval: scheduleInterval,
                            MaxRuntime: maxRuntime,
                            MaxRetries: maxRetries,
                            RetryPeriod: retryPeriod
                        );
                    }
                }

                // Convert to object dictionary to match interface
                return retentionPolicies.ToDictionary(
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
