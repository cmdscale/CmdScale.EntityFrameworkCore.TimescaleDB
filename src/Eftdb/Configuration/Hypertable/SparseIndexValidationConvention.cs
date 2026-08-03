using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Validates <c>timescaledb.sparse_index</c> entries against the entity's <c>compress_segmentby</c>
    /// and <c>compress_orderby</c> configuration. Runs at model finalization so that all fluent API
    /// configuration applied in <c>OnModelCreating</c> is visible.
    /// </summary>
    public class SparseIndexValidationConvention : IModelFinalizedConvention
    {
        /// <inheritdoc />
        public IModel ProcessModelFinalized(IModel model)
        {
            foreach (IEntityType entityType in model.GetEntityTypes())
            {
                ValidateSparseIndex(entityType);
            }

            return model;
        }

        private static void ValidateSparseIndex(IEntityType entityType)
        {
            bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
            if (!isHypertable)
            {
                return;
            }

            var sparseIndexAnnotation = entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex);
            if (sparseIndexAnnotation == null)
            {
                return;
            }

            string raw = sparseIndexAnnotation.Value as string ?? string.Empty;
            string displayName = entityType.ClrType?.Name ?? entityType.Name;

            string? orderByValue = entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
            if (string.IsNullOrWhiteSpace(orderByValue))
            {
                throw new InvalidOperationException(
                    $"Hypertable '{displayName}': sparse_index requires compress_orderby to be configured. " +
                    "Call WithCompressionOrderBy() or set the CompressionOrderBy property before using WithSparseIndex().");
            }

            if (raw.Length == 0)
            {
                return;
            }

            string? tableName = entityType.GetTableName();
            StoreObjectIdentifier storeIdentifier = tableName != null
                ? StoreObjectIdentifier.Table(tableName, entityType.GetSchema())
                : default;

            HashSet<string> segmentByColumns = [.. CompressionAnnotationExtractor.ExtractSegmentByColumns(entityType, storeIdentifier) ?? []];
            HashSet<string> orderByColumns = [.. (CompressionAnnotationExtractor.ExtractOrderByColumns(entityType, storeIdentifier) ?? [])
                .Select(clause => clause.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0])];

            Dictionary<string, string> singleColumnEntries = [];

            foreach (string rawEntry in CompressionAnnotationExtractor.SplitSparseIndexEntries(raw))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                int parenOpen = entry.IndexOf('(');
                int parenClose = entry.LastIndexOf(')');

                if (parenOpen < 0 || parenClose < 0 || parenClose < parenOpen)
                {
                    throw new InvalidOperationException(
                        $"Sparse index on '{displayName}': entry '{entry}' is malformed — missing or unbalanced parentheses.");
                }

                string funcName = entry[..parenOpen].Trim();
                if (!string.Equals(funcName, "bloom", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(funcName, "minmax", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Sparse index on '{displayName}': entry '{entry}' uses unknown function '{funcName}' — only 'bloom' and 'minmax' are supported.");
                }

                string argsPart = entry[(parenOpen + 1)..parenClose];
                List<string> columns = [];
                foreach (string col in argsPart.Split(',', StringSplitOptions.TrimEntries))
                {
                    if (col.Length > 0)
                    {
                        columns.Add(CompressionAnnotationExtractor.ResolveColumnName(entityType, storeIdentifier, col));
                    }
                }

                if (columns.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Sparse index on '{displayName}': entry '{entry}' has an empty argument list — at least one column must be specified.");
                }

                bool isComposite = columns.Count > 1;

                if (string.Equals(funcName, "bloom", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string col in columns)
                    {
                        if (segmentByColumns.Contains(col))
                        {
                            throw new InvalidOperationException(
                                $"Sparse index on '{displayName}': entry '{entry}' includes compress_segmentby column '{col}'. " +
                                "Segmentby columns are not compressed and cannot have a sparse index.");
                        }
                    }

                    if (!isComposite && orderByColumns.Contains(columns[0]))
                    {
                        throw new InvalidOperationException(
                            $"Sparse index on '{displayName}': entry '{entry}' — bloom({columns[0]}) is redundant because " +
                            $"'{columns[0]}' is a compress_orderby column that already receives an implicit sparse index. " +
                            "Remove the explicit entry or use a composite bloom if additional columns are needed.");
                    }
                }
                else
                {
                    if (columns.Count > 1)
                    {
                        throw new InvalidOperationException(
                            $"Sparse index on '{displayName}': entry '{entry}' — minmax supports a single column only. " +
                            "Use bloom(...) for composite entries.");
                    }

                    foreach (string col in columns)
                    {
                        if (segmentByColumns.Contains(col))
                        {
                            throw new InvalidOperationException(
                                $"Sparse index on '{displayName}': entry '{entry}' includes compress_segmentby column '{col}'. " +
                                "Segmentby columns are not compressed and cannot have a sparse index.");
                        }
                    }
                }

                if (!isComposite)
                {
                    string singleCol = columns[0];
                    if (singleColumnEntries.TryGetValue(singleCol, out string? existingEntry))
                    {
                        throw new InvalidOperationException(
                            $"Sparse index on '{displayName}': duplicate single-column sparse index entries " +
                            $"'{existingEntry}' and '{entry}' both target column '{singleCol}'. " +
                            "Remove one of them.");
                    }

                    singleColumnEntries[singleCol] = entry;
                }
            }
        }

    }
}
