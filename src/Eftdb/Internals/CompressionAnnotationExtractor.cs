using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Shared helpers for extracting compression segment-by and order-by column lists from
    /// entity-type annotations, used by both the hypertable and continuous-aggregate model extractors.
    /// </summary>
    internal static class CompressionAnnotationExtractor
    {
        /// <summary>
        /// Extracts and resolves <c>compress_segmentby</c> column names from the entity's annotations,
        /// mapping CLR property names to their database column names via the supplied store identifier.
        /// Returns <see langword="null"/> when the annotation is absent or empty.
        /// </summary>
        internal static List<string>? ExtractSegmentByColumns(IEntityType entityType, StoreObjectIdentifier storeIdentifier)
        {
            string? segmentByString = entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
            if (string.IsNullOrWhiteSpace(segmentByString))
            {
                return null;
            }

            List<string> result = [];
            foreach (string token in segmentByString.Split(',', StringSplitOptions.TrimEntries))
            {
                string resolved = ResolveColumnName(entityType, storeIdentifier, token);
                if (!string.IsNullOrEmpty(resolved))
                {
                    result.Add(resolved);
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Extracts and resolves <c>compress_orderby</c> column expressions from the entity's annotations,
        /// mapping the leading CLR property name in each clause to its database column name while preserving
        /// direction/nulls suffixes (e.g., <c>"Timestamp DESC NULLS LAST"</c>).
        /// Returns <see langword="null"/> when the annotation is absent or empty.
        /// </summary>
        internal static List<string>? ExtractOrderByColumns(IEntityType entityType, StoreObjectIdentifier storeIdentifier)
        {
            string? orderByString = entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
            if (string.IsNullOrWhiteSpace(orderByString))
            {
                return null;
            }

            List<string> result = [];
            foreach (string clause in orderByString.Split(',', StringSplitOptions.TrimEntries))
            {
                // Split by first space to isolate the property/column name from direction suffixes.
                string[] parts = clause.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                string columnName = ResolveColumnName(entityType, storeIdentifier, parts[0]);
                if (!string.IsNullOrEmpty(columnName))
                {
                    result.Add(parts.Length > 1 ? $"{columnName} {parts[1]}" : columnName);
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Extracts and resolves <c>timescaledb.sparse_index</c> entries from the entity's annotations.
        /// </summary>
        internal static string? ExtractSparseIndex(IEntityType entityType, StoreObjectIdentifier storeIdentifier)
        {
            IAnnotation? annotation = entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex);
            if (annotation == null)
            {
                return null;
            }

            string raw = annotation.Value as string ?? string.Empty;

            if (raw.Length == 0)
            {
                return string.Empty;
            }

            List<string> canonicalEntries = [];
            foreach (string entry in SplitSparseIndexEntries(raw))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                int parenOpen = trimmed.IndexOf('(');
                int parenClose = trimmed.LastIndexOf(')');

                if (parenOpen < 0 || parenClose < parenOpen)
                {
                    canonicalEntries.Add(trimmed);
                    continue;
                }

                string funcName = trimmed[..parenOpen].Trim();
                string argsPart = trimmed[(parenOpen + 1)..parenClose];

                List<string> resolvedColumns = [];
                foreach (string col in argsPart.Split(',', StringSplitOptions.TrimEntries))
                {
                    if (col.Length > 0)
                    {
                        resolvedColumns.Add(ResolveColumnName(entityType, storeIdentifier, col));
                    }
                }

                canonicalEntries.Add($"{funcName}({string.Join(",", resolvedColumns)})");
            }

            return string.Join(", ", canonicalEntries);
        }

        /// <summary>
        /// Splits a sparse-index annotation value into individual entries using paren-aware splitting.
        /// A top-level comma (one not inside parentheses) separates entries.
        /// </summary>
        internal static IEnumerable<string> SplitSparseIndexEntries(string value)
        {
            int depth = 0;
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    yield return value[start..i];
                    start = i + 1;
                }
            }

            if (start < value.Length)
            {
                yield return value[start..];
            }
        }

        /// <summary>
        /// Resolves a CLR property name or database column name to the database column name.
        /// Falls back to a case-insensitive property and column lookup, then to the input string
        /// when no match is found. Shared by extractors and validation conventions so that both
        /// resolve references identically.
        /// </summary>
        internal static string ResolveColumnName(IEntityType entityType, StoreObjectIdentifier storeIdentifier, string propertyName)
        {
            IProperty? property = ColumnNameResolver.ResolveProperty(entityType, propertyName, storeIdentifier, ignoreCase: true);
            string? resolved = property?.GetColumnName(storeIdentifier);
            return string.IsNullOrEmpty(resolved) ? propertyName : resolved;
        }
    }
}
