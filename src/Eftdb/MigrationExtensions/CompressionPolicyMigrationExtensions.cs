using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class CompressionPolicyMigrationExtensions
    {
        public static OperationBuilder<AddCompressionPolicyOperation> AddCompressionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null,
            string? after = null,
            string? createdBefore = null,
            string? scheduleInterval = null,
            DateTime? initialStart = null,
            string? timezone = null,
            bool? ifNotExists = null)
        {
            AddCompressionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
                After = after,
                CreatedBefore = createdBefore,
                ScheduleInterval = scheduleInterval,
                InitialStart = initialStart,
                Timezone = timezone,
                IfNotExists = ifNotExists,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AddCompressionPolicyOperation>(operation);
        }

        public static OperationBuilder<AlterCompressionPolicyOperation> AlterCompressionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null,
            string? after = null,
            string? createdBefore = null,
            string? scheduleInterval = null,
            DateTime? initialStart = null,
            string? timezone = null,
            bool? ifNotExists = null,
            string? oldAfter = null,
            string? oldCreatedBefore = null,
            string? oldScheduleInterval = null,
            DateTime? oldInitialStart = null,
            string? oldTimezone = null,
            bool? oldIfNotExists = null)
        {
            AlterCompressionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
                After = after,
                CreatedBefore = createdBefore,
                ScheduleInterval = scheduleInterval,
                InitialStart = initialStart,
                Timezone = timezone,
                IfNotExists = ifNotExists,
                OldAfter = oldAfter,
                OldCreatedBefore = oldCreatedBefore,
                OldScheduleInterval = oldScheduleInterval,
                OldInitialStart = oldInitialStart,
                OldTimezone = oldTimezone,
                OldIfNotExists = oldIfNotExists,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AlterCompressionPolicyOperation>(operation);
        }

        public static OperationBuilder<DropCompressionPolicyOperation> DropCompressionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null)
        {
            DropCompressionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<DropCompressionPolicyOperation>(operation);
        }
    }
}
