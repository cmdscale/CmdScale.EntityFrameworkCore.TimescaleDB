using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies
{
    /// <summary>
    /// Compares compression policy annotations between source and target EF Core models and
    /// produces the migration operations required to bring the source state to the target state.
    /// </summary>
    /// <remarks>
    /// This differ is purely annotation-driven and never reads from the database. It operates
    /// exclusively on EF metadata supplied by <see cref="CompressionPolicyModelExtractor"/>.
    /// Server-side policies that were not modelled in EF Core (including any auto-created default
    /// policies that a future TimescaleDB version might generate) are invisible to this differ and
    /// never produce spurious migration operations.
    /// </remarks>
    internal class CompressionPolicyDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
        {
            context ??= FeatureDiffContext.Empty;

            List<MigrationOperation> operations = [];

            // Apply table renames to the source so a rename isn't seen as a drop-and-add.
            List<CompressionPolicyModelExtractor.CompressionPolicyEntry> sourceEntries =
                [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(source).Select(e => RewriteSourceEntry(e, context))];
            List<CompressionPolicyModelExtractor.CompressionPolicyEntry> targetEntries =
                [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(target)];

            // Identify new compression policies
            IEnumerable<AddCompressionPolicyOperation> newPolicies = targetEntries
                .Where(t => !sourceEntries.Any(s => s.Operation.Schema == t.Operation.Schema && s.Operation.TableName == t.Operation.TableName))
                .Select(e => e.Operation);
            operations.AddRange(newPolicies);

            // Identify updated compression policies
            var updatedPolicies = targetEntries
                .Join(
                    sourceEntries,
                    t => (t.Operation.Schema, t.Operation.TableName),
                    s => (s.Operation.Schema, s.Operation.TableName),
                    (t, s) => new { Target = t, Source = s }
                )
                .Where(x =>
                    x.Target.Operation.After != x.Source.Operation.After ||
                    x.Target.Operation.CreatedBefore != x.Source.Operation.CreatedBefore ||
                    ScheduleIntervalChanged(x.Source, x.Target) ||
                    x.Target.Operation.InitialStart != x.Source.Operation.InitialStart ||
                    x.Target.Operation.Timezone != x.Source.Operation.Timezone
                );

            foreach (var policy in updatedPolicies)
            {
                operations.Add(new AlterCompressionPolicyOperation
                {
                    TableName = policy.Target.Operation.TableName,
                    Schema = policy.Target.Operation.Schema,
                    After = policy.Target.Operation.After,
                    CreatedBefore = policy.Target.Operation.CreatedBefore,
                    ScheduleInterval = policy.Target.Operation.ScheduleInterval,
                    InitialStart = policy.Target.Operation.InitialStart,
                    Timezone = policy.Target.Operation.Timezone,
                    IfNotExists = policy.Target.Operation.IfNotExists,

                    OldAfter = policy.Source.Operation.After,
                    OldCreatedBefore = policy.Source.Operation.CreatedBefore,
                    OldScheduleInterval = policy.Source.Operation.ScheduleInterval,
                    OldInitialStart = policy.Source.Operation.InitialStart,
                    OldTimezone = policy.Source.Operation.Timezone,
                    OldIfNotExists = policy.Source.Operation.IfNotExists,
                });
            }

            // Identify removed compression policies
            IEnumerable<DropCompressionPolicyOperation> removedPolicies = sourceEntries
                .Where(s => !targetEntries.Any(t => t.Operation.TableName == s.Operation.TableName && t.Operation.Schema == s.Operation.Schema))
                .Select(e => new DropCompressionPolicyOperation { TableName = e.Operation.TableName, Schema = e.Operation.Schema });
            operations.AddRange(removedPolicies);

            return operations;
        }

        /// <summary>
        /// Returns true when the schedule interval genuinely changed between models.
        /// A null (not configured) and the TimescaleDB-computed default for the hypertable's chunk
        /// time interval are treated as equivalent: the database already runs at that cadence and
        /// emitting an alter would produce a phantom migration. Each side uses its own chunk interval
        /// to compute its expected default, so a change in chunk interval alone does not suppress a
        /// real schedule-interval difference.
        /// </summary>
        private static bool ScheduleIntervalChanged(
            CompressionPolicyModelExtractor.CompressionPolicyEntry source,
            CompressionPolicyModelExtractor.CompressionPolicyEntry target)
        {
            string? sourceInterval = source.Operation.ScheduleInterval;
            string? targetInterval = target.Operation.ScheduleInterval;

            if (sourceInterval == targetInterval)
            {
                return false;
            }

            string? sourceDefault = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(source.ChunkTimeInterval);
            string? targetDefault = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(target.ChunkTimeInterval);

            string? normSource = sourceDefault != null && sourceInterval == sourceDefault ? null : sourceInterval;
            string? normTarget = targetDefault != null && targetInterval == targetDefault ? null : targetInterval;

            return normSource != normTarget;
        }

        /// <summary>
        /// Produces a copy of a source compression policy entry with its table and schema rewritten
        /// through the table-rename map, so that a pure rename compares equal to its target and
        /// produces no operation.
        /// </summary>
        private static CompressionPolicyModelExtractor.CompressionPolicyEntry RewriteSourceEntry(
            CompressionPolicyModelExtractor.CompressionPolicyEntry entry,
            FeatureDiffContext context)
        {
            (string schema, string tableName) = context.ResolveTable(entry.Operation.Schema, entry.Operation.TableName);

            AddCompressionPolicyOperation rewritten = new()
            {
                TableName = tableName,
                Schema = schema,
                After = entry.Operation.After,
                CreatedBefore = entry.Operation.CreatedBefore,
                ScheduleInterval = entry.Operation.ScheduleInterval,
                InitialStart = entry.Operation.InitialStart,
                Timezone = entry.Operation.Timezone,
                IfNotExists = entry.Operation.IfNotExists,
            };

            return new CompressionPolicyModelExtractor.CompressionPolicyEntry(rewritten, entry.ChunkTimeInterval);
        }
    }
}
