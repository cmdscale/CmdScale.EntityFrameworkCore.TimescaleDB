using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class RetentionPolicyMigrationExtensions
    {
        public static OperationBuilder<AddRetentionPolicyOperation> AddRetentionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null,
            string? dropAfter = null,
            string? dropCreatedBefore = null,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null)
        {
            AddRetentionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
                DropAfter = dropAfter,
                DropCreatedBefore = dropCreatedBefore,
                InitialStart = initialStart,
                ScheduleInterval = scheduleInterval,
                MaxRuntime = maxRuntime,
                MaxRetries = maxRetries,
                RetryPeriod = retryPeriod,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AddRetentionPolicyOperation>(operation);
        }

        public static OperationBuilder<AlterRetentionPolicyOperation> AlterRetentionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null,
            string? dropAfter = null,
            string? dropCreatedBefore = null,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null,
            string? oldDropAfter = null,
            string? oldDropCreatedBefore = null,
            DateTime? oldInitialStart = null,
            string? oldScheduleInterval = null,
            string? oldMaxRuntime = null,
            int? oldMaxRetries = null,
            string? oldRetryPeriod = null)
        {
            AlterRetentionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
                DropAfter = dropAfter,
                DropCreatedBefore = dropCreatedBefore,
                InitialStart = initialStart,
                ScheduleInterval = scheduleInterval,
                MaxRuntime = maxRuntime,
                MaxRetries = maxRetries,
                RetryPeriod = retryPeriod,
                OldDropAfter = oldDropAfter,
                OldDropCreatedBefore = oldDropCreatedBefore,
                OldInitialStart = oldInitialStart,
                OldScheduleInterval = oldScheduleInterval,
                OldMaxRuntime = oldMaxRuntime,
                OldMaxRetries = oldMaxRetries,
                OldRetryPeriod = oldRetryPeriod,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AlterRetentionPolicyOperation>(operation);
        }

        public static OperationBuilder<DropRetentionPolicyOperation> DropRetentionPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null)
        {
            DropRetentionPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<DropRetentionPolicyOperation>(operation);
        }
    }
}
