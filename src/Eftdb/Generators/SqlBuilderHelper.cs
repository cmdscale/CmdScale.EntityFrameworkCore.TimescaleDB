using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    internal static class SqlBuilderHelper
    {
        private static readonly string quoteString = "\"";

        public static void BuildQueryString(List<string> statements, MigrationCommandListBuilder builder, bool suppressTransaction = false, bool usePerform = false)
        {
            if (statements.Count == 0)
            {
                return;
            }

            // Group consecutive statements that don't end with semicolon into single commands
            List<List<string>> commandGroups = [];
            List<string> currentGroup = [];
            int dollarQuoteCount = 0;

            foreach (string statement in statements)
            {
                currentGroup.Add(statement);

                dollarQuoteCount += statement.AsSpan().Count("$$");
                bool insideDollarQuote = dollarQuoteCount % 2 != 0;

                if (!insideDollarQuote && statement.TrimEnd().EndsWith(';'))
                {
                    commandGroups.Add([.. currentGroup]);
                    currentGroup.Clear();
                }
            }

            // Add any remaining statements as a final command
            if (currentGroup.Count > 0)
            {
                commandGroups.Add([.. currentGroup]);
            }

            // Build each command group
            foreach (List<string> group in commandGroups)
            {
                List<string> processedGroup = usePerform
                    ? [.. group.Select(ReplaceSelectWithPerform)]
                    : group;

                string command = string.Join("\n", processedGroup);
                builder
                    .Append(command)
                    .EndCommand(suppressTransaction: suppressTransaction);
            }
        }

        /// <summary>
        /// Replaces a leading SELECT keyword with PERFORM for use inside PL/pgSQL blocks.
        /// In PL/pgSQL (e.g., idempotent migration scripts), bare SELECT statements that return
        /// results fail with "query has no destination for result data". PERFORM discards the
        /// results silently and is the standard PL/pgSQL equivalent of SELECT for this purpose.
        /// </summary>
        internal static string ReplaceSelectWithPerform(string sql)
        {
            string trimmed = sql.TrimStart();
            if (trimmed.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
            {
                int leadingWhitespaceLength = sql.Length - trimmed.Length;
                return string.Concat(sql.AsSpan(0, leadingWhitespaceLength), "PERFORM", trimmed.AsSpan("SELECT".Length));
            }

            return sql;
        }

        /// <summary>
        /// Applies <see cref="ReplaceSelectWithPerform"/> to each line of a multi-line SQL string.
        /// Handles multi-statement SQL blocks where each statement starts on its own line.
        /// Continuation lines (FROM, WHERE, etc.) are not affected because they don't start with SELECT.
        /// </summary>
        internal static string ReplaceSelectWithPerformMultiLine(string sql)
        {
            string[] lines = sql.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = ReplaceSelectWithPerform(lines[i]);
            }

            return string.Join('\n', lines);
        }

        public static string Regclass(string tableName, string schema = DefaultValues.DefaultSchema)
        {
            return $"'{EscapeStringLiteral(schema)}.{quoteString}{EscapeStringLiteral(tableName)}{quoteString}'";
        }

        public static string QualifiedIdentifier(string tableName, string schema = DefaultValues.DefaultSchema)
        {
            return $"{quoteString}{schema}{quoteString}.{quoteString}{tableName}{quoteString}";
        }

        /// <summary>
        /// Wraps a single identifier in PostgreSQL double quotes. Used by SQL generators
        /// to quote column references in compression segment/order-by lists, group-by clauses, etc.
        /// </summary>
        public static string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

        /// <summary>
        /// Escapes a value for safe embedding inside a PostgreSQL single-quoted string literal
        /// by doubling any single-quote characters it contains.
        /// </summary>
        /// <param name="value">The raw value to escape.</param>
        /// <returns>The value with every <c>'</c> replaced by <c>''</c>.</returns>
        public static string EscapeStringLiteral(string value) => value.Replace("'", "''");

        /// <summary>
        /// Formats a time value that follows the hypertable's time-column type: plain numeric
        /// strings target integer time columns and are emitted with an explicit <c>::bigint</c>
        /// cast, all other values are emitted as an escaped <c>INTERVAL</c> literal.
        /// </summary>
        /// <param name="value">The raw interval string or numeric string.</param>
        /// <returns>A SQL fragment: <c>{value}::bigint</c> or <c>INTERVAL '{value}'</c>.</returns>
        public static string IntervalOrBigint(string value)
            => long.TryParse(value, out _)
                ? $"{value}::bigint"
                : $"INTERVAL '{EscapeStringLiteral(value)}'";

        /// <summary>
        /// Formats a <see cref="DateTime"/> as an ISO 8601 UTC string for use in PostgreSQL
        /// timestamp literals, converting to UTC first to avoid ambiguity.
        /// </summary>
        /// <param name="value">The timestamp to format.</param>
        /// <returns>An ISO 8601 string in UTC, suitable for embedding in a PostgreSQL string literal.</returns>
        public static string FormatTimestamp(DateTime value)
            => value.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The prefix that turns a skip message into a SQL comment statement. Shared by every
        /// emitter of skip comments and by the migrations SQL generator that detects them.
        /// </summary>
        internal const string SkipCommentMarker = "-- ";

        /// <summary>
        /// Formats a skip message as a SQL comment statement.
        /// </summary>
        internal static string SkipComment(string skipMessage) => $"{SkipCommentMarker}{skipMessage}";

        /// <summary>
        /// Applies the edition policy to Community-only statements. For the Community edition
        /// (default) the statements are returned unchanged. When the provider is configured for
        /// the Apache edition (<c>UseApacheEdition()</c>), the statements are replaced by a single
        /// SQL comment carrying <paramref name="skipMessage"/>, so the skipped feature stays
        /// visible in scripted migration output. An empty input stays empty on both editions.
        /// </summary>
        /// <param name="statements">The Community-only statements.</param>
        /// <param name="skipMessage">The comment text emitted in place of the statements on the Apache edition.</param>
        /// <param name="isApacheEdition">Whether the provider targets the Apache edition.</param>
        internal static List<string> SkipOnApacheEdition(List<string> statements, string skipMessage, bool isApacheEdition)
        {
            if (statements.Count == 0 || !isApacheEdition)
            {
                return statements;
            }

            return [SkipComment(skipMessage)];
        }
    }
}
