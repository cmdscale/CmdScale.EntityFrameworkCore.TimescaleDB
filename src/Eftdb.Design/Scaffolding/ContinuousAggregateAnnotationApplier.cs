using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ContinuousAggregateScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies continuous aggregate annotations to scaffolded database views.
    /// Note: Continuous aggregates in TimescaleDB are materialized views, so they appear as tables/views in scaffolding.
    /// </summary>
    public sealed class ContinuousAggregateAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not ContinuousAggregateInfo info)
            {
                throw new ArgumentException($"Expected {nameof(ContinuousAggregateInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            // Mark as a continuous aggregate view
            table[ContinuousAggregateAnnotations.MaterializedViewName] = info.MaterializedViewName;
            table[ContinuousAggregateAnnotations.ParentName] = info.SourceHypertableName;
            table[ContinuousAggregateAnnotations.MaterializedOnly] = info.MaterializedOnly;

            if (!string.IsNullOrEmpty(info.ChunkInterval))
            {
                table[ContinuousAggregateAnnotations.ChunkInterval] = info.ChunkInterval;
            }

            // Capture the catalog's view body so the runtime extractor + generator can
            // round-trip CREATE MATERIALIZED VIEW without needing structured time_bucket /
            // aggregate / group_by annotations (which the scaffolder cannot reliably
            // recover from the catalog).
            table[ContinuousAggregateAnnotations.ViewDefinition] = info.ViewDefinition;
        }
    }
}
