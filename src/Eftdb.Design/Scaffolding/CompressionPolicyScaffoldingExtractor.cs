using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts compression policy metadata from a TimescaleDB database for scaffolding.
    /// Handles both hypertable and continuous aggregate compression policies.
    /// </summary>
    public sealed class CompressionPolicyScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record CompressionPolicyInfo(
            string? After,
            string? CreatedBefore,
            DateTime? InitialStart,
            string? ScheduleInterval,
            string? Timezone,
            bool? IfNotExists
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
                Dictionary<(string, string), CompressionPolicyInfo> compressionPolicies = [];

                using (DbCommand command = connection.CreateCommand())
                {
                    // For hypertables the jobs view reports the user-facing schema/name directly.
                    // For continuous aggregates the jobs view reports the internal materialization
                    // hypertable (_timescaledb_internal._materialized_hypertable_N). The LEFT JOIN
                    // against _timescaledb_catalog.continuous_agg maps those back to the user-facing
                    // view schema/name so the result key matches the scaffolded CAgg entity.
                    command.CommandText = @"
                        SELECT
                            COALESCE(cagg.user_view_schema, j.hypertable_schema) AS effective_schema,
                            COALESCE(cagg.user_view_name,   j.hypertable_name)   AS effective_name,
                            j.config,
                            j.initial_start,
                            j.schedule_interval::text,
                            bgw.timezone
                        FROM timescaledb_information.jobs AS j
                        LEFT JOIN _timescaledb_config.bgw_job AS bgw ON bgw.id = j.job_id
                        LEFT JOIN _timescaledb_catalog.continuous_agg AS cagg
                            ON cagg.mat_hypertable_id = (
                                SELECT id
                                FROM _timescaledb_catalog.hypertable
                                WHERE schema_name = j.hypertable_schema
                                  AND table_name  = j.hypertable_name
                            )
                        WHERE j.proc_name IN ('policy_compression', 'policy_columnstore');";

                    using DbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string? configJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                        DateTime? initialStart = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        string? scheduleInterval = reader.IsDBNull(4) ? null : IntervalParsingHelper.NormalizeInterval(reader.GetString(4));
                        string? timezone = reader.IsDBNull(5) ? null : reader.GetString(5);

                        // Parse the JSONB config to extract compress_after or compress_created_before
                        string? after = null;
                        string? createdBefore = null;

                        if (!string.IsNullOrWhiteSpace(configJson))
                        {
                            using JsonDocument doc = JsonDocument.Parse(configJson);
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("compress_after", out JsonElement afterElement))
                            {
                                after = IntervalParsingHelper.ParseIntervalOrInteger(afterElement);
                            }

                            if (root.TryGetProperty("compress_created_before", out JsonElement createdBeforeElement))
                            {
                                createdBefore = IntervalParsingHelper.ParseIntervalOrInteger(createdBeforeElement);
                            }
                        }

                        // A compression policy must have either compress_after or compress_created_before
                        if (string.IsNullOrWhiteSpace(after) && string.IsNullOrWhiteSpace(createdBefore))
                        {
                            continue;
                        }

                        compressionPolicies[(schema, name)] = new CompressionPolicyInfo(
                            After: after,
                            CreatedBefore: createdBefore,
                            InitialStart: initialStart,
                            ScheduleInterval: scheduleInterval,
                            Timezone: timezone,
                            IfNotExists: null
                        );
                    }
                }

                // Convert to object dictionary to match interface
                return compressionPolicies.ToDictionary(
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
