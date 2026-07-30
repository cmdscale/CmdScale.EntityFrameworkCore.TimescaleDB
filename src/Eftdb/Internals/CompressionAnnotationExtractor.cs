using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
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
        /// Resolves a CLR property name or database column name to the database column name.
        /// Falls back to the input string when no matching property is found.
        /// </summary>
        private static string ResolveColumnName(IEntityType entityType, StoreObjectIdentifier storeIdentifier, string propertyName)
            => entityType.FindProperty(propertyName)?.GetColumnName(storeIdentifier) ?? propertyName;
    }
}
