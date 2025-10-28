using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies
{
    internal static class ReorderPolicyModelExtractor
    {
        public static IEnumerable<AddReorderPolicyOperation> GetReorderPolicies(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }
            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                // Retrieve the annotations set by the convention
                bool hasReorderPolicy = entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value as bool? ?? false;
                string? indexName = entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value as string;

                if (!hasReorderPolicy || string.IsNullOrWhiteSpace(indexName))
                {
                    continue;
                }

                DateTime? initialStart = entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value as DateTime?;
                yield return new AddReorderPolicyOperation
                {
                    TableName = entityType.GetTableName()!,
                    IndexName = indexName!,
                    InitialStart = initialStart,
                    ScheduleInterval = entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value as string ?? DefaultValues.ReorderPolicyScheduleInterval,
                    MaxRuntime = entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value as string ?? DefaultValues.ReorderPolicyMaxRuntime,
                    MaxRetries = entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value as int? ?? DefaultValues.ReorderPolicyMaxRetries,
                    RetryPeriod = entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value as string ?? DefaultValues.ReorderPolicyRetryPeriod
                };
                }
            
        }
    }
}
