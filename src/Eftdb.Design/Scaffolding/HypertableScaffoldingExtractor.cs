using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using System.Data.Common;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts hypertable metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    public sealed class HypertableScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record HypertableInfo(
            string TimeColumnName,
            string ChunkTimeInterval,
            bool CompressionEnabled,
            List<string> CompressionSegmentBy,
            List<string> CompressionOrderBy,
            List<string> ChunkSkipColumns,
            List<Dimension> AdditionalDimensions,
            string? CompressionSparseIndex,
            string? CompressChunkTimeInterval
        );

        public Dictionary<(string Schema, string TableName), object> Extract(DbConnection connection)
            => ScaffoldingExtractorHelper.UsingConnection(connection, () =>
            {
                Dictionary<(string, string), HypertableInfo> hypertables = [];
                Dictionary<(string, string), bool> compressionSettings = GetCompressionSettings(connection);

                GetHypertableSettings(connection, hypertables, compressionSettings);
                GetChunkSkipColumns(connection, hypertables);

                bool usedNewView = GetColumnstoreSettings(connection, hypertables);
                if (!usedNewView)
                {
                    GetCompressionConfiguration(connection, hypertables);
                }

                // Convert to object dictionary to match interface
                return hypertables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value
                );
            });

        private static Dictionary<(string, string), bool> GetCompressionSettings(DbConnection connection)
        {
            Dictionary<(string, string), bool> compressionSettings = [];
            using DbCommand command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT hypertable_schema, hypertable_name, compression_enabled
                FROM timescaledb_information.hypertables
                WHERE hypertable_schema NOT IN ({ScaffoldingExtractorHelper.TimescaleInternalSchemaExclusion});";
            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                compressionSettings[(reader.GetString(0), reader.GetString(1))] = reader.GetBoolean(2);
            }
            return compressionSettings;
        }

        private static void GetHypertableSettings(
            DbConnection connection,
            Dictionary<(string, string), HypertableInfo> hypertables,
            Dictionary<(string, string), bool> compressionSettings)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT
                    hypertable_schema,
                    hypertable_name,
                    column_name,
                    dimension_number,
                    num_partitions,
                    time_interval::text AS time_interval_text,
                    integer_interval
                FROM timescaledb_information.dimensions
                WHERE hypertable_schema NOT IN ({ScaffoldingExtractorHelper.TimescaleInternalSchemaExclusion})
                ORDER BY hypertable_schema, hypertable_name, dimension_number;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);
                string columnName = reader.GetString(2);
                int dimensionNumber = reader.GetInt32(3);

                (string schema, string name) key = (schema, name);

                // If it's the first dimension, it defines the primary hypertable settings
                if (dimensionNumber == 1)
                {
                    string chunkTimeInterval;
                    if (!reader.IsDBNull(5))
                    {
                        chunkTimeInterval = IntervalParsingHelper.NormalizeInterval(reader.GetString(5));
                    }
                    else if (!reader.IsDBNull(6))
                    {
                        chunkTimeInterval = reader.GetInt64(6).ToString();
                    }
                    else
                    {
                        chunkTimeInterval = DefaultValues.ChunkTimeInterval;
                    }

                    bool compressionEnabled = compressionSettings.TryGetValue(key, out bool enabled) && enabled;

                    hypertables[key] = new HypertableInfo(
                        TimeColumnName: columnName,
                        ChunkTimeInterval: chunkTimeInterval,
                        CompressionEnabled: compressionEnabled,
                        CompressionSegmentBy: [],
                        CompressionOrderBy: [],
                        ChunkSkipColumns: [],
                        AdditionalDimensions: [],
                        CompressionSparseIndex: null,
                        CompressChunkTimeInterval: null
                    );
                }
                // For all other dimensions, add them to the AdditionalDimensions list
                else
                {
                    if (hypertables.TryGetValue(key, out HypertableInfo? info))
                    {
                        Dimension dimension;

                        if (!reader.IsDBNull(4) && reader.GetInt32(4) > 0)
                        {
                            // Hash dimension (space partitioning)
                            dimension = Dimension.CreateHash(columnName, reader.GetInt32(4));
                        }
                        else if (!reader.IsDBNull(5))
                        {
                            dimension = Dimension.CreateRange(columnName, IntervalParsingHelper.NormalizeInterval(reader.GetString(5)));
                        }
                        else if (!reader.IsDBNull(6))
                        {
                            // Integer-based range dimension
                            long integerInterval = reader.GetInt64(6);
                            dimension = Dimension.CreateRange(columnName, integerInterval.ToString());
                        }
                        else continue;

                        info.AdditionalDimensions.Add(dimension);
                    }
                }
            }
        }

        private static void GetChunkSkipColumns(DbConnection connection, Dictionary<(string, string), HypertableInfo> hypertables)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    h.schema_name,
                    h.table_name,
                    ccs.column_name
                FROM _timescaledb_catalog.chunk_column_stats AS ccs
                JOIN _timescaledb_catalog.hypertable AS h ON ccs.hypertable_id = h.id;";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);
                string columnName = reader.GetString(2);

                if (hypertables.TryGetValue((schema, name), out HypertableInfo? info))
                {
                    info.ChunkSkipColumns.Add(columnName);
                }
            }
        }

        /// <summary>
        /// Reads segmentby, orderby, sparse_index, and compress_interval_length from
        /// <c>timescaledb_information.hypertable_columnstore_settings</c> in a single query.
        /// Returns <see langword="true"/> when the view exists and was used; <see langword="false"/>
        /// when the view is absent (TimescaleDB older than 2.18) so the caller can fall back to
        /// <see cref="GetCompressionConfiguration"/>.
        /// </summary>
        private static bool GetColumnstoreSettings(DbConnection connection, Dictionary<(string, string), HypertableInfo> hypertables)
        {
            if (!ScaffoldingExtractorHelper.ViewExists(connection, "timescaledb_information", "hypertable_columnstore_settings"))
            {
                return false;
            }

            using DbCommand command = connection.CreateCommand();

            command.CommandText = $@"
                SELECT
                    ht.schema_name,
                    ht.table_name,
                    hcs.segmentby,
                    hcs.orderby,
                    hcs.index,
                    hcs.compress_interval_length
                FROM timescaledb_information.hypertable_columnstore_settings AS hcs
                JOIN _timescaledb_catalog.hypertable AS ht
                    ON format('%I.%I', ht.schema_name, ht.table_name)::regclass = hcs.hypertable
                WHERE ht.schema_name NOT IN ({ScaffoldingExtractorHelper.TimescaleInternalSchemaExclusion});";

            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);

                if (!hypertables.TryGetValue((schema, name), out HypertableInfo? info))
                {
                    continue;
                }

                List<string> segmentBy = [];
                string? segmentByText = reader.IsDBNull(2) ? null : reader.GetString(2);
                if (segmentByText is not null)
                {
                    foreach (string col in segmentByText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        segmentBy.Add(col);
                    }
                }

                List<string> orderBy = [];
                string? orderByText = reader.IsDBNull(3) ? null : reader.GetString(3);
                if (orderByText is not null)
                {
                    foreach (string token in orderByText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        orderBy.Add(CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token));
                    }
                }

                string? sparseIndexRaw = reader.IsDBNull(4) ? null : reader.GetString(4);
                string? sparseIndex = sparseIndexRaw is not null ? ParseSparseIndexJson(sparseIndexRaw) : null;

                string? compressChunkTimeInterval = reader.IsDBNull(5) ? null : IntervalParsingHelper.NormalizeInterval(reader.GetString(5));

                bool updated = segmentBy.Count > 0 || orderBy.Count > 0
                    || sparseIndex != null || compressChunkTimeInterval != null;

                if (updated)
                {
                    hypertables[(schema, name)] = info with
                    {
                        CompressionSegmentBy = segmentBy.Count > 0 ? segmentBy : info.CompressionSegmentBy,
                        CompressionOrderBy = orderBy.Count > 0 ? orderBy : info.CompressionOrderBy,
                        CompressionSparseIndex = sparseIndex ?? info.CompressionSparseIndex,
                        CompressChunkTimeInterval = compressChunkTimeInterval ?? info.CompressChunkTimeInterval,
                    };
                }
            }

            return true;
        }

        /// <summary>
        /// Fallback path for TimescaleDB servers older than 2.18 that do not have
        /// <c>timescaledb_information.hypertable_columnstore_settings</c>. Reads segmentby and
        /// orderby from the legacy row-per-column <c>timescaledb_information.compression_settings</c> view.
        /// </summary>
        private static void GetCompressionConfiguration(DbConnection connection, Dictionary<(string, string), HypertableInfo> hypertables)
        {
            CompressionSettingsScaffoldingHelper.ReadCompressionSettings(
                 connection,
                 rawKey =>
                 {
                     bool accepted = hypertables.ContainsKey(rawKey);
                     return (rawKey, accepted);
                 },
                 (key, columnName, isSegmentBy, isOrderBy, isAscending, isNullsFirst) =>
                 {
                     if (!hypertables.TryGetValue(key, out HypertableInfo? info))
                     {
                         return;
                     }

                     if (isSegmentBy)
                     {
                         info.CompressionSegmentBy.Add(columnName);
                     }

                     if (isOrderBy)
                     {
                         info.CompressionOrderBy.Add(CompressionSettingsScaffoldingHelper.BuildOrderByEntry(columnName, isAscending, isNullsFirst));
                     }
                 });
        }

        /// <summary>
        /// Parses the <c>index</c> JSON array from <c>hypertable_columnstore_settings</c> and
        /// reconstructs the canonical sparse-index annotation string used by the runtime library.
        /// </summary>
        /// <returns>
        /// A comma-separated string of entries in <c>type(column)</c> form (e.g.
        /// <c>"bloom(device_id), minmax(value)"</c>), or <see langword="null"/> when no
        /// user-configured entries are present in the array.
        /// </returns>
        private static string? ParseSparseIndexJson(string indexJson)
        {
            List<string> entries = [];

            using JsonDocument doc = JsonDocument.Parse(indexJson);
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("source", out JsonElement sourceElement) ||
                    !string.Equals(sourceElement.GetString(), "config", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!element.TryGetProperty("type", out JsonElement typeElement))
                {
                    continue;
                }

                string? type = typeElement.GetString();
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                if (!element.TryGetProperty("column", out JsonElement columnElement))
                {
                    continue;
                }

                string columns;
                if (columnElement.ValueKind == JsonValueKind.Array)
                {
                    List<string> cols = [];
                    foreach (JsonElement col in columnElement.EnumerateArray())
                    {
                        string? colName = col.GetString();
                        if (!string.IsNullOrEmpty(colName))
                        {
                            cols.Add(colName);
                        }
                    }
                    columns = string.Join(",", cols);
                }
                else
                {
                    columns = columnElement.GetString() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(columns))
                {
                    entries.Add($"{type}({columns})");
                }
            }

            return entries.Count > 0 ? string.Join(", ", entries) : null;
        }

    }
}
