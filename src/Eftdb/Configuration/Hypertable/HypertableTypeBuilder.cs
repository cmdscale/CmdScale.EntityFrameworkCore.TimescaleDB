using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Provides extension methods for configuring TimescaleDB hypertables using the EF Core Fluent API.
    /// </summary>
    public static class HypertableTypeBuilder
    {
        /// <summary>
        /// Configures the entity as a TimescaleDB hypertable, specifying the primary time column.
        /// </summary>
        /// <remarks>
        /// This is the essential first step to enable TimescaleDB features for an entity.
        /// It corresponds to the `create_hypertable` function in PostgreSQL.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="timePropertyExpression">A lambda expression representing the time column (e.g., `x => x.Timestamp`).</param>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, object>> timePropertyExpression) where TEntity : class
        {
            string propertyName = GetPropertyName(timePropertyExpression);

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.IsHypertable, true);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, propertyName);

            return entityTypeBuilder;
        }

        /// <summary>
        /// Adds an additional partitioning dimension to the hypertable.
        /// </summary>
        /// <remarks>
        /// This method can be called multiple times to add several dimensions (hash or range).
        /// These are often called "space" dimensions and are used to partition data within the same time interval,
        /// which can improve performance by enabling parallelism and query constraints.
        /// This corresponds to the `add_dimension` function.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="dimension">A <see cref="Dimension"/> object defining the partitioning configuration.</param>
        public static EntityTypeBuilder<TEntity> HasDimension<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Dimension dimension) where TEntity : class
        {
            // Find existing dimensions annotations
            IAnnotation? existingAnnotation = entityTypeBuilder.Metadata.FindAnnotation(HypertableAnnotations.AdditionalDimensions);

            // Deserialize existing dimensions or create a new list
            List<Dimension> dimensions;
            if (existingAnnotation?.Value is string json)
            {
                dimensions = JsonSerializer.Deserialize<List<Dimension>>(json) ?? [];
            }
            else
            {
                dimensions = [];
            }

            // Add new dimension to the list and serialize back to JSON
            dimensions.Add(dimension);
            string updatedJson = JsonSerializer.Serialize(dimensions);

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.AdditionalDimensions, updatedJson);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Sets the time interval for each chunk of the hypertable.
        /// </summary>
        /// <remarks>
        /// This is a critical performance-tuning parameter. The interval should be chosen based on your data ingestion rate and query patterns.
        /// If not specified, TimescaleDB uses a default value (e.g., 7 days).
        /// Example values: <c>"1 day"</c>, <c>"12 hours"</c>, <c>"1 month"</c>, <c>"86400000"</c>.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="interval">A string representing a PostgreSQL interval.</param>
        public static EntityTypeBuilder<TEntity> WithChunkTimeInterval<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string interval) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkTimeInterval, interval);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Enables chunk skipping for the specified columns on a compressed hypertable.
        /// </summary>
        /// <remarks>
        /// Chunk skipping significantly improves query performance by allowing the query planner to avoid reading chunks
        /// whose data ranges do not match the query's WHERE clause.
        /// Note: Calling this method will implicitly enable compression on the hypertable, as chunk skipping only applies to compressed chunks.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="chunkSkipColumns">A list of lambda expressions representing the columns to enable chunk skipping on.</param>
        public static EntityTypeBuilder<TEntity> WithChunkSkipping<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params Expression<Func<TEntity, object>>[] chunkSkipColumns) where TEntity : class
        {
            string[] columnNames = [.. chunkSkipColumns.Select(GetPropertyName)];
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkSkipColumns, string.Join(",", columnNames));
            return entityTypeBuilder;
        }

        /// <summary>
        /// Enables or disables TimescaleDB's native columnar compression on the hypertable.
        /// </summary>
        /// <remarks>
        /// Compression can lead to significant storage savings (up to 90%+) and faster analytical queries.
        /// It's typically applied to older chunks of data via a compression policy.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="enable">A boolean indicating whether to enable compression. Defaults to <c>true</c>.</param>
        public static EntityTypeBuilder<TEntity> EnableCompression<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            bool enable = true) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, enable);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Specifies the columns to group by when compressing the hypertable (SegmentBy).
        /// </summary>
        /// <remarks>
        /// Valid settings for <c>timescaledb.compress_segmentby</c>.
        /// Columns used for segmenting are not compressed themselves but are used as keys to group rows.
        /// Good candidates are columns with low cardinality (e.g., "device_id", "tenant_id").
        /// </remarks>
        public static EntityTypeBuilder<TEntity> WithCompressionSegmentBy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params Expression<Func<TEntity, object>>[] segmentByColumns) where TEntity : class
        {
            string[] columnNames = [.. segmentByColumns.Select(GetPropertyName)];

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSegmentBy, string.Join(", ", columnNames));
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);

            return entityTypeBuilder;
        }

        /// <summary>
        /// Specifies the columns to order by within each compressed segment using explicit OrderBy definitions.
        /// </summary>
        /// <remarks>
        /// Uses the <see cref="OrderByBuilder"/> to define direction and null handling.
        /// Example: <c>.WithCompressionOrderBy(OrderByBuilder.For&lt;T&gt;(x => x.Time).Descending())</c>
        /// </remarks>
        public static EntityTypeBuilder<TEntity> WithCompressionOrderBy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params OrderBy[] orderByRules) where TEntity : class
        {
            string annotationValue = string.Join(", ", orderByRules.Select(r => r.ToSql()));

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionOrderBy, annotationValue);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);

            return entityTypeBuilder;
        }

        /// <summary>
        /// Specifies the columns to order by within each compressed segment using the OrderBySelector.
        /// </summary>
        /// <remarks>
        /// Provides a simplified syntax for defining order.
        /// Example: <c>.WithCompressionOrderBy(s => [s.ByDescending(x => x.Time), s.By(x => x.Value)])</c>
        /// </remarks>
        public static EntityTypeBuilder<TEntity> WithCompressionOrderBy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Func<OrderBySelector<TEntity>, IEnumerable<OrderBy>> orderSelector) where TEntity : class
        {
            OrderBySelector<TEntity> selector = new();
            IEnumerable<OrderBy> rules = orderSelector(selector);

            return entityTypeBuilder.WithCompressionOrderBy([.. rules]);
        }

        /// <summary>
        /// Specifies whether existing data should be migrated when converting a table to a hypertable.
        /// </summary>
        /// <remarks>
        /// When converting an existing table to a hypertable, this parameter controls whether existing data
        /// is migrated into chunks. If set to false, only new data will be stored in chunks.
        /// Defaults to <c>false</c> to match TimescaleDB's default behavior.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="migrateData">A boolean indicating whether to migrate existing data. Defaults to <c>true</c>.</param>
        public static EntityTypeBuilder<TEntity> WithMigrateData<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            bool migrateData = true) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.MigrateData, migrateData);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Extracts the property name from a member access lambda expression.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="propertyExpression">The expression to parse.</param>
        /// <returns>The name of the property.</returns>
        /// <exception cref="ArgumentException">Thrown if the expression is not a valid property expression.</exception>
        private static string GetPropertyName<TEntity>(Expression<Func<TEntity, object>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            // This handles cases where the property is a value type (e.g., DateTime)
            if (propertyExpression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
                return unaryMemberExpression.Member.Name;
            }

            throw new ArgumentException("Expression is not a valid property expression.", nameof(propertyExpression));
        }
    }
}
