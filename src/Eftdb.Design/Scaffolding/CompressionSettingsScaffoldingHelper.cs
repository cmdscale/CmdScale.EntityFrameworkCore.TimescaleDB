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
        /// Builds the ORDER BY entry string from the supplied direction and nulls properties.
        /// </summary>
        internal static string BuildOrderByEntry(string columnName, bool isAscending, bool isNullsFirst)
        {
            string direction = isAscending ? "ASC" : "DESC";
            bool isDefaultNulls = (isAscending && !isNullsFirst) || (!isAscending && isNullsFirst);
            string nulls = isDefaultNulls ? "" : (isNullsFirst ? " NULLS FIRST" : " NULLS LAST");

            return $"{columnName} {direction}{nulls}";
        }
    }
}
