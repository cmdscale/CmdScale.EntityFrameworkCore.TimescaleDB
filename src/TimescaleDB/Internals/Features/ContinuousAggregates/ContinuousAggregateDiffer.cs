using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates
{
    internal class ContinuousAggregateDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            List<MigrationOperation> operations = [];

            List<CreateContinuousAggregateOperation> sourceAggregates = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(source)];
            List<CreateContinuousAggregateOperation> targetAggregates = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(target)];

            // Find new continuous aggregates - only compare by MaterializedViewName, not Schema
            IEnumerable<CreateContinuousAggregateOperation> newAggregates = targetAggregates
                .Where(t => !sourceAggregates.Any(s => s.MaterializedViewName == t.MaterializedViewName));
            operations.AddRange(newAggregates);

            // Find updated continuous aggregates
            // Note: Only certain properties can be altered (ChunkInterval, CreateGroupIndexes, MaterializedOnly)
            // For structural changes (time bucket, aggregates, group by, where), drop and recreate is required
            var updatedAggregates = targetAggregates
                .Join(
                    sourceAggregates,
                    target => (target.Schema, target.MaterializedViewName),
                    source => (source.Schema, source.MaterializedViewName),
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    x.Target.ChunkInterval != x.Source.ChunkInterval ||
                    x.Target.CreateGroupIndexes != x.Source.CreateGroupIndexes ||
                    x.Target.MaterializedOnly != x.Source.MaterializedOnly
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
                    OldChunkInterval = aggregate.Source.ChunkInterval,
                    OldCreateGroupIndexes = aggregate.Source.CreateGroupIndexes,
                    OldMaterializedOnly = aggregate.Source.MaterializedOnly
                });
            }

            // Find structural changes that require drop and recreate
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
                    x.Target.TimeBucketGroupBy != x.Source.TimeBucketGroupBy ||
                    x.Target.WithNoData != x.Source.WithNoData ||
                    !AreAggregateFunctionsEqual(x.Target.AggregateFunctions, x.Source.AggregateFunctions) ||
                    !AreGroupByColumnsEqual(x.Target.GroupByColumns, x.Source.GroupByColumns) ||
                    x.Target.WhereClaus != x.Source.WhereClaus
                );

            foreach (var aggregate in structurallyChangedAggregates)
            {
                operations.Add(new DropContinuousAggregateOperation
                {
                    Schema = aggregate.Source.Schema,
                    MaterializedViewName = aggregate.Source.MaterializedViewName
                });

                operations.Add(aggregate.Target);
            }

            // Find removed continuous aggregates
            IEnumerable<DropContinuousAggregateOperation> removedAggregates = sourceAggregates
                .Where(s => !targetAggregates.Any(t => t.MaterializedViewName == s.MaterializedViewName))
                .Select(s => new DropContinuousAggregateOperation
                {
                    Schema = s.Schema,
                    MaterializedViewName = s.MaterializedViewName
                });
            operations.AddRange(removedAggregates);

            return operations;
        }

        private static bool AreAggregateFunctionsEqual(List<string>? list1, List<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            return list1.SequenceEqual(list2);
        }

        private static bool AreGroupByColumnsEqual(List<string>? list1, List<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            return list1.SequenceEqual(list2);
        }
    }
}
