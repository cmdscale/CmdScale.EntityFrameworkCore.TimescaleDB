using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Collections.Generic;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies
{
    internal class ReorderPolicyDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            // Get the standard migration operations (CreateTable, AddColumn, etc.) from the base MigrationsModelDiffer.
            List<MigrationOperation> operations = [];

            // Reorder diffs
            List<AddReorderPolicyOperation> sourcePolicies = [.. ReorderPolicyModelExtractor.GetReorderPolicies(source)];
            List<AddReorderPolicyOperation> targetPolicies = [.. ReorderPolicyModelExtractor.GetReorderPolicies(target)];

            // Identiy new reorder policies
            IEnumerable<AddReorderPolicyOperation> newReorderPolicies = targetPolicies.Where(t => !sourcePolicies.Any(s => s.TableName == t.TableName));
            operations.AddRange(newReorderPolicies);

            // Identify updated reorder policies
            var updatedReorderPolicies = targetPolicies
                .Join(
                    sourcePolicies,
                    targetPolicy => targetPolicy.TableName,
                    sourcePolicy => sourcePolicy.TableName,
                    (targetPolicy, sourcePolicy) => new { Target = targetPolicy, Source = sourcePolicy }
                )
                .Where(x =>
                    x.Target.IndexName != x.Source.IndexName ||
                    x.Target.InitialStart != x.Source.InitialStart ||
                    x.Target.ScheduleInterval != x.Source.ScheduleInterval ||
                    x.Target.MaxRuntime != x.Source.MaxRuntime ||
                    x.Target.MaxRetries != x.Source.MaxRetries ||
                    x.Target.RetryPeriod != x.Source.RetryPeriod
                );

            foreach (var policy in updatedReorderPolicies)
            {
                operations.Add(new AlterReorderPolicyOperation
                {
                    TableName = policy.Target.TableName,
                    IndexName = policy.Target.IndexName,
                    InitialStart = policy.Target.InitialStart,
                    ScheduleInterval = policy.Target.ScheduleInterval,
                    MaxRuntime = policy.Target.MaxRuntime,
                    MaxRetries = policy.Target.MaxRetries,
                    RetryPeriod = policy.Target.RetryPeriod,

                    OldIndexName = policy.Source.IndexName,
                    OldInitialStart = policy.Source.InitialStart,
                    OldScheduleInterval = policy.Source.ScheduleInterval,
                    OldMaxRuntime = policy.Source.MaxRuntime,
                    OldMaxRetries = policy.Source.MaxRetries,
                    OldRetryPeriod = policy.Source.RetryPeriod
                });
            }

            IEnumerable<DropReorderPolicyOperation> removedReorderPolicies = sourcePolicies
                .Where(s => !targetPolicies.Any(t => t.TableName == s.TableName))
                .Select(p => new DropReorderPolicyOperation { TableName = p.TableName });
            operations.AddRange(removedReorderPolicies);

            return operations;
        }
    }
}
