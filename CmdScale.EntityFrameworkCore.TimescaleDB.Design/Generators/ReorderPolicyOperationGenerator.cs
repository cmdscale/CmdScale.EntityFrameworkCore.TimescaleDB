using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    public class ReorderPolicyOperationGenerator
    {
        public static void Generate(AddReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                BuildAddReorderPolicySql(operation.TableName, operation.IndexName, operation.InitialStart)
            ];

            List<string> alterJobClauses = BuildAlterJobClauses(operation);
            if (alterJobClauses.Count != 0)
            {
                statements.Add(BuildAlterJobSql(operation.TableName, alterJobClauses));
            }

            MigrationBuilderSqlHelper.BuildQueryString(statements, builder);
        }

        public static void Generate(AlterReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements = [];
            bool needsRecreation = operation.IndexName != operation.OldIndexName || operation.InitialStart != operation.OldInitialStart;

            if (needsRecreation)
            {
                statements.Add($"SELECT remove_reorder_policy('\"\"{operation.TableName}\"\"', if_exists => true);");
                statements.Add(BuildAddReorderPolicySql(operation.TableName, operation.IndexName, operation.InitialStart));

                // Create a temporary "add" operation representing the final desired state to ensure existing settings are reapplied.
                AddReorderPolicyOperation finalStateOperation = new()
                {
                    TableName = operation.TableName,
                    IndexName = operation.IndexName,
                    InitialStart = operation.InitialStart,
                    ScheduleInterval = operation.ScheduleInterval,
                    MaxRuntime = operation.MaxRuntime,
                    MaxRetries = operation.MaxRetries,
                    RetryPeriod = operation.RetryPeriod
                };

                List<string> finalStateClauses = BuildAlterJobClauses(finalStateOperation);
                if (finalStateClauses.Count != 0)
                {
                    statements.Add(BuildAlterJobSql(operation.TableName, finalStateClauses));
                }
            }
            else
            {
                List<string> changedClauses = BuildAlterJobClauses(operation);
                if (changedClauses.Count != 0)
                {
                    statements.Add(BuildAlterJobSql(operation.TableName, changedClauses));
                }
            }

            MigrationBuilderSqlHelper.BuildQueryString(statements, builder);
        }

        public static void Generate(DropReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                $"SELECT remove_reorder_policy('\"\"{operation.TableName}\"\"', if_exists => true);"
            ];
            MigrationBuilderSqlHelper.BuildQueryString(statements, builder);
        }

        private static List<string> BuildAlterJobClauses(AddReorderPolicyOperation operation)
        {
            List<string> clauses = [];
            if (!string.IsNullOrWhiteSpace(operation.ScheduleInterval) && operation.ScheduleInterval != DefaultValues.ReorderPolicyScheduleInterval)
                clauses.Add($"schedule_interval => INTERVAL '{operation.ScheduleInterval}'");

            if (!string.IsNullOrWhiteSpace(operation.MaxRuntime) && operation.MaxRuntime != DefaultValues.ReorderPolicyMaxRuntime)
                clauses.Add($"max_runtime => INTERVAL '{operation.MaxRuntime}'");

            if (operation.MaxRetries != null && operation.MaxRetries != DefaultValues.ReorderPolicyMaxRetries)
                clauses.Add($"max_retries => {operation.MaxRetries}");

            if (!string.IsNullOrWhiteSpace(operation.RetryPeriod) && operation.RetryPeriod != DefaultValues.ReorderPolicyRetryPeriod)
                clauses.Add($"retry_period => INTERVAL '{operation.RetryPeriod}'");

            return clauses;
        }

        private static List<string> BuildAlterJobClauses(AlterReorderPolicyOperation operation)
        {
            List<string> clauses = [];

            if (!string.IsNullOrWhiteSpace(operation.ScheduleInterval) && operation.ScheduleInterval != operation.OldScheduleInterval)
                clauses.Add($"schedule_interval => INTERVAL '{operation.ScheduleInterval}'");

            if (!string.IsNullOrWhiteSpace(operation.MaxRuntime) && operation.MaxRuntime != operation.OldMaxRuntime)
            {
                string maxRuntimeValue = string.IsNullOrWhiteSpace(operation.MaxRuntime) ? "NULL" : $"INTERVAL '{operation.MaxRuntime}'";
                clauses.Add($"max_runtime => {maxRuntimeValue}");
            }

            if (operation.MaxRetries != null && operation.MaxRetries != operation.OldMaxRetries)
                clauses.Add($"max_retries => {operation.MaxRetries}");

            if (!string.IsNullOrWhiteSpace(operation.RetryPeriod) && operation.RetryPeriod != operation.OldRetryPeriod)
                clauses.Add($"retry_period => INTERVAL '{operation.RetryPeriod}'");

            return clauses;
        }

        private static string BuildAlterJobSql(string tableName, IEnumerable<string> clauses)
        {
            return $@"
                SELECT alter_job(job_id, {string.Join(", ", clauses)})
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_name = '{tableName}';".Trim();
            }

        private static string BuildAddReorderPolicySql(string tableName, string indexName, DateTime? initialStart)
        {
            string baseSql = $"SELECT add_reorder_policy('\"\"{tableName}\"\"', '{indexName}'";

            List<string> optionalArgs = [];

            // Add optional arguments if they are provided
            if (initialStart.HasValue)
            {
                // Use ISO 8601 format for timestamps to avoid ambiguity
                string timestamp = initialStart.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                optionalArgs.Add($"initial_start => '{timestamp}'");
            }

            if (optionalArgs.Count > 0)
            {
                baseSql += $", {string.Join(", ", optionalArgs)}";
            }

            baseSql += ");";
            return baseSql;
        }
    }
}
