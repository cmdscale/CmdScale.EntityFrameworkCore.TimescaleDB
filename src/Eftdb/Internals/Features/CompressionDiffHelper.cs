namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features
{
    /// <summary>
    /// Shared comparison and rewrite helpers for compression-related differ logic,
    /// used by both the hypertable and continuous-aggregate feature differs.
    /// </summary>
    internal static class CompressionDiffHelper
    {
        /// <summary>
        /// Returns <see langword="true"/> when both lists are equal in content and order.
        /// Treats <see langword="null"/> as an empty list.
        /// </summary>
        internal static bool AreStringListsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
            => (list1 ?? []).SequenceEqual(list2 ?? []);

        /// <summary>
        /// Compares two compression ORDER BY lists, treating an implicit direction as <c>ASC</c>
        /// so that <c>"col"</c> and <c>"col ASC"</c> compare equal.
        /// </summary>
        internal static bool AreOrderByListsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
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
        /// </summary>
        /// <example>
        /// <list type="bullet">
        ///   <item><c>"Value"</c> → <c>"Value ASC"</c></item>
        ///   <item><c>"Value ASC"</c> → <c>"Value ASC"</c></item>
        ///   <item><c>"Value DESC"</c> → <c>"Value DESC"</c></item>
        ///   <item><c>"Value NULLS FIRST"</c> → <c>"Value ASC NULLS FIRST"</c></item>
        ///   <item><c>"Value ASC NULLS FIRST"</c> → <c>"Value ASC NULLS FIRST"</c></item>
        /// </list>
        /// </example>
        internal static string NormalizeOrderByEntry(string entry)
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

        /// <summary>
        /// Rewrites plain column names through the rename context.
        /// Returns <see langword="null"/> when <paramref name="columns"/> is <see langword="null"/>.
        /// </summary>
        internal static List<string>? RewriteColumns(IReadOnlyList<string>? columns, string schema, string relation, FeatureDiffContext context)
            => columns?.Select(c => context.ResolveColumn(schema, relation, c)).ToList();

        /// <summary>
        /// Rewrites the leading column name of each ORDER BY entry through the rename context,
        /// preserving any direction/nulls suffix.
        /// Returns <see langword="null"/> when <paramref name="columns"/> is <see langword="null"/>.
        /// </summary>
        internal static List<string>? RewriteOrderByColumns(IReadOnlyList<string>? columns, string schema, string relation, FeatureDiffContext context)
        {
            return columns?.Select(c =>
            {
                string[] parts = c.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return c;
                }

                string column = context.ResolveColumn(schema, relation, parts[0]);
                return parts.Length > 1 ? $"{column} {parts[1]}" : column;
            }).ToList();
        }
    }
}
