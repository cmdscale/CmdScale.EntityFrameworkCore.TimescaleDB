using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies
{
    public static class RetentionPolicyModelExtractor
    {
        public static IEnumerable<AddRetentionPolicyOperation> GetRetentionPolicies(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }

            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                bool hasRetentionPolicy = entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value as bool? ?? false;
                if (!hasRetentionPolicy)
                {
                    continue;
                }

                string? dropAfter = entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value as string;
                string? dropCreatedBefore = entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore)?.Value as string;

                if (string.IsNullOrWhiteSpace(dropAfter) && string.IsNullOrWhiteSpace(dropCreatedBefore))
                {
                    continue;
                }

                // Resolve the target name: table name for hypertables, view name for continuous aggregates
                string? targetName = entityType.GetTableName() ?? entityType.GetViewName();
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    continue;
                }

                DateTime? initialStart = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value as DateTime?;

                yield return new AddRetentionPolicyOperation
                {
                    TableName = targetName,
                    Schema = entityType.GetSchema() ?? entityType.GetViewSchema() ?? DefaultValues.DefaultSchema,
                    DropAfter = dropAfter,
                    DropCreatedBefore = dropCreatedBefore,
                    InitialStart = initialStart,
                    ScheduleInterval = entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value as string ?? DefaultValues.RetentionPolicyScheduleInterval,
                    MaxRuntime = entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value as string ?? DefaultValues.RetentionPolicyMaxRuntime,
                    MaxRetries = entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value as int? ?? DefaultValues.RetentionPolicyMaxRetries,
                    RetryPeriod = entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value as string ?? DefaultValues.RetentionPolicyScheduleInterval
                };
            }
        }
    }
}
