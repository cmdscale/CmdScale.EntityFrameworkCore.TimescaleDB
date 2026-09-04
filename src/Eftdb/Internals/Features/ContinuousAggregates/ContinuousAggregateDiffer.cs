using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates
{
    internal class ContinuousAggregateDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
        {
            context ??= FeatureDiffContext.Empty;

            List<MigrationOperation> operations = [];

            List<CreateContinuousAggregateOperation> sourceAggregates = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(source)];
            List<CreateContinuousAggregateOperation> targetAggregates = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(target)];

            foreach (CreateContinuousAggregateOperation aggregate in sourceAggregates)
            {
                (_, aggregate.ParentName) = context.ResolveTable(aggregate.Schema, aggregate.ParentName);
                aggregate.CompressionSegmentBy = CompressionDiffHelper.RewriteColumns(aggregate.CompressionSegmentBy, aggregate.Schema, aggregate.MaterializedViewName, context);
                aggregate.CompressionOrderBy = CompressionDiffHelper.RewriteOrderByColumns(aggregate.CompressionOrderBy, aggregate.Schema, aggregate.MaterializedViewName, context);
            }

            List<DropContinuousAggregateOperation> drops = [];
            List<CreateContinuousAggregateOperation> creates = [];
            HashSet<string> droppedNames = [];

            // Find new continuous aggregates - only compare by MaterializedViewName, not Schema
            creates.AddRange(targetAggregates
                .Where(t => !sourceAggregates.Any(s => s.MaterializedViewName == t.MaterializedViewName)));

            // Find structural changes that require drop and recreate
            FindStructuralChanges(sourceAggregates, targetAggregates, drops, creates, droppedNames);

            // Find removed continuous aggregates
            foreach (CreateContinuousAggregateOperation aggregate in sourceAggregates
                .Where(s => !targetAggregates.Any(t => t.MaterializedViewName == s.MaterializedViewName)))
            {
                droppedNames.Add(aggregate.MaterializedViewName);
                drops.Add(new DropContinuousAggregateOperation
                {
                    Schema = aggregate.Schema,
                    MaterializedViewName = aggregate.MaterializedViewName
                });
            }

            // A materialized view cannot be dropped while hierarchical aggregates depend on it, so
            // descendants of any dropped aggregate are dropped and recreated as well. Source aggregates
            // arrive parent-first from the extractor, so one forward pass propagates transitively.
            foreach (CreateContinuousAggregateOperation sourceAggregate in sourceAggregates)
            {
                if (droppedNames.Contains(sourceAggregate.MaterializedViewName) || !droppedNames.Contains(sourceAggregate.ParentName))
                {
                    continue;
                }

                droppedNames.Add(sourceAggregate.MaterializedViewName);
                drops.Add(new DropContinuousAggregateOperation
                {
                    Schema = sourceAggregate.Schema,
                    MaterializedViewName = sourceAggregate.MaterializedViewName
                });

                CreateContinuousAggregateOperation? recreateTarget = targetAggregates
                    .FirstOrDefault(t => t.MaterializedViewName == sourceAggregate.MaterializedViewName);
                if (recreateTarget != null)
                {
                    creates.Add(recreateTarget);
                }
            }

            // Find updated continuous aggregates; recreated aggregates already carry their new settings
            FindAlterableChanges(sourceAggregates, targetAggregates, droppedNames, operations);

            // The extractor emits aggregates parent-first (topologically sorted), and the model
            // differ's priority sort is stable, so emission order decides execution order: drops in
            // reverse topological order (children before their parents), creates in forward order
            // (parents before their children).
            Dictionary<string, int> sourceOrder = TopologicalIndexByViewName(sourceAggregates);
            Dictionary<string, int> targetOrder = TopologicalIndexByViewName(targetAggregates);
            operations.AddRange(drops.OrderByDescending(d => sourceOrder.GetValueOrDefault(d.MaterializedViewName)));
            operations.AddRange(creates.OrderBy(c => targetOrder.GetValueOrDefault(c.MaterializedViewName)));

            return operations;
        }

        /// <summary>
        /// Find structural changes that require drop and recreate
        /// Note: Only certain properties can be altered (ChunkInterval, CreateGroupIndexes,
        /// MaterializedOnly, and compression settings).
        /// For structural changes (time bucket, aggregates, group by, where), drop and recreate is required.     
        /// </summary>
        private static void FindStructuralChanges(
            List<CreateContinuousAggregateOperation> sourceAggregates,
            List<CreateContinuousAggregateOperation> targetAggregates,
            List<DropContinuousAggregateOperation> drops,
            List<CreateContinuousAggregateOperation> creates,
            HashSet<string> droppedNames)
        {

            var structurallyChangedAggregates = targetAggregates
                .Join(
                    sourceAggregates,
                    target => (target.Schema, target.MaterializedViewName),
                    source => (source.Schema, source.MaterializedViewName),
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    x.Target.ParentName != x.Source.ParentName ||
                    x.Target.TimeBucketWidth != x.Source.TimeBucketWidth ||
                    x.Target.TimeBucketSourceColumn != x.Source.TimeBucketSourceColumn ||
                    x.Target.TimeBucketColumnName != x.Source.TimeBucketColumnName ||
                    x.Target.TimeBucketGroupBy != x.Source.TimeBucketGroupBy ||
                    x.Target.WithNoData != x.Source.WithNoData ||
                    !AreAggregateFunctionsEqual(x.Target.AggregateFunctions, x.Source.AggregateFunctions) ||
                    !AreGroupByColumnsEqual(x.Target.GroupByColumns, x.Source.GroupByColumns) ||
                    x.Target.WhereClause != x.Source.WhereClause ||
                    x.Target.ViewDefinition != x.Source.ViewDefinition
                );

            foreach (var aggregate in structurallyChangedAggregates)
            {
                droppedNames.Add(aggregate.Source.MaterializedViewName);
                drops.Add(new DropContinuousAggregateOperation
                {
                    Schema = aggregate.Source.Schema,
                    MaterializedViewName = aggregate.Source.MaterializedViewName
                });

                creates.Add(aggregate.Target);
            }
        }

        /// <summary>
        /// Find changes limited to properties that can be applied in place (ChunkInterval,
        /// CreateGroupIndexes, MaterializedOnly, and compression settings) and emit alter operations.
        /// Aggregates already marked for drop and recreate are skipped; their recreated definition
        /// carries the new settings.
        /// </summary>
        private static void FindAlterableChanges(
            List<CreateContinuousAggregateOperation> sourceAggregates,
            List<CreateContinuousAggregateOperation> targetAggregates,
            HashSet<string> droppedNames,
            List<MigrationOperation> operations)
        {
            var updatedAggregates = targetAggregates
                .Join(
                    sourceAggregates,
                    target => (target.Schema, target.MaterializedViewName),
                    source => (source.Schema, source.MaterializedViewName),
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    !droppedNames.Contains(x.Target.MaterializedViewName) &&
                    (
                        x.Target.ChunkInterval != x.Source.ChunkInterval ||
                        x.Target.CreateGroupIndexes != x.Source.CreateGroupIndexes ||
                        x.Target.MaterializedOnly != x.Source.MaterializedOnly ||
                        x.Target.EnableCompression != x.Source.EnableCompression ||
                        !CompressionDiffHelper.AreStringListsEqual(x.Target.CompressionSegmentBy, x.Source.CompressionSegmentBy) ||
                        !CompressionDiffHelper.AreOrderByListsEqual(x.Target.CompressionOrderBy, x.Source.CompressionOrderBy)
                    )
                );

            foreach (var aggregate in updatedAggregates)
            {
                operations.Add(new AlterContinuousAggregateOperation
                {
                    Schema = aggregate.Target.Schema,
                    MaterializedViewName = aggregate.Target.MaterializedViewName,
                    ChunkInterval = aggregate.Target.ChunkInterval,
                    CreateGroupIndexes = aggregate.Target.CreateGroupIndexes,
                    MaterializedOnly = aggregate.Target.MaterializedOnly,
                    EnableCompression = aggregate.Target.EnableCompression,
                    CompressionSegmentBy = aggregate.Target.CompressionSegmentBy,
                    CompressionOrderBy = aggregate.Target.CompressionOrderBy,
                    OldChunkInterval = aggregate.Source.ChunkInterval,
                    OldCreateGroupIndexes = aggregate.Source.CreateGroupIndexes,
                    OldMaterializedOnly = aggregate.Source.MaterializedOnly,
                    OldEnableCompression = aggregate.Source.EnableCompression,
                    OldCompressionSegmentBy = aggregate.Source.CompressionSegmentBy,
                    OldCompressionOrderBy = aggregate.Source.CompressionOrderBy,
                });
            }
        }

        /// <summary>
        /// Captures each aggregate's position in the extractor's parent-first topological order
        /// (see <see cref="ContinuousAggregateModelExtractor"/>) as a view-name lookup, so drops and
        /// creates can be sorted against it.
        /// </summary>
        private static Dictionary<string, int> TopologicalIndexByViewName(List<CreateContinuousAggregateOperation> aggregates)
        {
            Dictionary<string, int> order = [];
            for (int i = 0; i < aggregates.Count; i++)
            {
                order.TryAdd(aggregates[i].MaterializedViewName, i);
            }

            return order;
        }

        private static bool AreAggregateFunctionsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            return list1.SequenceEqual(list2);
        }

        private static bool AreGroupByColumnsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            return list1.ToHashSet().SetEquals(list2);
        }
    }
}
