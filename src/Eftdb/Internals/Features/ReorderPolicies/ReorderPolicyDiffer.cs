using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies
{
    internal class ReorderPolicyDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
        {
            context ??= FeatureDiffContext.Empty;

            // Get the standard migration operations (CreateTable, AddColumn, etc.) from the base MigrationsModelDiffer.
            List<MigrationOperation> operations = [];

            // Apply table/index renames to the source so a rename isn't seen as a new policy.
            List<AddReorderPolicyOperation> sourcePolicies = [.. ReorderPolicyModelExtractor.GetReorderPolicies(source).Select(s => RewriteSource(s, context))];
            List<AddReorderPolicyOperation> targetPolicies = [.. ReorderPolicyModelExtractor.GetReorderPolicies(target)];

            // Identiy new reorder policies (keyed on schema and table name, consistent with the update join below)
            IEnumerable<AddReorderPolicyOperation> newReorderPolicies = targetPolicies.Where(t => !sourcePolicies.Any(s => s.Schema == t.Schema && s.TableName == t.TableName));
            operations.AddRange(newReorderPolicies);

            // Identify updated reorder policies
            var updatedReorderPolicies = targetPolicies
                .Join(
                    sourcePolicies,
                    targetPolicy => (targetPolicy.Schema, targetPolicy.TableName),
                    sourcePolicy => (sourcePolicy.Schema, sourcePolicy.TableName),
                    (targetPolicy, sourcePolicy) => new { Target = targetPolicy, Source = sourcePolicy }
                )
                .Where(x =>
                    x.Target.IndexName != x.Source.IndexName ||
                    ConventionValidationHelper.NormalizeInitialStartToUtc(x.Target.InitialStart) != ConventionValidationHelper.NormalizeInitialStartToUtc(x.Source.InitialStart) ||
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
                    Schema = policy.Target.Schema,
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
                .Where(s => !targetPolicies.Any(t => t.TableName == s.TableName && t.Schema == s.Schema))
                .Select(p => new DropReorderPolicyOperation { TableName = p.TableName, Schema = p.Schema });
            operations.AddRange(removedReorderPolicies);

            return operations;
        }

        /// <summary>
        /// Produces a copy of a source reorder policy with its table, schema, and referenced index rewritten through
        /// the rename maps, so that a pure rename compares equal to its target and produces no operation.
        /// </summary>
        private static AddReorderPolicyOperation RewriteSource(AddReorderPolicyOperation source, FeatureDiffContext context)
        {
            (string schema, string tableName) = context.ResolveTable(source.Schema, source.TableName);
            (_, string indexName) = context.ResolveIndex(source.Schema, source.IndexName);

            return new AddReorderPolicyOperation
            {
                TableName = tableName,
                Schema = schema,
                IndexName = indexName,
                InitialStart = source.InitialStart,
                ScheduleInterval = source.ScheduleInterval,
                MaxRuntime = source.MaxRuntime,
                MaxRetries = source.MaxRetries,
                RetryPeriod = source.RetryPeriod,
            };
        }
    }
}
