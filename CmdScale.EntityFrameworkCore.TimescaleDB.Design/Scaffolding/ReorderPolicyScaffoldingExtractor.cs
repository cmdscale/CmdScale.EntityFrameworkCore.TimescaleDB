using System.Data;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts reorder policy metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    internal sealed class ReorderPolicyScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        internal sealed record ReorderPolicyInfo(
            string IndexName,
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

                // Convert to object dictionary to match interface
                return reorderPolicies.ToDictionary(
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
