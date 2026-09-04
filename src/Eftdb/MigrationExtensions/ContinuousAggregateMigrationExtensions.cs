using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class ContinuousAggregateMigrationExtensions
    {
        public static OperationBuilder<CreateContinuousAggregateOperation> CreateContinuousAggregate(
            this MigrationBuilder migrationBuilder,
            string materializedViewName,
            string parentName,
            string? schema = null,
            string? chunkInterval = null,
            bool withNoData = false,
            bool createGroupIndexes = false,
            bool materializedOnly = false,
            string? timeBucketWidth = null,
            string? timeBucketSourceColumn = null,
            string timeBucketColumnName = "time_bucket",
            bool timeBucketGroupBy = true,
            IReadOnlyList<ContinuousAggregateFunction>? aggregateFunctions = null,
            IReadOnlyList<string>? groupByColumns = null,
            string? whereClause = null,
            string? viewDefinition = null,
            bool enableCompression = false,
            IReadOnlyList<string>? compressionSegmentBy = null,
            IReadOnlyList<string>? compressionOrderBy = null)
        {
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = materializedViewName,
                ParentName = parentName,
                Schema = schema ?? string.Empty,
                ChunkInterval = chunkInterval,
                WithNoData = withNoData,
                CreateGroupIndexes = createGroupIndexes,
                MaterializedOnly = materializedOnly,
                TimeBucketWidth = timeBucketWidth ?? string.Empty,
                TimeBucketSourceColumn = timeBucketSourceColumn ?? string.Empty,
                TimeBucketColumnName = timeBucketColumnName,
                TimeBucketGroupBy = timeBucketGroupBy,
                AggregateFunctions = aggregateFunctions is null ? [] : [.. aggregateFunctions.Select(f => f.ToAnnotationValue())],
                GroupByColumns = groupByColumns ?? [],
                WhereClause = whereClause,
                ViewDefinition = viewDefinition,
                EnableCompression = enableCompression,
                CompressionSegmentBy = compressionSegmentBy,
                CompressionOrderBy = compressionOrderBy,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<CreateContinuousAggregateOperation>(operation);
        }

        public static OperationBuilder<AlterContinuousAggregateOperation> AlterContinuousAggregate(
            this MigrationBuilder migrationBuilder,
            string materializedViewName,
            string? schema = null,
            string? chunkInterval = null,
            bool createGroupIndexes = false,
            bool materializedOnly = false,
            bool enableCompression = false,
            IReadOnlyList<string>? compressionSegmentBy = null,
            IReadOnlyList<string>? compressionOrderBy = null,
            string? oldChunkInterval = null,
            bool oldCreateGroupIndexes = false,
            bool oldMaterializedOnly = false,
            bool oldEnableCompression = false,
            IReadOnlyList<string>? oldCompressionSegmentBy = null,
            IReadOnlyList<string>? oldCompressionOrderBy = null)
        {
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = materializedViewName,
                Schema = schema ?? string.Empty,
                ChunkInterval = chunkInterval,
                CreateGroupIndexes = createGroupIndexes,
                MaterializedOnly = materializedOnly,
                EnableCompression = enableCompression,
                CompressionSegmentBy = compressionSegmentBy,
                CompressionOrderBy = compressionOrderBy,
                OldChunkInterval = oldChunkInterval,
                OldCreateGroupIndexes = oldCreateGroupIndexes,
                OldMaterializedOnly = oldMaterializedOnly,
                OldEnableCompression = oldEnableCompression,
                OldCompressionSegmentBy = oldCompressionSegmentBy,
                OldCompressionOrderBy = oldCompressionOrderBy,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AlterContinuousAggregateOperation>(operation);
        }

        public static OperationBuilder<DropContinuousAggregateOperation> DropContinuousAggregate(
            this MigrationBuilder migrationBuilder,
            string materializedViewName,
            string? schema = null)
        {
            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = materializedViewName,
                Schema = schema ?? string.Empty,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<DropContinuousAggregateOperation>(operation);
        }
    }
}
