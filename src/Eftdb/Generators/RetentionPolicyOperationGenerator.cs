using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class RetentionPolicyOperationGenerator
    {
        private readonly string quoteString = "\"";
        private readonly SqlBuilderHelper sqlHelper;

        public RetentionPolicyOperationGenerator(bool isDesignTime = false)
        {
            if (isDesignTime)
            {
                quoteString = "\"\"";
            }

            sqlHelper = new SqlBuilderHelper(quoteString);
        }

        public List<string> Generate(AddRetentionPolicyOperation operation)
        {
            List<string> statements =
            [
                BuildAddRetentionPolicySql(operation.TableName, operation.Schema, operation.DropAfter, operation.DropCreatedBefore, operation.InitialStart)
            ];

            // alter_job fails for drop_created_before retention policies.
            // TimescaleDB's policy_retention_check expects drop_after in the job config JSONB
            // but finds drop_created_before instead. Workaround: avoid alter_job for
            // drop_created_before policies, or recreate the policy entirely.
            // TODO: Remove this when a fix has been applied to TimescaleDB.
            if (string.IsNullOrEmpty(operation.DropAfter))
            {
                return statements;
            }

            List<string> alterJobClauses = BuildAlterJobClauses(operation);
            if (alterJobClauses.Count != 0)
            {
                statements.Add(BuildAlterJobSql(operation.TableName, operation.Schema, alterJobClauses));
            }

            return statements;
        }

        public List<string> Generate(AlterRetentionPolicyOperation operation)
        {
            string qualifiedTableName = sqlHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements = [];
            bool needsRecreation =
                operation.DropAfter != operation.OldDropAfter ||
                operation.DropCreatedBefore != operation.OldDropCreatedBefore ||
                operation.InitialStart != operation.OldInitialStart;

            if (needsRecreation)
            {
                statements.Add($"SELECT remove_retention_policy({qualifiedTableName}, if_exists => true);");
                statements.Add(BuildAddRetentionPolicySql(operation.TableName, operation.Schema, operation.DropAfter, operation.DropCreatedBefore, operation.InitialStart));

                // Create a temporary "add" operation representing the final desired state to ensure existing settings are reapplied.
                AddRetentionPolicyOperation finalStateOperation = new()
                {
                    TableName = operation.TableName,
                    Schema = operation.Schema,
                    DropAfter = operation.DropAfter,
                    DropCreatedBefore = operation.DropCreatedBefore,
                    InitialStart = operation.InitialStart,
                    ScheduleInterval = operation.ScheduleInterval,
                    MaxRuntime = operation.MaxRuntime,
                    MaxRetries = operation.MaxRetries,
                    RetryPeriod = operation.RetryPeriod
                };

                List<string> finalStateClauses = BuildAlterJobClauses(finalStateOperation);
                if (finalStateClauses.Count != 0)
                {
                    statements.Add(BuildAlterJobSql(operation.TableName, operation.Schema, finalStateClauses));
                }
            }
            else
            {
                List<string> changedClauses = BuildAlterJobClauses(operation);
                if (changedClauses.Count != 0)
                {
                    statements.Add(BuildAlterJobSql(operation.TableName, operation.Schema, changedClauses));
                }
            }

            return statements;
        }

        public List<string> Generate(DropRetentionPolicyOperation operation)
        {
            string qualifiedTableName = sqlHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT remove_retention_policy({qualifiedTableName}, if_exists => true);"
            ];
            return statements;
        }

        private static List<string> BuildAlterJobClauses(AddRetentionPolicyOperation operation)
        {
            List<string> clauses = [];

            if (!string.IsNullOrWhiteSpace(operation.ScheduleInterval))
                clauses.Add($"schedule_interval => INTERVAL '{operation.ScheduleInterval}'");

            if (!string.IsNullOrWhiteSpace(operation.MaxRuntime))
                clauses.Add($"max_runtime => INTERVAL '{operation.MaxRuntime}'");

            if (operation.MaxRetries != null)
                clauses.Add($"max_retries => {operation.MaxRetries}");

            if (!string.IsNullOrWhiteSpace(operation.RetryPeriod))
                clauses.Add($"retry_period => INTERVAL '{operation.RetryPeriod}'");

            return clauses;
        }

        private static List<string> BuildAlterJobClauses(AlterRetentionPolicyOperation operation)
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

        private static string BuildAlterJobSql(string tableName, string schema, IEnumerable<string> clauses)
        {
            // Note: hypertable_name is a varchar column, so it compares against a string literal, not a regclass.
            return $@"
                SELECT alter_job(job_id, {string.Join(", ", clauses)})
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = '{schema}' AND hypertable_name = '{tableName}';".Trim();
        }

        private string BuildAddRetentionPolicySql(string tableName, string schema, string? dropAfter, string? dropCreatedBefore, DateTime? initialStart)
        {
            string qualifiedTableName = sqlHelper.Regclass(tableName, schema);

            List<string> args = [];

            if (!string.IsNullOrWhiteSpace(dropAfter))
                args.Add($"drop_after => INTERVAL '{dropAfter}'");
            else if (!string.IsNullOrWhiteSpace(dropCreatedBefore))
                args.Add($"drop_created_before => INTERVAL '{dropCreatedBefore}'");

            if (initialStart.HasValue)
            {
                string timestamp = initialStart.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                args.Add($"initial_start => '{timestamp}'");
            }

            return $"SELECT add_retention_policy({qualifiedTableName}, {string.Join(", ", args)});";
        }
    }
}
