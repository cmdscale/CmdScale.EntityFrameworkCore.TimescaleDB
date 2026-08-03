using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public static class CompressionPolicySqlGenerator
    {
        public static List<string> Generate(AddCompressionPolicyOperation operation, bool useLegacyCompressionNames = false)
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

            return statements;
        }

        public static List<string> Generate(AlterCompressionPolicyOperation operation, bool useLegacyCompressionNames = false)
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

            return statements;
        }

        public static List<string> Generate(DropCompressionPolicyOperation operation, bool useLegacyCompressionNames = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                BuildRemovePolicySql(qualifiedTableName, useLegacyCompressionNames)
            ];
            return statements;
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
                    args.Add($"compress_after => INTERVAL '{after}'");
                else if (!string.IsNullOrWhiteSpace(createdBefore))
                    args.Add($"compress_created_before => INTERVAL '{createdBefore}'");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(after))
                    args.Add($"after => INTERVAL '{after}'");
                else if (!string.IsNullOrWhiteSpace(createdBefore))
                    args.Add($"created_before => INTERVAL '{createdBefore}'");
            }

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                args.Add($"schedule_interval => INTERVAL '{scheduleInterval}'");

            if (initialStart.HasValue)
            {
                // Use ISO 8601 format for timestamps to avoid ambiguity.
                string timestamp = initialStart.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                args.Add($"initial_start => '{timestamp}'");
            }

            if (!string.IsNullOrWhiteSpace(timezone))
                args.Add($"timezone => '{timezone}'");

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
