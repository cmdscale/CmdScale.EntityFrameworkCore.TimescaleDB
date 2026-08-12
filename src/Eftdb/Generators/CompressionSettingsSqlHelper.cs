using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    /// <summary>
    /// Shared SQL-building helpers for compression settings, used by both the hypertable
    /// and continuous-aggregate SQL generators.
    /// </summary>
    internal static class CompressionSettingsSqlHelper
    {
        /// <summary>
        /// Quotes the column name within each ORDER BY clause entry while preserving
        /// direction keywords and NULLS qualifiers.
        /// </summary>
        /// <example>
        /// <c>"Timestamp DESC"</c> becomes <c>"\"Timestamp\" DESC"</c>.
        /// </example>
        internal static string QuoteOrderByList(IEnumerable<string> orderByClauses)
        {
            return string.Join(", ", orderByClauses.Select(clause =>
            {
                string[] parts = clause.Split(' ', 2);
                string col = parts[0];
                string suffix = parts.Length > 1 ? " " + parts[1] : "";

                return SqlBuilderHelper.QuoteIdentifier(col) + suffix;
            }));
        }

        /// <summary>
        /// Returns the option name for enabling compression/columnstore.
        /// </summary>
        internal static string CompressOptionName(bool useLegacy)
            => useLegacy ? "timescaledb.compress" : "timescaledb.enable_columnstore";

        /// <summary>
        /// Returns the option name for the segment-by setting.
        /// </summary>
        internal static string SegmentByOptionName(bool useLegacy)
            => useLegacy ? "timescaledb.compress_segmentby" : "timescaledb.segmentby";

        /// <summary>
        /// Returns the option name for the order-by setting.
        /// </summary>
        internal static string OrderByOptionName(bool useLegacy)
            => useLegacy ? "timescaledb.compress_orderby" : "timescaledb.orderby";

        /// <summary>
        /// Appends a community-feature-guarded compression statement to <paramref name="statements"/>
        /// when compression is configured for a newly created relation (hypertable or materialized view).
        /// </summary>
        /// <param name="statements">The statement list to append to.</param>
        /// <param name="relationName">The unqualified relation name.</param>
        /// <param name="schema">The schema of the relation.</param>
        /// <param name="enableCompression">Whether the explicit compress flag is set.</param>
        /// <param name="compressionSegmentBy">Segment-by column list, or <see langword="null"/>.</param>
        /// <param name="compressionOrderBy">Order-by column list, or <see langword="null"/>.</param>
        /// <param name="alterDdl">The DDL keyword phrase used to target the relation (e.g., <c>"ALTER TABLE"</c> or <c>"ALTER MATERIALIZED VIEW"</c>).</param>
        /// <param name="warningText">The RAISE WARNING text for the Apache Edition path.</param>
        /// <param name="useLegacy">When <see langword="true"/>, emits pre-2.18 compression option names.</param>
        internal static void AppendCreateCompressionStatements(
            List<string> statements,
            string relationName,
            string schema,
            bool enableCompression,
            IReadOnlyList<string>? compressionSegmentBy,
            IReadOnlyList<string>? compressionOrderBy,
            string alterDdl,
            string warningText,
            bool useLegacy = false)
        {
            bool hasSegmentBy = compressionSegmentBy is { Count: > 0 };
            bool hasOrderBy = compressionOrderBy is { Count: > 0 };

            if (!enableCompression && !hasSegmentBy && !hasOrderBy)
            {
                return;
            }

            List<string> compressionSettings = [];

            compressionSettings.Add($"{CompressOptionName(useLegacy)} = true");

            if (hasSegmentBy)
            {
                string segmentList = string.Join(", ", compressionSegmentBy!.Select(SqlBuilderHelper.QuoteIdentifier));
                compressionSettings.Add($"{SegmentByOptionName(useLegacy)} = '{SqlBuilderHelper.EscapeStringLiteral(segmentList)}'");
            }

            if (hasOrderBy)
            {
                string orderList = QuoteOrderByList(compressionOrderBy!);
                compressionSettings.Add($"{OrderByOptionName(useLegacy)} = '{SqlBuilderHelper.EscapeStringLiteral(orderList)}'");
            }

            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(relationName, schema);
            string setClause = $"{alterDdl} {qualifiedIdentifier} SET ({string.Join(", ", compressionSettings)});";
            statements.Add(SqlBuilderHelper.WrapCommunityFeatures([setClause], warningText));
        }

        /// <summary>
        /// Builds the list of changed compression SET options for an alter operation.
        /// Returns an empty list when no compression properties changed.
        /// </summary>
        /// <param name="newEnable">New value of the explicit compress flag.</param>
        /// <param name="newSegmentBy">New segment-by column list.</param>
        /// <param name="newOrderBy">New order-by column list.</param>
        /// <param name="oldEnable">Previous value of the explicit compress flag.</param>
        /// <param name="oldSegmentBy">Previous segment-by column list.</param>
        /// <param name="oldOrderBy">Previous order-by column list.</param>
        /// <param name="useLegacy">When <see langword="true"/>, emits pre-2.18 compression option names.</param>
        internal static List<string> BuildAlterCompressionSettings(
            bool newEnable,
            IReadOnlyList<string>? newSegmentBy,
            IReadOnlyList<string>? newOrderBy,
            bool oldEnable,
            IReadOnlyList<string>? oldSegmentBy,
            IReadOnlyList<string>? oldOrderBy,
            bool useLegacy = false)
        {
            List<string> settings = [];

            bool newCompressionState = IsCompressionEnabled(newEnable, newSegmentBy, newOrderBy);
            bool oldCompressionState = IsCompressionEnabled(oldEnable, oldSegmentBy, oldOrderBy);

            if (newCompressionState != oldCompressionState)
            {
                settings.Add($"{CompressOptionName(useLegacy)} = {newCompressionState.ToString().ToLower()}");
            }

            if (!CompressionDiffHelper.AreStringListsEqual(oldSegmentBy, newSegmentBy))
            {
                string val = (newSegmentBy?.Count > 0)
                    ? $"'{SqlBuilderHelper.EscapeStringLiteral(string.Join(", ", newSegmentBy.Select(SqlBuilderHelper.QuoteIdentifier)))}'"
                    : "''";
                settings.Add($"{SegmentByOptionName(useLegacy)} = {val}");
            }

            if (!CompressionDiffHelper.AreStringListsEqual(oldOrderBy, newOrderBy))
            {
                string val = (newOrderBy?.Count > 0)
                    ? $"'{SqlBuilderHelper.EscapeStringLiteral(QuoteOrderByList(newOrderBy))}'"
                    : "''";
                settings.Add($"{OrderByOptionName(useLegacy)} = {val}");
            }

            return settings;
        }

        /// <summary>
        /// Returns <see langword="true"/> when compression is configured by any supported mechanism:
        /// an explicit enable flag, non-empty segment-by columns, non-empty order-by columns,
        /// or non-empty chunk-skipping columns (which implicitly require compression).
        /// </summary>
        internal static bool IsCompressionEnabled(
            bool enableFlag,
            IReadOnlyList<string>? segmentBy,
            IReadOnlyList<string>? orderBy,
            IReadOnlyList<string>? chunkSkipColumns = null)
            => enableFlag || (segmentBy?.Count > 0) || (orderBy?.Count > 0) || (chunkSkipColumns?.Count > 0);

        /// <summary>
        /// Annotation-value variant of <see cref="IsCompressionEnabled(bool, IReadOnlyList{string}?, IReadOnlyList{string}?, IReadOnlyList{string}?)"/>
        /// for callers holding the comma-joined string form the compression annotations store.
        /// </summary>
        internal static bool IsCompressionEnabled(
            bool enableFlag,
            string? segmentBy,
            string? orderBy,
            string? chunkSkipColumns = null)
            => enableFlag
                || !string.IsNullOrWhiteSpace(segmentBy)
                || !string.IsNullOrWhiteSpace(orderBy)
                || !string.IsNullOrWhiteSpace(chunkSkipColumns);
    }
}
