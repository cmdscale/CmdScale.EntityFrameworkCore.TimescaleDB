using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    internal class RetentionPolicySqlGenerator
    {
        private const string ProcName = "policy_retention";

        public static List<string> Generate(AddRetentionPolicyOperation operation)
        {
            List<string> statements =
            [
                BuildAddRetentionPolicySql(operation.TableName, operation.Schema, operation.DropAfter, operation.DropCreatedBefore, operation.InitialStart)
            ];

            List<string> jobClauses = PolicyJobSqlBuilder.BuildJobClauses(
                operation.ScheduleInterval, operation.MaxRuntime, operation.MaxRetries, operation.RetryPeriod);
            if (jobClauses.Count != 0)
            {
                statements.Add(PolicyJobSqlBuilder.BuildAlterJobSql(operation.TableName, operation.Schema, ProcName, jobClauses));
            }

            return statements;
        }

        public static List<string> Generate(AlterRetentionPolicyOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements = [];
            bool needsRecreation =
                operation.DropAfter != operation.OldDropAfter ||
                operation.DropCreatedBefore != operation.OldDropCreatedBefore ||
                operation.InitialStart != operation.OldInitialStart;

            if (needsRecreation)
            {
                statements.Add($"SELECT remove_retention_policy({qualifiedTableName}, if_exists => true);");
                statements.Add(BuildAddRetentionPolicySql(operation.TableName, operation.Schema, operation.DropAfter, operation.DropCreatedBefore, operation.InitialStart));

                // After recreation, reapply the full desired job configuration so existing settings are not lost.
                List<string> finalStateClauses = PolicyJobSqlBuilder.BuildJobClauses(
                    operation.ScheduleInterval, operation.MaxRuntime, operation.MaxRetries, operation.RetryPeriod);
                if (finalStateClauses.Count != 0)
                {
                    statements.Add(PolicyJobSqlBuilder.BuildAlterJobSql(operation.TableName, operation.Schema, ProcName, finalStateClauses));
                }
            }
            else
            {
                List<string> changedClauses = PolicyJobSqlBuilder.BuildChangedJobClauses(
                    operation.ScheduleInterval, operation.OldScheduleInterval,
                    operation.MaxRuntime, operation.OldMaxRuntime,
                    operation.MaxRetries, operation.OldMaxRetries,
                    operation.RetryPeriod, operation.OldRetryPeriod);
                if (changedClauses.Count != 0)
                {
                    statements.Add(PolicyJobSqlBuilder.BuildAlterJobSql(operation.TableName, operation.Schema, ProcName, changedClauses));
                }
            }

            return statements;
        }

        public static List<string> Generate(DropRetentionPolicyOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT remove_retention_policy({qualifiedTableName}, if_exists => true);"
            ];
            return statements;
        }

        private static string BuildAddRetentionPolicySql(string tableName, string schema, string? dropAfter, string? dropCreatedBefore, DateTime? initialStart)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(tableName, schema);

            List<string> args = [];

            if (!string.IsNullOrWhiteSpace(dropAfter))
                args.Add($"drop_after => {SqlBuilderHelper.IntervalOrBigint(dropAfter)}");
            else if (!string.IsNullOrWhiteSpace(dropCreatedBefore))
                args.Add($"drop_created_before => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(dropCreatedBefore)}'");

            if (initialStart.HasValue)
            {
                args.Add($"initial_start => '{SqlBuilderHelper.FormatTimestamp(initialStart.Value)}'");
            }

            return $"SELECT add_retention_policy({qualifiedTableName}, {string.Join(", ", args)});";
        }
    }
}
