using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    internal static class ReorderPolicySqlGenerator
    {
        private const string ProcName = "policy_reorder";
        private const string CommunityWarning = "Skipping Community Edition feature (reorder policy) - not available in Apache Edition";

        public static List<string> Generate(AddReorderPolicyOperation operation, bool isApacheEdition = false)
        {
            List<string> statements =
            [
                BuildAddReorderPolicySql(operation.TableName, operation.Schema, operation.IndexName, operation.InitialStart)
            ];

            List<string> jobClauses = PolicyJobSqlBuilder.BuildJobClauses(
                operation.ScheduleInterval, operation.MaxRuntime, operation.MaxRetries, operation.RetryPeriod);
            if (jobClauses.Count != 0)
            {
                statements.Add(PolicyJobSqlBuilder.BuildAlterJobSql(operation.TableName, operation.Schema, ProcName, jobClauses));
            }

            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        public static List<string> Generate(AlterReorderPolicyOperation operation, bool isApacheEdition = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements = [];
            bool needsRecreation = operation.IndexName != operation.OldIndexName || operation.InitialStart != operation.OldInitialStart;

            if (needsRecreation)
            {
                statements.Add($"SELECT remove_reorder_policy({qualifiedTableName}, if_exists => true);");
                statements.Add(BuildAddReorderPolicySql(operation.TableName, operation.Schema, operation.IndexName, operation.InitialStart));

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

            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        public static List<string> Generate(DropReorderPolicyOperation operation, bool isApacheEdition = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT remove_reorder_policy({qualifiedTableName}, if_exists => true);"
            ];
            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        private static string BuildAddReorderPolicySql(string tableName, string schema, string indexName, DateTime? initialStart)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(tableName, schema);

            string baseSql = $"SELECT add_reorder_policy({qualifiedTableName}, '{SqlBuilderHelper.EscapeStringLiteral(indexName)}'";

            List<string> optionalArgs = [];

            // Add optional arguments if they are provided
            if (initialStart.HasValue)
            {
                optionalArgs.Add($"initial_start => '{SqlBuilderHelper.FormatTimestamp(initialStart.Value)}'");
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
