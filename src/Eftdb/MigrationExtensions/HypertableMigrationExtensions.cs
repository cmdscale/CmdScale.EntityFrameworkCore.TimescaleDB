using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    public static class HypertableMigrationExtensions
    {
        public static OperationBuilder<CreateHypertableOperation> CreateHypertable(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string timeColumnName,
            string? schema = null,
            string? chunkTimeInterval = null,
            bool enableCompression = false,
            bool migrateData = false,
            IReadOnlyList<string>? chunkSkipColumns = null,
            IReadOnlyList<Dimension>? additionalDimensions = null,
            IReadOnlyList<string>? compressionSegmentBy = null,
            IReadOnlyList<string>? compressionOrderBy = null,
            string? compressionSparseIndex = null,
            string? compressChunkTimeInterval = null)
        {
            CreateHypertableOperation operation = new()
            {
                TableName = tableName,
                TimeColumnName = timeColumnName,
                Schema = schema ?? string.Empty,
                ChunkTimeInterval = chunkTimeInterval ?? string.Empty,
                EnableCompression = enableCompression,
                MigrateData = migrateData,
                ChunkSkipColumns = chunkSkipColumns,
                AdditionalDimensions = additionalDimensions,
                CompressionSegmentBy = compressionSegmentBy,
                CompressionOrderBy = compressionOrderBy,
                CompressionSparseIndex = compressionSparseIndex,
                CompressChunkTimeInterval = compressChunkTimeInterval,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<CreateHypertableOperation>(operation);
        }

        public static OperationBuilder<AlterHypertableOperation> AlterHypertable(
            this MigrationBuilder migrationBuilder,
            string tableName,
            string? schema = null,
            string? chunkTimeInterval = null,
            bool enableCompression = false,
            IReadOnlyList<string>? chunkSkipColumns = null,
            IReadOnlyList<Dimension>? additionalDimensions = null,
            IReadOnlyList<string>? compressionSegmentBy = null,
            IReadOnlyList<string>? compressionOrderBy = null,
            string? compressionSparseIndex = null,
            string? compressChunkTimeInterval = null,
            string? oldChunkTimeInterval = null,
            bool oldEnableCompression = false,
            IReadOnlyList<string>? oldChunkSkipColumns = null,
            IReadOnlyList<Dimension>? oldAdditionalDimensions = null,
            IReadOnlyList<string>? oldCompressionSegmentBy = null,
            IReadOnlyList<string>? oldCompressionOrderBy = null,
            string? oldCompressionSparseIndex = null,
            string? oldCompressChunkTimeInterval = null)
        {
            AlterHypertableOperation operation = new()
            {
                TableName = tableName,
                Schema = schema ?? string.Empty,
                ChunkTimeInterval = chunkTimeInterval ?? string.Empty,
                EnableCompression = enableCompression,
                ChunkSkipColumns = chunkSkipColumns,
                AdditionalDimensions = additionalDimensions,
                CompressionSegmentBy = compressionSegmentBy,
                CompressionOrderBy = compressionOrderBy,
                CompressionSparseIndex = compressionSparseIndex,
                CompressChunkTimeInterval = compressChunkTimeInterval,
                OldChunkTimeInterval = oldChunkTimeInterval ?? string.Empty,
                OldEnableCompression = oldEnableCompression,
                OldChunkSkipColumns = oldChunkSkipColumns,
                OldAdditionalDimensions = oldAdditionalDimensions,
                OldCompressionSegmentBy = oldCompressionSegmentBy,
                OldCompressionOrderBy = oldCompressionOrderBy,
                OldCompressionSparseIndex = oldCompressionSparseIndex,
                OldCompressChunkTimeInterval = oldCompressChunkTimeInterval,
            };

            migrationBuilder.Operations.Add(operation);
            return new OperationBuilder<AlterHypertableOperation>(operation);
        }
    }
}
