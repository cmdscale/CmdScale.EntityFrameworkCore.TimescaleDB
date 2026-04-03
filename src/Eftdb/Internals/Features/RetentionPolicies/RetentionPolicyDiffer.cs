using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies
{
    public class RetentionPolicyDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            List<MigrationOperation> operations = [];

            List<AddRetentionPolicyOperation> sourcePolicies = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(source)];
            List<AddRetentionPolicyOperation> targetPolicies = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(target)];

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
    }
}
