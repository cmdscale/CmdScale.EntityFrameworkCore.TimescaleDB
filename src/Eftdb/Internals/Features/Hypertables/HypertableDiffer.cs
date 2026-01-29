using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables
{
    public class HypertableDiffer : IFeatureDiffer
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
                    !AreDimensionsEqual(x.Target.AdditionalDimensions, x.Source.AdditionalDimensions) ||
                    !AreStringListsEqual(x.Target.CompressionSegmentBy, x.Source.CompressionSegmentBy) ||
                    !AreStringListsEqual(x.Target.CompressionOrderBy, x.Source.CompressionOrderBy)
                );

            foreach (var hypertable in updatedHypertables)
            {
                operations.Add(new AlterHypertableOperation
                {
                    TableName = hypertable.Target.TableName,
                    Schema = hypertable.Target.Schema,

                    // Current values
                    ChunkTimeInterval = hypertable.Target.ChunkTimeInterval,
                    EnableCompression = hypertable.Target.EnableCompression,
                    ChunkSkipColumns = hypertable.Target.ChunkSkipColumns,
                    AdditionalDimensions = hypertable.Target.AdditionalDimensions,
                    CompressionSegmentBy = hypertable.Target.CompressionSegmentBy,
                    CompressionOrderBy = hypertable.Target.CompressionOrderBy,

                    // Old values
                    OldChunkTimeInterval = hypertable.Source.ChunkTimeInterval,
                    OldEnableCompression = hypertable.Source.EnableCompression,
                    OldChunkSkipColumns = hypertable.Source.ChunkSkipColumns,
                    OldAdditionalDimensions = hypertable.Source.AdditionalDimensions,
                    OldCompressionSegmentBy = hypertable.Source.CompressionSegmentBy,
                    OldCompressionOrderBy = hypertable.Source.CompressionOrderBy
                });
            }

            // TODO: Detect dropped hypertables if TimescaleDB supports a "de-hyper" operation.

            return operations;
        }

        private static bool AreStringListsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            return (list1 ?? []).SequenceEqual(list2 ?? []);
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