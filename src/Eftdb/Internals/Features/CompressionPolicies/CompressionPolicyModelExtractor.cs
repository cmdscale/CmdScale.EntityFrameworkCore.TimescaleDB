using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies
{
    internal static class CompressionPolicyModelExtractor
    {
        /// <summary>
        /// Pairs a compression policy operation with the chunk time interval of its owning hypertable,
        /// so the differ can compute the correct default schedule interval per-table.
        /// </summary>
        internal record CompressionPolicyEntry(AddCompressionPolicyOperation Operation, string ChunkTimeInterval);

        /// <summary>
        /// Returns all compression policy entries from the model, each paired with the owning
        /// hypertable's chunk time interval.
        /// </summary>
        internal static IEnumerable<CompressionPolicyEntry> GetCompressionPolicyEntries(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }

            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                bool hasCompressionPolicy = entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value as bool? ?? false;
                if (!hasCompressionPolicy)
                {
                    continue;
                }

                string? after = entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value as string;
                string? createdBefore = entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore)?.Value as string;

                if (string.IsNullOrWhiteSpace(after) && string.IsNullOrWhiteSpace(createdBefore))
                {
                    continue;
                }

                string? targetName = entityType.GetTableName() ?? entityType.GetViewName();
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    continue;
                }

                DateTime? initialStart = entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)?.Value as DateTime?;
                string chunkTimeInterval = entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string
                    ?? DefaultValues.ChunkTimeInterval;

                AddCompressionPolicyOperation operation = new()
                {
                    TableName = targetName,
                    Schema = entityType.GetSchema() ?? entityType.GetViewSchema() ?? DefaultValues.DefaultSchema,
                    After = after,
                    CreatedBefore = createdBefore,
                    InitialStart = initialStart,
                    ScheduleInterval = entityType.FindAnnotation(CompressionPolicyAnnotations.ScheduleInterval)?.Value as string,
                    Timezone = entityType.FindAnnotation(CompressionPolicyAnnotations.Timezone)?.Value as string,
                    IfNotExists = entityType.FindAnnotation(CompressionPolicyAnnotations.IfNotExists)?.Value as bool?,
                };

                yield return new CompressionPolicyEntry(operation, chunkTimeInterval);
            }
        }
    }
}
