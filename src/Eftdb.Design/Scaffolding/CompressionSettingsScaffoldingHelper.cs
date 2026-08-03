using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Shared helper for reading compression settings for scaffolding extractors.
    /// </summary>
    internal static class CompressionSettingsScaffoldingHelper
    {
        /// <summary>
        /// Reads all rows from <c>timescaledb_information.compression_settings</c> and invokes
        /// <paramref name="resolveKey"/> for each row to map the raw <c>(schema, name)</c> key to
        /// the consumer's dictionary key. Rows for which <paramref name="resolveKey"/> returns
        /// <see langword="false"/> are silently skipped.
        /// </summary>
        /// <param name="connection">An open database connection.</param>
        /// <param name="resolveKey">
        /// A callback that receives the raw <c>(schema, name)</c> key from the view and returns
        /// <see langword="true"/> plus the resolved consumer key when the row should be processed.
        /// </param>
        /// <param name="applyRow">
        /// A callback invoked for each accepted row. Receives the resolved consumer key, the column name,
        /// whether this is a segment-by entry, and (when it is an order-by entry) the <c>isAscending</c>
        /// and <c>isNullsFirst</c> flags. Both the segment-by and order-by checks are independent: a row
        /// may contribute to either or both lists, matching the raw view behaviour.
        /// </param>
        internal static void ReadCompressionSettings(
            DbConnection connection,
            Func<(string Schema, string Name), ((string, string) Key, bool Accepted)> resolveKey,
            Action<(string, string), string, bool, bool, bool, bool> applyRow)
        {
            using DbCommand command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    hypertable_schema,
                    hypertable_name,
                    attname,
                    segmentby_column_index,
                    orderby_column_index,
                    orderby_asc,
                    orderby_nullsfirst
                FROM timescaledb_information.compression_settings
                ORDER BY hypertable_schema, hypertable_name, segmentby_column_index, orderby_column_index;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string rawSchema = reader.GetString(0);
                string rawName = reader.GetString(1);
                string columnName = reader.GetString(2);

                ((string, string) resolvedKey, bool accepted) = resolveKey((rawSchema, rawName));
                if (!accepted)
                {
                    continue;
                }

                bool isSegmentBy = !reader.IsDBNull(3);
                bool isOrderBy = !reader.IsDBNull(4);

                bool isAscending = isOrderBy && reader.GetBoolean(5);
                bool isNullsFirst = isOrderBy && reader.GetBoolean(6);

                applyRow(resolvedKey, columnName, isSegmentBy, isOrderBy, isAscending, isNullsFirst);
            }
        }

        /// <summary>
        /// Detects whether <c>timescaledb_information.hypertable_columnstore_settings</c> exists
        /// (available from TimescaleDB 2.18) and, when present, reads segmentby and orderby lists
        /// for each hypertable using the <paramref name="resolveKey"/> callback. Returns
        /// <see langword="true"/> when the view was used so the caller can skip the legacy fallback.
        /// </summary>
        /// <param name="connection">An open database connection.</param>
        /// <param name="resolveKey">
        /// A callback that receives the raw <c>(schema, name)</c> key from the view and returns
        /// <see langword="true"/> plus the resolved consumer key when the row should be processed.
        /// </param>
        /// <param name="applySegmentBy">
        /// A callback invoked for each segment-by column name of an accepted hypertable.
        /// </param>
        /// <param name="applyOrderBy">
        /// A callback invoked for each order-by entry of an accepted hypertable, already in
        /// canonical <c>column ASC|DESC [NULLS FIRST|LAST]</c> form.
        /// </param>
        internal static bool TryReadCompressionSettingsFromColumnstoreView(
            DbConnection connection,
            Func<(string Schema, string Name), ((string, string) Key, bool Accepted)> resolveKey,
            Action<(string, string), string> applySegmentBy,
            Action<(string, string), string> applyOrderBy)
        {
            using (DbCommand checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = @"
                    SELECT EXISTS (
                        SELECT 1
                        FROM information_schema.views
                        WHERE table_schema = 'timescaledb_information'
                          AND table_name = 'hypertable_columnstore_settings'
                    );";

                object? exists = checkCommand.ExecuteScalar();
                if (exists is not true)
                {
                    return false;
                }
            }

            using DbCommand command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    ht.schema_name,
                    ht.table_name,
                    hcs.segmentby,
                    hcs.orderby
                FROM timescaledb_information.hypertable_columnstore_settings AS hcs
                JOIN _timescaledb_catalog.hypertable AS ht
                    ON format('%I.%I', ht.schema_name, ht.table_name)::regclass = hcs.hypertable;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string rawSchema = reader.GetString(0);
                string rawName = reader.GetString(1);

                ((string, string) resolvedKey, bool accepted) = resolveKey((rawSchema, rawName));
                if (!accepted)
                {
                    continue;
                }

                if (!reader.IsDBNull(2))
                {
                    string segmentByText = reader.GetString(2);
                    foreach (string col in segmentByText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        applySegmentBy(resolvedKey, col);
                    }
                }

                if (!reader.IsDBNull(3))
                {
                    string orderByText = reader.GetString(3);
                    foreach (string token in orderByText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        applyOrderBy(resolvedKey, ParseColumnstoreOrderByToken(token));
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the ORDER BY entry string from the supplied direction and nulls properties.
        /// </summary>
        internal static string BuildOrderByEntry(string columnName, bool isAscending, bool isNullsFirst)
        {
            string direction = isAscending ? "ASC" : "DESC";
            bool isDefaultNulls = (isAscending && !isNullsFirst) || (!isAscending && isNullsFirst);
            string nulls = isDefaultNulls ? "" : (isNullsFirst ? " NULLS FIRST" : " NULLS LAST");

            return $"{columnName} {direction}{nulls}";
        }

        /// <summary>
        /// Parses a single ORDER BY token from <c>timescaledb_information.hypertable_columnstore_settings</c>
        /// into the canonical <c>BuildOrderByEntry</c> format: <c>column ASC|DESC [NULLS FIRST|NULLS LAST]</c>.
        /// </summary>
        internal static string ParseColumnstoreOrderByToken(string token)
        {
            string columnName;
            string suffix;

            if (token.Length > 0 && token[0] == '"')
            {
                // Quoted identifier: scan for the closing double-quote, handling doubled "" escapes.
                int end = 1;
                while (end < token.Length)
                {
                    if (token[end] == '"')
                    {
                        if (end + 1 < token.Length && token[end + 1] == '"')
                        {
                            // Escaped double-quote inside identifier — skip both
                            end += 2;
                        }
                        else
                        {
                            // Closing quote found
                            break;
                        }
                    }
                    else
                    {
                        end++;
                    }
                }

                columnName = token[1..end].Replace("\"\"", "\"");
                suffix = end + 1 < token.Length ? token[(end + 1)..] : string.Empty;
            }
            else
            {
                int space = token.IndexOf(' ');
                if (space < 0)
                {
                    columnName = token;
                    suffix = string.Empty;
                }
                else
                {
                    columnName = token[..space];
                    suffix = token[(space + 1)..];
                }
            }

            string upper = suffix.ToUpperInvariant().Trim();

            bool isAscending = !upper.Contains("DESC");
            bool? nullsFirst = upper.Contains("NULLS FIRST") ? true : upper.Contains("NULLS LAST") ? false : null;
            bool resolvedNullsFirst = nullsFirst ?? !isAscending;

            return BuildOrderByEntry(columnName, isAscending, resolvedNullsFirst);
        }
    }
}
