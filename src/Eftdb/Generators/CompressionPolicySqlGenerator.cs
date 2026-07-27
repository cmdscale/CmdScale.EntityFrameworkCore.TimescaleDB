using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public static class CompressionPolicySqlGenerator
    {
        public static List<string> Generate(AddCompressionPolicyOperation operation)
        {
            List<string> statements =
            [
                BuildAddCompressionPolicySql(
                    operation.TableName,
                    operation.Schema,
                    operation.After,
                    operation.CreatedBefore,
                    operation.ScheduleInterval,
                    operation.InitialStart,
                    operation.Timezone,
                    operation.IfNotExists)
            ];

            return statements;
        }

        public static List<string> Generate(AlterCompressionPolicyOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT remove_compression_policy({qualifiedTableName}, if_exists => true);",
                BuildAddCompressionPolicySql(
                    operation.TableName,
                    operation.Schema,
                    operation.After,
                    operation.CreatedBefore,
                    operation.ScheduleInterval,
                    operation.InitialStart,
                    operation.Timezone,
                    operation.IfNotExists)
            ];

            return statements;
        }

        public static List<string> Generate(DropCompressionPolicyOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT remove_compression_policy({qualifiedTableName}, if_exists => true);"
            ];
            return statements;
        }

        private static string BuildAddCompressionPolicySql(
            string tableName,
            string schema,
            string? after,
            string? createdBefore,
            string? scheduleInterval,
            DateTime? initialStart,
            string? timezone,
            bool? ifNotExists)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(tableName, schema);

            List<string> args = [qualifiedTableName];

            if (!string.IsNullOrWhiteSpace(after))
                args.Add($"compress_after => INTERVAL '{after}'");
            else if (!string.IsNullOrWhiteSpace(createdBefore))
                args.Add($"compress_created_before => INTERVAL '{createdBefore}'");

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

            return $"SELECT add_compression_policy({string.Join(", ", args)});";
        }
    }
}
