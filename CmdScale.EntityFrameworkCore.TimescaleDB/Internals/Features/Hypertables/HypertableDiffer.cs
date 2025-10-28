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
                    t => t.TableName,
                    s => s.TableName,
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    x.Target.ChunkTimeInterval != x.Source.ChunkTimeInterval ||
                    x.Target.EnableCompression != x.Source.EnableCompression ||
                    !AreChunkSkipColumnsEqual(x.Target.ChunkSkipColumns, x.Source.ChunkSkipColumns)
                );

            foreach (var hypertable in updatedHypertables)
            {
                operations.Add(new AlterHypertableOperation
                {
                    TableName = hypertable.Target.TableName,
                    ChunkTimeInterval = hypertable.Target.ChunkTimeInterval,
                    EnableCompression = hypertable.Target.EnableCompression,
                    ChunkSkipColumns = hypertable.Target.ChunkSkipColumns,
                    OldChunkTimeInterval = hypertable.Source.ChunkTimeInterval,
                    OldEnableCompression = hypertable.Source.EnableCompression,
                    OldChunkSkipColumns = hypertable.Source.ChunkSkipColumns
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
    }
}