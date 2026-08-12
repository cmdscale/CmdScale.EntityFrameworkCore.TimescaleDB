using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Extracts continuous aggregate metadata from a TimescaleDB database for scaffolding.
    /// </summary>
    public sealed class ContinuousAggregateScaffoldingExtractor : ITimescaleFeatureExtractor
    {
        public sealed record ContinuousAggregateInfo(
            string MaterializedViewName,
            string Schema,
            string ViewDefinition,
            string SourceHypertableName,
            string SourceSchema,
            bool MaterializedOnly,
            string? ChunkInterval,
            bool CompressionEnabled = false,
            List<string>? CompressionSegmentBy = null,
            List<string>? CompressionOrderBy = null
        );

        public Dictionary<(string Schema, string TableName), object> Extract(DbConnection connection)
        {
            try
            {
                return ScaffoldingExtractorHelper.UsingConnection(connection, () =>
                {
                    Dictionary<(string, string), ContinuousAggregateInfo> continuousAggregates = [];

                    using (DbCommand command = connection.CreateCommand())
                    {
                        // Query continuous aggregates from TimescaleDB information schema
                        // This query supports TimescaleDB v2.16 and higher.
                        // materialization_hypertable_schema/name identify the internal materialized hypertable
                        // (_timescaledb_internal._materialized_hypertable_N) used to join compression settings.
                        command.CommandText = @"
                        SELECT
                            ca.view_schema,
                            ca.view_name,
                            ca.view_definition,
                            ca.hypertable_schema,
                            ca.hypertable_name,
                            ca.materialized_only,
                            dim.time_interval::text AS chunk_interval,
                            ca.compression_enabled,
                            ca.materialization_hypertable_schema,
                            ca.materialization_hypertable_name
                        FROM timescaledb_information.continuous_aggregates ca
                        LEFT JOIN _timescaledb_catalog.continuous_agg cagg
                            ON ca.view_schema = cagg.user_view_schema
                            AND ca.view_name = cagg.user_view_name
                        LEFT JOIN _timescaledb_catalog.hypertable mat_ht
                            ON cagg.mat_hypertable_id = mat_ht.id
                        LEFT JOIN timescaledb_information.dimensions dim
                            ON dim.hypertable_schema = mat_ht.schema_name
                            AND dim.hypertable_name = mat_ht.table_name
                            AND dim.dimension_number = 1;";

                        using DbDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            string viewSchema = reader.GetString(0);
                            string viewName = reader.GetString(1);
                            string viewDefinition = reader.GetString(2);
                            string hypertableSchema = reader.GetString(3);
                            string hypertableName = reader.GetString(4);
                            bool materializedOnly = reader.GetBoolean(5);
                            string? chunkInterval = reader.IsDBNull(6) ? null : reader.GetString(6);
                            bool compressionEnabled = !reader.IsDBNull(7) && reader.GetBoolean(7);
                            string? matSchema = reader.IsDBNull(8) ? null : reader.GetString(8);
                            string? matName = reader.IsDBNull(9) ? null : reader.GetString(9);

                            continuousAggregates[(viewSchema, viewName)] = new ContinuousAggregateInfo(
                                MaterializedViewName: viewName,
                                Schema: viewSchema,
                                ViewDefinition: viewDefinition,
                                SourceHypertableName: hypertableName,
                                SourceSchema: hypertableSchema,
                                MaterializedOnly: materializedOnly,
                                ChunkInterval: chunkInterval,
                                CompressionEnabled: compressionEnabled,
                                CompressionSegmentBy: [],
                                CompressionOrderBy: []
                            );

                            if (matSchema is not null && matName is not null)
                            {
                                _matHypertableToView[(matSchema, matName)] = (viewSchema, viewName);
                            }
                        }
                    }

                    GetCompressionConfiguration(connection, continuousAggregates);

                    // Convert to object dictionary to match interface
                    return continuousAggregates.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (object)kvp.Value
                    );
                });
            }
            finally
            {
                _matHypertableToView.Clear();
            }
        }

        private readonly Dictionary<(string, string), (string ViewSchema, string ViewName)> _matHypertableToView = [];

        private void GetCompressionConfiguration(
            DbConnection connection,
            Dictionary<(string, string), ContinuousAggregateInfo> continuousAggregates)
        {
            bool usedNewView = CompressionSettingsScaffoldingHelper.TryReadCompressionSettingsFromColumnstoreView(
                connection,
                rawKey =>
                {
                    if (!_matHypertableToView.TryGetValue(rawKey, out (string ViewSchema, string ViewName) viewKey))
                    {
                        return (default, false);
                    }

                    return ((viewKey.ViewSchema, viewKey.ViewName), true);
                },
                (key, columnName) =>
                {
                    if (continuousAggregates.TryGetValue(key, out ContinuousAggregateInfo? info))
                    {
                        info.CompressionSegmentBy?.Add(columnName);
                    }
                },
                (key, orderByEntry) =>
                {
                    if (continuousAggregates.TryGetValue(key, out ContinuousAggregateInfo? info))
                    {
                        info.CompressionOrderBy?.Add(orderByEntry);
                    }
                });

            if (usedNewView)
            {
                return;
            }

            CompressionSettingsScaffoldingHelper.ReadCompressionSettings(
                connection,
                rawKey =>
                {
                    if (!_matHypertableToView.TryGetValue(rawKey, out (string ViewSchema, string ViewName) viewKey))
                    {
                        return (default, false);
                    }

                    return ((viewKey.ViewSchema, viewKey.ViewName), true);
                },
                (key, columnName, isSegmentBy, isOrderBy, isAscending, isNullsFirst) =>
                {
                    if (!continuousAggregates.TryGetValue(key, out ContinuousAggregateInfo? info))
                    {
                        return;
                    }

                    if (isSegmentBy)
                    {
                        info.CompressionSegmentBy?.Add(columnName);
                    }

                    if (isOrderBy)
                    {
                        info.CompressionOrderBy?.Add(CompressionSettingsScaffoldingHelper.BuildOrderByEntry(columnName, isAscending, isNullsFirst));
                    }
                });
        }
    }
}
