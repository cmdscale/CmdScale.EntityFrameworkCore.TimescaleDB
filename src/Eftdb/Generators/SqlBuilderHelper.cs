using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public static class SqlBuilderHelper
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

        public static void BuildQueryString(List<string> statements, IndentedStringBuilder builder, bool suppressTransaction = false)
        {
            if (statements.Count > 0)
            {
                builder.AppendLine(".Sql(@\"");
                using (builder.Indent())
                {
                    foreach (string statement in statements)
                    {
                        builder.AppendLines(statement, skipFinalNewline: false);
                    }
                }
                if (suppressTransaction)
                {
                    builder.Append("\", suppressTransaction: true)");
                }
                else
                {
                    builder.Append("\")");
                }
            }
        }

        public static string Regclass(string tableName, string schema = DefaultValues.DefaultSchema)
        {
            return $"'{schema}.{quoteString}{tableName}{quoteString}'";
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

        // TODO: Is this being generated inside a .Sql() call or in exxtension method in the migration? Also, what about scaffolding?
        /// <summary>
        /// Wraps SQL statements in a Community Edition license-guard DO block. Statements execute
        /// only when the TimescaleDB license is not <c>apache</c>; otherwise the supplied warning
        /// is raised and the block exits without executing them.
        /// </summary>
        /// <param name="sqlStatements">The statements to execute inside the guarded block.</param>
        /// <param name="warningText">The text of the RAISE WARNING emitted on the Apache Edition path.</param>
        internal static string WrapCommunityFeatures(List<string> sqlStatements, string warningText)
        {
            StringBuilder sb = new();
            sb.AppendLine("DO $$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    license TEXT;");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    license := current_setting('timescaledb.license', true);");
            sb.AppendLine("    ");
            sb.AppendLine("    IF license IS NULL OR license != 'apache' THEN");

            foreach (string sql in sqlStatements)
            {
                string cleanSql = sql.TrimEnd(';').Replace("'", "''");
                sb.AppendLine($"        EXECUTE '{cleanSql}';");
            }

            sb.AppendLine("    ELSE");
            sb.AppendLine($"        RAISE WARNING '{warningText}';");
            sb.AppendLine("    END IF;");
            sb.AppendLine("END $$;");

            return sb.ToString();
        }
    }
}
