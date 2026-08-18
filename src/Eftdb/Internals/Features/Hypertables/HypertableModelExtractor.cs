using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables
{
    internal static class HypertableModelExtractor
    {
        public static IEnumerable<CreateHypertableOperation> GetHypertables(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }

            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                // Retrieve the annotations set by the convention
                bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
                if (!isHypertable)
                {
                    continue;
                }

                // Get convention-aware store identifier for the table
                StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

                string? timeColumnModelName = entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value as string;
                if (string.IsNullOrWhiteSpace(timeColumnModelName))
                {
                    continue;
                }

                string? timeColumnName = ColumnNameResolver.Resolve(entityType, timeColumnModelName, storeIdentifier);
                if (string.IsNullOrWhiteSpace(timeColumnName))
                {
                    continue;
                }


                string? chunkSkipColumnsString = entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value as string;
                List<string>? chunkSkipColumns = null;
                if (!string.IsNullOrWhiteSpace(chunkSkipColumnsString))
                {
                    chunkSkipColumns = chunkSkipColumnsString.Split(',', StringSplitOptions.TrimEntries)
                        .Select(name => ColumnNameResolver.Resolve(entityType, name, storeIdentifier))
                        .Where(name => name != null)
                        .ToList()!;
                }

                List<string>? compressionSegmentBy = CompressionAnnotationExtractor.ExtractSegmentByColumns(entityType, storeIdentifier);
                List<string>? compressionOrderBy = CompressionAnnotationExtractor.ExtractOrderByColumns(entityType, storeIdentifier);

                List<Dimension>? additionalDimensions = null;
                IAnnotation? additionalDimensionsAnnotations = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions);
                if (additionalDimensionsAnnotations?.Value is string json && !string.IsNullOrWhiteSpace(json))
                {
                    List<Dimension>? modelDimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
                    if (modelDimensions != null)
                    {
                        additionalDimensions = [];
                        foreach (Dimension dim in modelDimensions)
                        {
                            string? conventionalColumnName = ColumnNameResolver.Resolve(entityType, dim.ColumnName, storeIdentifier);
                            if (conventionalColumnName != null)
                            {
                                Dimension newDimension = JsonSerializer.Deserialize<Dimension>(JsonSerializer.Serialize(dim))!;
                                newDimension.ColumnName = conventionalColumnName;
                                additionalDimensions.Add(newDimension);
                            }
                        }
                    }
                }

                string chunkTimeInterval = entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string ?? DefaultValues.ChunkTimeInterval;
                bool enableCompression = entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value as bool? ?? false;
                bool migrateData = entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value as bool? ?? false;
                string? compressionSparseIndex = CompressionAnnotationExtractor.ExtractSparseIndex(entityType, storeIdentifier);
                string? compressChunkTimeInterval = entityType.FindAnnotation(HypertableAnnotations.CompressChunkTimeInterval)?.Value as string;

                yield return new CreateHypertableOperation
                {
                    TableName = entityType.GetTableName()!,
                    Schema = entityType.GetSchema() ?? DefaultValues.DefaultSchema,
                    TimeColumnName = timeColumnName,
                    ChunkTimeInterval = chunkTimeInterval ?? DefaultValues.ChunkTimeInterval,
                    EnableCompression = enableCompression,
                    MigrateData = migrateData,
                    ChunkSkipColumns = chunkSkipColumns,
                    AdditionalDimensions = additionalDimensions,
                    CompressionSegmentBy = compressionSegmentBy,
                    CompressionOrderBy = compressionOrderBy,
                    CompressionSparseIndex = compressionSparseIndex,
                    CompressChunkTimeInterval = compressChunkTimeInterval,
                };
            }
        }
    }
}
