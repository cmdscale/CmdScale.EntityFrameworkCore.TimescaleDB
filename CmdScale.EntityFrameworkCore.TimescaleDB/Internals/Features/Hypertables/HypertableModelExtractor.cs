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
                bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
                string? timeColumnName = entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value as string;

                if (!isHypertable || string.IsNullOrWhiteSpace(timeColumnName))
                {
                    continue;
                }

                string chunkTimeInterval = entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string ?? DefaultValues.ChunkTimeInterval;
                string? chunkSkipColumnsString = entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value as string;
                List<string>? chunkSkipColumns = chunkSkipColumnsString?.Split(',', StringSplitOptions.TrimEntries).ToList();
                bool enableCompression = entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value as bool? ?? false;

                IAnnotation? additionalDimensionsAnnotations = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions);
                List<Dimension>? additionalDimensions = null;
                if (additionalDimensionsAnnotations?.Value is string json && !string.IsNullOrWhiteSpace(json))
                {
                    additionalDimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
                }

                yield return new CreateHypertableOperation
                {
                    TableName = entityType.GetTableName()!,
                    TimeColumnName = timeColumnName,
                    ChunkTimeInterval = chunkTimeInterval,
                    EnableCompression = enableCompression,
                    ChunkSkipColumns = chunkSkipColumns,
                    AdditionalDimensions = additionalDimensions
                };
            }
        }
    }
}