using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies
{
    public class RetentionPolicyDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
        {
            context ??= FeatureDiffContext.Empty;

            List<MigrationOperation> operations = [];

            // Apply table renames to the source so a rename isn't seen as a drop-and-add.
            List<AddRetentionPolicyOperation> allSourcePolicies = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(source).Select(s => RewriteSource(s, context))];
            List<AddRetentionPolicyOperation> allTargetPolicies = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(target)];

            // Recreating an aggregate drops its retention policy, so re-add it and skip the normal diff.
            foreach (AddRetentionPolicyOperation policy in allTargetPolicies.Where(t => context.RecreatedAggregates.Contains((t.Schema, t.TableName))))
            {
                operations.Add(policy);
            }

            List<AddRetentionPolicyOperation> sourcePolicies = [.. allSourcePolicies.Where(s => !context.RecreatedAggregates.Contains((s.Schema, s.TableName)))];
            List<AddRetentionPolicyOperation> targetPolicies = [.. allTargetPolicies.Where(t => !context.RecreatedAggregates.Contains((t.Schema, t.TableName)))];

            // Identify new retention policies
            IEnumerable<AddRetentionPolicyOperation> newRetentionPolicies = targetPolicies.Where(t => !sourcePolicies.Any(s => s.TableName == t.TableName && s.Schema == t.Schema));
            operations.AddRange(newRetentionPolicies);

            // Identify updated retention policies
            var updatedRetentionPolicies = targetPolicies
                .Join(
                    sourcePolicies,
                    targetPolicy => (targetPolicy.Schema, targetPolicy.TableName),
                    sourcePolicy => (sourcePolicy.Schema, sourcePolicy.TableName),
                    (targetPolicy, sourcePolicy) => new { Target = targetPolicy, Source = sourcePolicy }
                )
                .Where(x =>
                    x.Target.DropAfter != x.Source.DropAfter ||
                    x.Target.DropCreatedBefore != x.Source.DropCreatedBefore ||
                    x.Target.InitialStart != x.Source.InitialStart ||
                    x.Target.ScheduleInterval != x.Source.ScheduleInterval ||
                    x.Target.MaxRuntime != x.Source.MaxRuntime ||
                    x.Target.MaxRetries != x.Source.MaxRetries ||
                    x.Target.RetryPeriod != x.Source.RetryPeriod
                );

            foreach (var policy in updatedRetentionPolicies)
            {
                operations.Add(new AlterRetentionPolicyOperation
                {
                    TableName = policy.Target.TableName,
                    Schema = policy.Target.Schema,
                    DropAfter = policy.Target.DropAfter,
                    DropCreatedBefore = policy.Target.DropCreatedBefore,
                    InitialStart = policy.Target.InitialStart,
                    ScheduleInterval = policy.Target.ScheduleInterval,
                    MaxRuntime = policy.Target.MaxRuntime,
                    MaxRetries = policy.Target.MaxRetries,
                    RetryPeriod = policy.Target.RetryPeriod,

                    OldDropAfter = policy.Source.DropAfter,
                    OldDropCreatedBefore = policy.Source.DropCreatedBefore,
                    OldInitialStart = policy.Source.InitialStart,
                    OldScheduleInterval = policy.Source.ScheduleInterval,
                    OldMaxRuntime = policy.Source.MaxRuntime,
                    OldMaxRetries = policy.Source.MaxRetries,
                    OldRetryPeriod = policy.Source.RetryPeriod
                });
            }

            // Identify removed retention policies
            IEnumerable<DropRetentionPolicyOperation> removedRetentionPolicies = sourcePolicies
                .Where(s => !targetPolicies.Any(t => t.TableName == s.TableName && t.Schema == s.Schema))
                .Select(p => new DropRetentionPolicyOperation { TableName = p.TableName, Schema = p.Schema });
            operations.AddRange(removedRetentionPolicies);

            return operations;
        }

        /// <summary>
        /// Produces a copy of a source retention policy with its table and schema rewritten through the table-rename
        /// map, so that a pure rename compares equal to its target and produces no operation.
        /// </summary>
        private static AddRetentionPolicyOperation RewriteSource(AddRetentionPolicyOperation source, FeatureDiffContext context)
        {
            (string schema, string tableName) = context.ResolveTable(source.Schema, source.TableName);

            return new AddRetentionPolicyOperation
            {
                TableName = tableName,
                Schema = schema,
                DropAfter = source.DropAfter,
                DropCreatedBefore = source.DropCreatedBefore,
                InitialStart = source.InitialStart,
                ScheduleInterval = source.ScheduleInterval,
                MaxRuntime = source.MaxRuntime,
                MaxRetries = source.MaxRetries,
                RetryPeriod = source.RetryPeriod,
            };
        }
    }
}
