using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies
{
    /// <summary>
    /// Detects differences in continuous aggregate refresh policy configurations between model snapshots.
    /// </summary>
    public class ContinuousAggregatePolicyDiffer : IFeatureDiffer
    {
        /// <summary>
        /// Gets the migration operations needed to transition continuous aggregate refresh policies from the source to the target model.
        /// </summary>
        /// <param name="source">The source model (from the last migration).</param>
        /// <param name="target">The target model (the current state).</param>
        /// <returns>A collection of migration operations.</returns>
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            List<MigrationOperation> operations = [];

            List<AddContinuousAggregatePolicyOperation> sourcePolicies = [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(source)];
            List<AddContinuousAggregatePolicyOperation> targetPolicies = [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(target)];

            // Find new policies - continuous aggregates that now have a policy but didn't before
            IEnumerable<AddContinuousAggregatePolicyOperation> newPolicies = targetPolicies
                .Where(t => !sourcePolicies.Any(s => s.Schema == t.Schema && s.MaterializedViewName == t.MaterializedViewName));
            operations.AddRange(newPolicies);

            // Find removed policies - continuous aggregates that had a policy but no longer do
            IEnumerable<RemoveContinuousAggregatePolicyOperation> removedPolicies = sourcePolicies
                .Where(s => !targetPolicies.Any(t => t.Schema == s.Schema && t.MaterializedViewName == s.MaterializedViewName))
                .Select(s => new RemoveContinuousAggregatePolicyOperation
                {
                    Schema = s.Schema,
                    MaterializedViewName = s.MaterializedViewName,
                    IfExists = true // Use IfExists to avoid errors if the policy was already removed
                });
            operations.AddRange(removedPolicies);

            // Find modified policies - policies that exist in both but have different configurations
            // Since TimescaleDB doesn't have an "alter" function for continuous aggregate policies,
            // we need to remove and re-add the policy when configuration changes.
            var modifiedPolicies = targetPolicies
                .Join(
                    sourcePolicies,
                    target => (target.Schema, target.MaterializedViewName),
                    source => (source.Schema, source.MaterializedViewName),
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x => !ArePoliciesEqual(x.Source, x.Target));

            foreach (var policy in modifiedPolicies)
            {
                // Remove the old policy
                operations.Add(new RemoveContinuousAggregatePolicyOperation
                {
                    Schema = policy.Source.Schema,
                    MaterializedViewName = policy.Source.MaterializedViewName,
                    IfExists = true
                });

                // Add the new policy with updated configuration
                operations.Add(policy.Target);
            }

            return operations;
        }

        /// <summary>
        /// Compares two policy configurations to determine if they are equal.
        /// </summary>
        private static bool ArePoliciesEqual(AddContinuousAggregatePolicyOperation source, AddContinuousAggregatePolicyOperation target)
        {
            return source.StartOffset == target.StartOffset &&
                   source.EndOffset == target.EndOffset &&
                   source.ScheduleInterval == target.ScheduleInterval &&
                   source.InitialStart == target.InitialStart &&
                   source.Timezone == target.Timezone &&
                   source.IncludeTieredData == target.IncludeTieredData &&
                   source.BucketsPerBatch == target.BucketsPerBatch &&
                   source.MaxBatchesPerExecution == target.MaxBatchesPerExecution &&
                   source.RefreshNewestFirst == target.RefreshNewestFirst;
        }
    }
}
