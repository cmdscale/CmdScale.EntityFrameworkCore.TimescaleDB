using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
#pragma warning disable EF1001
    /// <summary>
    /// Database model factory that extends Npgsql's scaffolding to include TimescaleDB-specific features.
    /// Handles extraction of hypertables, reorder policies, and continuous aggregates during db-first scaffolding.
    /// </summary>
    public class TimescaleDatabaseModelFactory(IDiagnosticsLogger<DbLoggerCategory.Scaffolding> logger)
        : NpgsqlDatabaseModelFactory(logger)
    {
        private readonly List<(ITimescaleFeatureExtractor Extractor, IAnnotationApplier Applier)> _features =
        [
            (new HypertableScaffoldingExtractor(), new HypertableAnnotationApplier()),
            (new ReorderPolicyScaffoldingExtractor(), new ReorderPolicyAnnotationApplier()),
            (new ContinuousAggregateScaffoldingExtractor(), new ContinuousAggregateAnnotationApplier()),
            (new ContinuousAggregatePolicyScaffoldingExtractor(), new ContinuousAggregatePolicyAnnotationApplier())
        ];

        public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
        {
            DatabaseModel databaseModel = base.Create(connection, options);

            // Extract all TimescaleDB features from the database
            List<Dictionary<(string Schema, string TableName), object>> allFeatureData = [.. _features.Select(feature => feature.Extractor.Extract(connection))];

            // Apply annotations to tables/views in the model
            foreach (DatabaseTable table in databaseModel.Tables)
            {
                if (table?.Schema == null) continue;

                (string Schema, string Name) tableKey = (table.Schema, table.Name);

                // Apply each feature's annotations if the table has that feature
                for (int i = 0; i < _features.Count; i++)
                {
                    Dictionary<(string Schema, string TableName), object> featureData = allFeatureData[i];
                    if (featureData.TryGetValue(tableKey, out object? featureInfo))
                    {
                        _features[i].Applier.ApplyAnnotations(table, featureInfo);
                    }
                }
            }

            return databaseModel;
        }
    }
#pragma warning restore EF1001
}
