using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Extension methods for configuring an entity as a TimescaleDB continuous aggregate.
    /// </summary>
    public static class ContinuousAggregateTypeBuilder
    {
        /// <summary>
        /// Configures the entity as a TimescaleDB continuous aggregate.
        /// </summary>
        /// <typeparam name="TEntity">The continuous aggregate entity type.</typeparam>
        /// <typeparam name="TSourceEntity">The source hypertable entity type.</typeparam>
        /// <param name="entityTypeBuilder">The entity type builder.</param>
        /// <param name="materializedViewName">The name of the materialized view.</param>
        /// <param name="timeBucketWidth">The time bucket width interval (e.g., "1 hour", "1 day").</param>
        /// <param name="propertyExpression">Expression selecting the time column from the source entity.</param>
        /// <param name="timeBucketGroupBy">Whether to include time_bucket in GROUP BY clause.</param>
        /// <param name="chunkInterval">Optional chunk interval for the continuous aggregate.</param>
        /// <returns>A builder for further continuous aggregate configuration.</returns>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, DateTime>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <inheritdoc cref="IsContinuousAggregate{TEntity, TSourceEntity}(EntityTypeBuilder{TEntity}, string, string, Expression{Func{TSourceEntity, DateTime}}, bool, string)"/>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, DateTimeOffset>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <inheritdoc cref="IsContinuousAggregate{TEntity, TSourceEntity}(EntityTypeBuilder{TEntity}, string, string, Expression{Func{TSourceEntity, DateTime}}, bool, string)"/>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, DateOnly>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <inheritdoc cref="IsContinuousAggregate{TEntity, TSourceEntity}(EntityTypeBuilder{TEntity}, string, string, Expression{Func{TSourceEntity, DateTime}}, bool, string)"/>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, long>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <inheritdoc cref="IsContinuousAggregate{TEntity, TSourceEntity}(EntityTypeBuilder{TEntity}, string, string, Expression{Func{TSourceEntity, DateTime}}, bool, string)"/>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, int>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <inheritdoc cref="IsContinuousAggregate{TEntity, TSourceEntity}(EntityTypeBuilder{TEntity}, string, string, Expression{Func{TSourceEntity, DateTime}}, bool, string)"/>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, short>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <summary>
        /// Configures the entity as a TimescaleDB continuous aggregate using a time column of any mapped type.
        /// </summary>
        /// <typeparam name="TEntity">The continuous aggregate entity type.</typeparam>
        /// <typeparam name="TSourceEntity">The source hypertable entity type.</typeparam>
        /// <typeparam name="TProperty">The .NET type of the source time column.</typeparam>
        /// <param name="entityTypeBuilder">The entity type builder.</param>
        /// <param name="materializedViewName">The name of the materialized view.</param>
        /// <param name="timeBucketWidth">The time bucket width interval (e.g., "1 hour", "1 day").</param>
        /// <param name="propertyExpression">Expression selecting the time column from the source entity.</param>
        /// <param name="timeBucketGroupBy">Whether to include time_bucket in GROUP BY clause.</param>
        /// <param name="chunkInterval">Optional chunk interval for the continuous aggregate.</param>
        /// <returns>A builder for further continuous aggregate configuration.</returns>
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity, TProperty>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, TProperty>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chunkInterval = null)
            where TEntity : class
            where TSourceEntity : class
            => IsContinuousAggregateCore(entityTypeBuilder, materializedViewName, timeBucketWidth, propertyExpression, timeBucketGroupBy, chunkInterval);

        /// <summary>
        /// Configures the entity as a TimescaleDB continuous aggregate using string column references.
        /// </summary>
        /// <typeparam name="TEntity">The continuous aggregate entity type.</typeparam>
        /// <param name="entityTypeBuilder">The entity type builder.</param>
        /// <param name="materializedViewName">The name of the materialized view.</param>
        /// <param name="parentName">The name of the source hypertable entity or its database table name.</param>
        /// <param name="timeBucketWidth">The time bucket width interval (e.g., "1 hour", "7 days").</param>
        /// <param name="timeBucketSourceColumn">The source time column name on the source hypertable.</param>
        /// <returns>A builder for further continuous aggregate configuration.</returns>
        public static ContinuousAggregateStringBuilder<TEntity> IsContinuousAggregate<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string parentName,
            string timeBucketWidth,
            string timeBucketSourceColumn)
            where TEntity : class
        {
            // Preserve any schema already set on the entity (e.g., by EF Core's standard scaffolding before this call).
            entityTypeBuilder.ToView(materializedViewName, entityTypeBuilder.Metadata.GetViewSchema());
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, materializedViewName);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ParentName, parentName);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, timeBucketWidth);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, timeBucketSourceColumn);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy, true);
            return new ContinuousAggregateStringBuilder<TEntity>(entityTypeBuilder);
        }

        private static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregateCore<TEntity, TSourceEntity, TProperty>(
            EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materializedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, TProperty>> propertyExpression,
            bool timeBucketGroupBy,
            string? chunkInterval)
            where TEntity : class
            where TSourceEntity : class
        {
            // Configure the entity to map to a view instead of a table
            // This prevents EF Core from trying to create a table for the continuous aggregate
            entityTypeBuilder.ToView(materializedViewName);

            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, materializedViewName);

            string parentName = typeof(TSourceEntity).Name;
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ParentName, parentName);

            string timeBucketSourceColumn = ExpressionHelper.GetPropertyName(propertyExpression);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, timeBucketSourceColumn);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, timeBucketWidth);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy, timeBucketGroupBy);

            if (!string.IsNullOrEmpty(chunkInterval))
            {
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ChunkInterval, chunkInterval);
            }

            return new ContinuousAggregateBuilder<TEntity, TSourceEntity>(entityTypeBuilder);
        }
    }
}
