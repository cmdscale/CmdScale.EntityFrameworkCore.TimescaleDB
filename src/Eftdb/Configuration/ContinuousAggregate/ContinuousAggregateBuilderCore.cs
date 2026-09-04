using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Shared annotation-writing logic for <see cref="ContinuousAggregateBuilder{TEntity, TSourceEntity}"/>
    /// and <see cref="ContinuousAggregateStringBuilder{TEntity}"/>. Operates on the non-generic
    /// <see cref="EntityTypeBuilder"/> so both public builders stay thin delegating wrappers that cannot
    /// drift apart.
    /// </summary>
    internal static class ContinuousAggregateBuilderCore
    {
        public static void WithNoData(EntityTypeBuilder builder, bool withNoData)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.WithNoData, withNoData);

        public static void CreateGroupIndexes(EntityTypeBuilder builder, bool createGroupIndexes)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes, createGroupIndexes);

        public static void MaterializedOnly(EntityTypeBuilder builder, bool materializedOnly)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedOnly, materializedOnly);

        public static void WithChunkInterval(EntityTypeBuilder builder, string chunkInterval)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.ChunkInterval, chunkInterval);

        public static void Where(EntityTypeBuilder builder, string whereClause)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.WhereClause, whereClause);

        /// <summary>
        /// Designates the model property that represents the bucket column, so the view's
        /// bucket alias derives from that property's mapped column name rather than the
        /// hard-coded <c>time_bucket</c>.
        /// </summary>
        public static void WithTimeBucketProperty(EntityTypeBuilder builder, string propertyName)
            => builder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketTargetProperty, propertyName);

        /// <summary>
        /// Enables or disables columnstore (compression) on the continuous aggregate materialized view.
        /// Maps to <c>ALTER MATERIALIZED VIEW ... SET (timescaledb.compress = true)</c>.
        /// Enabling compression is a prerequisite for adding a compression policy to a continuous aggregate.
        /// </summary>
        public static void EnableCompression(EntityTypeBuilder builder, bool enable)
            => builder.HasAnnotation(HypertableAnnotations.EnableCompression, enable);

        /// <summary>
        /// Sets the segment-by columns for compression on the continuous aggregate.
        /// Implicitly enables compression. Maps to <c>timescaledb.compress_segmentby</c>.
        /// </summary>
        public static void WithCompressionSegmentBy(EntityTypeBuilder builder, string segmentBy)
        {
            builder.HasAnnotation(HypertableAnnotations.CompressionSegmentBy, segmentBy);
            builder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
        }

        /// <summary>
        /// Sets the order-by clause for compression on the continuous aggregate.
        /// Implicitly enables compression. Maps to <c>timescaledb.compress_orderby</c>.
        /// </summary>
        public static void WithCompressionOrderBy(EntityTypeBuilder builder, string orderBy)
        {
            builder.HasAnnotation(HypertableAnnotations.CompressionOrderBy, orderBy);
            builder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
        }

        /// <summary>
        /// Appends an aggregate mapping in the <c>"alias:function:sourceColumn"</c> annotation format.
        /// An alias already present is left unchanged.
        /// </summary>
        public static void AddAggregateFunction(EntityTypeBuilder builder, string alias, string sourceColumn, EAggregateFunction function)
        {
            IAnnotation? annotation = builder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions);
            List<string> aggregateFunctions = annotation?.Value as List<string> ?? [];

            if (aggregateFunctions.Any(x => x.StartsWith(alias + ":")))
            {
                return;
            }

            aggregateFunctions.Add($"{alias}:{function}:{sourceColumn}");
            builder.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
        }

        /// <summary>
        /// Appends a GROUP BY column or raw SQL expression. Duplicates are ignored.
        /// </summary>
        public static void AddGroupByColumn(EntityTypeBuilder builder, string groupByExpression)
        {
            IAnnotation? annotation = builder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            List<string> groupByColumns = annotation?.Value as List<string> ?? [];

            if (groupByColumns.Contains(groupByExpression))
            {
                return;
            }

            groupByColumns.Add(groupByExpression);
            builder.HasAnnotation(ContinuousAggregateAnnotations.GroupByColumns, groupByColumns);
        }
    }
}
