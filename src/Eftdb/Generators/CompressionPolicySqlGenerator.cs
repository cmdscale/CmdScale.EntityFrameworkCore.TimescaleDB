using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    /// <summary>
    /// Generates <c>add_columnstore_policy</c>/<c>remove_columnstore_policy</c> SQL (or the legacy
    /// <c>add_compression_policy</c>/<c>remove_compression_policy</c> forms) for compression policy
    /// migration operations on hypertables and continuous aggregates.
    /// </summary>
    internal static class CompressionPolicySqlGenerator
    {
        private const string CommunityWarning = "Skipping Community Edition feature (compression policy) - not available in Apache Edition";

        public static List<string> Generate(AddCompressionPolicyOperation operation, bool useLegacyCompressionNames = false, bool isApacheEdition = false)
        {
            List<string> statements =
            [
                BuildAddPolicySql(
                    operation.TableName,
                    operation.Schema,
                    operation.After,
                    operation.CreatedBefore,
                    operation.ScheduleInterval,
                    operation.InitialStart,
                    operation.Timezone,
                    operation.IfNotExists,
                    useLegacyCompressionNames)
            ];

            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        public static List<string> Generate(AlterCompressionPolicyOperation operation, bool useLegacyCompressionNames = false, bool isApacheEdition = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                BuildRemovePolicySql(qualifiedTableName, useLegacyCompressionNames),
                BuildAddPolicySql(
                    operation.TableName,
                    operation.Schema,
                    operation.After,
                    operation.CreatedBefore,
                    operation.ScheduleInterval,
                    operation.InitialStart,
                    operation.Timezone,
                    operation.IfNotExists,
                    useLegacyCompressionNames)
            ];

            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        public static List<string> Generate(DropCompressionPolicyOperation operation, bool useLegacyCompressionNames = false, bool isApacheEdition = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                BuildRemovePolicySql(qualifiedTableName, useLegacyCompressionNames)
            ];
            return SqlBuilderHelper.SkipOnApacheEdition(statements, CommunityWarning, isApacheEdition);
        }

        private static string BuildRemovePolicySql(string qualifiedTableName, bool useLegacy)
        {
            if (useLegacy)
            {
                return $"SELECT remove_compression_policy({qualifiedTableName}, if_exists => true);";
            }

            return $"CALL remove_columnstore_policy({qualifiedTableName}, if_exists => true);";
        }

        private static string BuildAddPolicySql(
            string tableName,
            string schema,
            string? after,
            string? createdBefore,
            string? scheduleInterval,
            DateTime? initialStart,
            string? timezone,
            bool? ifNotExists,
            bool useLegacy)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(tableName, schema);

            List<string> args = [qualifiedTableName];

            if (useLegacy)
            {
                if (!string.IsNullOrWhiteSpace(after))
                    args.Add($"compress_after => {SqlBuilderHelper.IntervalOrBigint(after)}");
                else if (!string.IsNullOrWhiteSpace(createdBefore))
                    args.Add($"compress_created_before => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(createdBefore)}'");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(after))
                    args.Add($"after => {SqlBuilderHelper.IntervalOrBigint(after)}");
                else if (!string.IsNullOrWhiteSpace(createdBefore))
                    args.Add($"created_before => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(createdBefore)}'");
            }

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                args.Add($"schedule_interval => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(scheduleInterval)}'");

            if (initialStart.HasValue)
            {
                args.Add($"initial_start => '{SqlBuilderHelper.FormatTimestamp(initialStart.Value)}'");
            }

            if (!string.IsNullOrWhiteSpace(timezone))
                args.Add($"timezone => '{SqlBuilderHelper.EscapeStringLiteral(timezone)}'");

            if (ifNotExists == true)
                args.Add("if_not_exists => true");

            if (useLegacy)
            {
                return $"SELECT add_compression_policy({string.Join(", ", args)});";
            }

            return $"CALL add_columnstore_policy({string.Join(", ", args)});";
        }
    }
}
