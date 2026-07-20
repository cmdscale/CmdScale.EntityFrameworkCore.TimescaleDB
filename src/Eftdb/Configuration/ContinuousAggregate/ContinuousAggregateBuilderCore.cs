using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
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
