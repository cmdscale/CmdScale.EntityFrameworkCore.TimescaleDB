using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class ReorderPolicyMigrationExtensions
    {
        public static OperationBuilder<AddReorderPolicyOperation> AddReorderPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string indexName,
            string? schema = null,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null)
        {
            AddReorderPolicyOperation operation = new()
            {
                TableName = tableName,
                IndexName = indexName,
                Schema = schema ?? string.Empty,
                InitialStart = initialStart,
                ScheduleInterval = scheduleInterval,
                MaxRuntime = maxRuntime,
                MaxRetries = maxRetries,
                RetryPeriod = retryPeriod,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AddReorderPolicyOperation>(operation);
        }

        public static OperationBuilder<AlterReorderPolicyOperation> AlterReorderPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string indexName,
            string? schema = null,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null,
            string? oldIndexName = null,
            DateTime? oldInitialStart = null,
            string? oldScheduleInterval = null,
            string? oldMaxRuntime = null,
            int? oldMaxRetries = null,
            string? oldRetryPeriod = null)
        {
            AlterReorderPolicyOperation operation = new()
            {
                TableName = tableName,
                IndexName = indexName,
                Schema = schema ?? string.Empty,
                InitialStart = initialStart,
                ScheduleInterval = scheduleInterval,
                MaxRuntime = maxRuntime,
                MaxRetries = maxRetries,
                RetryPeriod = retryPeriod,
                OldIndexName = oldIndexName ?? string.Empty,
                OldInitialStart = oldInitialStart,
                OldScheduleInterval = oldScheduleInterval,
                OldMaxRuntime = oldMaxRuntime,
                OldMaxRetries = oldMaxRetries,
                OldRetryPeriod = oldRetryPeriod,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AlterReorderPolicyOperation>(operation);
        }

        public static OperationBuilder<DropReorderPolicyOperation> DropReorderPolicy(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null)
        {
            DropReorderPolicyOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<DropReorderPolicyOperation>(operation);
        }
    }
}
