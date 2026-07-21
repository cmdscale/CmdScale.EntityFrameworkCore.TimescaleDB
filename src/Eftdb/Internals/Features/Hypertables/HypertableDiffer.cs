using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables
{
    public class HypertableDiffer : IFeatureDiffer
    {
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
        {
            context ??= FeatureDiffContext.Empty;

            List<MigrationOperation> operations = [];

            List<CreateHypertableOperation> sourceHypertables = [.. HypertableModelExtractor.GetHypertables(source).Select(s => RewriteSource(s, context))];
            List<CreateHypertableOperation> targetHypertables = [.. HypertableModelExtractor.GetHypertables(target)];

            // Find new hypertables
            IEnumerable<CreateHypertableOperation> newHypertables = targetHypertables.Where(t => !sourceHypertables.Any(s => s.Schema == t.Schema && s.TableName == t.TableName));
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
                    !AreOrderByListsEqual(x.Target.CompressionOrderBy, x.Source.CompressionOrderBy)
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

            return operations;
        }

        /// <summary>
        /// Produces a copy of a source hypertable with its table, schema, and all column-bearing fields rewritten
        /// through the rename maps, so that a pure rename compares equal to its target and produces no operation.
        /// </summary>
        private static CreateHypertableOperation RewriteSource(CreateHypertableOperation source, FeatureDiffContext context)
        {
            (string schema, string tableName) = context.ResolveTable(source.Schema, source.TableName);

            return new CreateHypertableOperation
            {
                TableName = tableName,
                Schema = schema,
                TimeColumnName = context.ResolveColumn(schema, tableName, source.TimeColumnName),
                ChunkTimeInterval = source.ChunkTimeInterval,
                EnableCompression = source.EnableCompression,
                MigrateData = source.MigrateData,
                ChunkSkipColumns = RewriteColumns(source.ChunkSkipColumns, schema, tableName, context),
                CompressionSegmentBy = RewriteColumns(source.CompressionSegmentBy, schema, tableName, context),
                CompressionOrderBy = RewriteOrderByColumns(source.CompressionOrderBy, schema, tableName, context),
                AdditionalDimensions = RewriteDimensions(source.AdditionalDimensions, schema, tableName, context),
            };
        }

        private static List<string>? RewriteColumns(IReadOnlyList<string>? columns, string schema, string table, FeatureDiffContext context)
            => columns?.Select(c => context.ResolveColumn(schema, table, c)).ToList();

        private static List<string>? RewriteOrderByColumns(IReadOnlyList<string>? columns, string schema, string table, FeatureDiffContext context)
        {
            // Order-by entries carry a direction suffix (e.g. "time DESC"); only the leading column name is renamed.
            return columns?.Select(c =>
            {
                string[] parts = c.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return c;
                }

                string column = context.ResolveColumn(schema, table, parts[0]);
                return parts.Length > 1 ? $"{column} {parts[1]}" : column;
            }).ToList();
        }

        private static List<Dimension>? RewriteDimensions(IReadOnlyList<Dimension>? dimensions, string schema, string table, FeatureDiffContext context)
        {
            return dimensions?.Select(d => new Dimension
            {
                ColumnName = context.ResolveColumn(schema, table, d.ColumnName),
                Type = d.Type,
                Interval = d.Interval,
                NumberOfPartitions = d.NumberOfPartitions,
            }).ToList();
        }

        private static bool AreStringListsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            return (list1 ?? []).SequenceEqual(list2 ?? []);
        }

        /// <summary>
        /// Compares two compression ORDER BY lists treating an implicit direction as ASC.
        /// </summary>
        private static bool AreOrderByListsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            IReadOnlyList<string> l1 = list1 ?? [];
            IReadOnlyList<string> l2 = list2 ?? [];

            if (l1.Count != l2.Count)
            {
                return false;
            }

            return l1.Zip(l2).All(pair => NormalizeOrderByEntry(pair.First) == NormalizeOrderByEntry(pair.Second));
        }

        /// <summary>
        /// Normalizes a compression ORDER BY entry to its canonical form with an explicit direction
        /// keyword immediately after the column name.
        /// Examples:
        ///   "Value"              -> "Value ASC"
        ///   "Value ASC"          -> "Value ASC"
        ///   "Value DESC"         -> "Value DESC"
        ///   "Value NULLS FIRST"  -> "Value ASC NULLS FIRST"
        ///   "Value ASC NULLS FIRST" -> "Value ASC NULLS FIRST"
        /// </summary>
        private static string NormalizeOrderByEntry(string entry)
        {
            string trimmed = entry.Trim();
            int spaceIndex = trimmed.IndexOf(' ');

            if (spaceIndex < 0)
            {
                return trimmed + " ASC";
            }

            string columnPart = trimmed[..spaceIndex];
            string suffix = trimmed[(spaceIndex + 1)..].TrimStart();

            if (suffix.StartsWith("DESC", StringComparison.OrdinalIgnoreCase)
                || suffix.StartsWith("ASC", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return $"{columnPart} ASC {suffix}";
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