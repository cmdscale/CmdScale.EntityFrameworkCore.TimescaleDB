using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables
{
    internal class HypertableDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            List<MigrationOperation> operations = [];

            List<CreateHypertableOperation> sourceHypertables = [.. HypertableModelExtractor.GetHypertables(source)];
            List<CreateHypertableOperation> targetHypertables = [.. HypertableModelExtractor.GetHypertables(target)];

            // Find new hypertables
            IEnumerable<CreateHypertableOperation> newHypertables = targetHypertables.Where(t => !sourceHypertables.Any(s => s.TableName == t.TableName));
            operations.AddRange(newHypertables);

            // Find updated hypertables
            var updatedHypertables = targetHypertables
                .Join(
                    sourceHypertables,
                    target => (target.Schema, target.TableName),
                    source => (source.Schema, source.TableName),
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    x.Target.ChunkTimeInterval != x.Source.ChunkTimeInterval ||
                    x.Target.EnableCompression != x.Source.EnableCompression ||
                    !AreChunkSkipColumnsEqual(x.Target.ChunkSkipColumns, x.Source.ChunkSkipColumns) ||
                    !AreDimensionsEqual(x.Target.AdditionalDimensions, x.Source.AdditionalDimensions)
                );

            foreach (var hypertable in updatedHypertables)
            {
                operations.Add(new AlterHypertableOperation
                {
                    TableName = hypertable.Target.TableName,
                    Schema = hypertable.Target.Schema,
                    ChunkTimeInterval = hypertable.Target.ChunkTimeInterval,
                    EnableCompression = hypertable.Target.EnableCompression,
                    ChunkSkipColumns = hypertable.Target.ChunkSkipColumns,
                    AdditionalDimensions = hypertable.Target.AdditionalDimensions,
                    OldChunkTimeInterval = hypertable.Source.ChunkTimeInterval,
                    OldEnableCompression = hypertable.Source.EnableCompression,
                    OldChunkSkipColumns = hypertable.Source.ChunkSkipColumns,
                    OldAdditionalDimensions = hypertable.Source.AdditionalDimensions
                });
            }

            // TODO: Detect dropped hypertables if TimescaleDB supports a "de-hyper" operation.

            return operations;
        }

        private static bool AreChunkSkipColumnsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            return new HashSet<string>(list1).SetEquals(list2);
        }

        private static bool AreDimensionsEqual(IReadOnlyList<Dimension>? list1, IReadOnlyList<Dimension>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            // Compare each dimension's properties
            for (int i = 0; i < list1.Count; i++)
            {
                Dimension dim1 = list1[i];
                Dimension dim2 = list2[i];

                if (dim1.ColumnName != dim2.ColumnName ||
                    dim1.Type != dim2.Type ||
                    dim1.Interval != dim2.Interval ||
                    dim1.NumberOfPartitions != dim2.NumberOfPartitions)
                {
                    return false;
                }
            }

            return true;
        }
    }
}