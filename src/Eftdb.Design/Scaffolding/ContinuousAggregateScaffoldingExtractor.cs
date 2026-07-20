using System.Data;
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
            string? ChunkInterval
        );

        public Dictionary<(string Schema, string TableName), object> Extract(DbConnection connection)
        {
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                Dictionary<(string, string), ContinuousAggregateInfo> continuousAggregates = [];

                using (DbCommand command = connection.CreateCommand())
                {
                    // Query continuous aggregates from TimescaleDB information schema
                    // This query supports TimescaleDB v2.16 and higher
                    command.CommandText = @"
                        SELECT
                            ca.view_schema,
                            ca.view_name,
                            ca.view_definition,
                            ca.hypertable_schema,
                            ca.hypertable_name,
                            ca.materialized_only,
                            dim.time_interval::text AS chunk_interval
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

                        continuousAggregates[(viewSchema, viewName)] = new ContinuousAggregateInfo(
                            MaterializedViewName: viewName,
                            Schema: viewSchema,
                            ViewDefinition: viewDefinition,
                            SourceHypertableName: hypertableName,
                            SourceSchema: hypertableSchema,
                            MaterializedOnly: materializedOnly,
                            ChunkInterval: chunkInterval
                        );
                    }
                }

                // Convert to object dictionary to match interface
                return continuousAggregates.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value
                );
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }
    }
}
