using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class ContinuousAggregatePolicyMigrationExtensions
    {
        public static OperationBuilder<AddContinuousAggregatePolicyOperation> AddContinuousAggregatePolicy(
            this MigrationBuilder migrationBuilder,
            string materializedViewName,
            string? schema = null,
            string? startOffset = null,
            string? endOffset = null,
            string? scheduleInterval = null,
            DateTime? initialStart = null,
            bool ifNotExists = false,
            bool? includeTieredData = null,
            int bucketsPerBatch = 1,
            int maxBatchesPerExecution = 0,
            bool refreshNewestFirst = true)
        {
            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = materializedViewName,
                Schema = schema ?? string.Empty,
                StartOffset = startOffset,
                EndOffset = endOffset,
                ScheduleInterval = scheduleInterval,
                InitialStart = initialStart,
                IfNotExists = ifNotExists,
                IncludeTieredData = includeTieredData,
                BucketsPerBatch = bucketsPerBatch,
                MaxBatchesPerExecution = maxBatchesPerExecution,
                RefreshNewestFirst = refreshNewestFirst,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AddContinuousAggregatePolicyOperation>(operation);
        }

        public static OperationBuilder<RemoveContinuousAggregatePolicyOperation> RemoveContinuousAggregatePolicy(
            this MigrationBuilder migrationBuilder,
            string materializedViewName,
            string? schema = null,
            bool ifExists = false)
        {
            RemoveContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = materializedViewName,
                Schema = schema ?? string.Empty,
                IfExists = ifExists,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<RemoveContinuousAggregatePolicyOperation>(operation);
        }
    }
}
