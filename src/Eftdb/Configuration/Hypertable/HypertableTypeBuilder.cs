using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
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
        /// It corresponds to the <c>create_hypertable</c> function in PostgreSQL.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="timePropertyExpression">A lambda expression representing the time column (e.g., <c>x =&gt; x.Timestamp</c>).</param>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, DateTime>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <inheritdoc cref="IsHypertable{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, DateTime}})"/>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, DateTimeOffset>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <inheritdoc cref="IsHypertable{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, DateTime}})"/>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, DateOnly>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <inheritdoc cref="IsHypertable{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, DateTime}})"/>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, long>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <inheritdoc cref="IsHypertable{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, DateTime}})"/>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, int>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <inheritdoc cref="IsHypertable{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, DateTime}})"/>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, short>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        /// <summary>
        /// Configures the entity as a TimescaleDB hypertable using a time column of any mapped type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <typeparam name="TProperty">The .NET type of the time column.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="timePropertyExpression">A lambda expression representing the time column (e.g., <c>x =&gt; x.Timestamp</c>).</param>
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity, TProperty>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, TProperty>> timePropertyExpression) where TEntity : class
            => IsHypertableCore(entityTypeBuilder, timePropertyExpression);

        private static EntityTypeBuilder<TEntity> IsHypertableCore<TEntity, TProperty>(
            EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, TProperty>> timePropertyExpression) where TEntity : class
        {
            string propertyName = ExpressionHelper.GetPropertyName(timePropertyExpression);

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.IsHypertable, true);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, propertyName);

            return entityTypeBuilder;
        }

        /// <summary>
        /// Adds an additional partitioning dimension to the hypertable from a <see cref="Dimension"/> object.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="HasRangeDimension{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, object}}, string)"/>
        /// or <see cref="HasHashDimension{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, object}}, int)"/>
        /// for static configuration; they select the column with a type-safe lambda and match the scaffolder's output.
        /// This overload suits dynamic scenarios where the column name is only known at runtime.
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
            => AddDimension(entityTypeBuilder, dimension);

        private static EntityTypeBuilder<TEntity> AddDimension<TEntity>(
            EntityTypeBuilder<TEntity> entityTypeBuilder,
            Dimension dimension) where TEntity : class
        {
            IAnnotation? existingAnnotation = entityTypeBuilder.Metadata.FindAnnotation(HypertableAnnotations.AdditionalDimensions);

            List<Dimension> dimensions = existingAnnotation?.Value is string json
                ? JsonSerializer.Deserialize<List<Dimension>>(json) ?? []
                : [];

            dimensions.Add(dimension);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.AdditionalDimensions, JsonSerializer.Serialize(dimensions));

            return entityTypeBuilder;
        }

        /// <summary>
        /// Adds a range partitioning dimension to the hypertable.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="column">A lambda expression representing the column to partition by (e.g. <c>x =&gt; x.Timestamp</c>).</param>
        /// <param name="interval">The partitioning interval (e.g. <c>"1 month"</c> or an integer interval as a string).</param>
        public static EntityTypeBuilder<TEntity> HasRangeDimension<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, object>> column,
            string interval) where TEntity : class
            => AddDimension(entityTypeBuilder, Dimension.CreateRange(ExpressionHelper.GetPropertyName(column), interval));

        /// <summary>
        /// Adds a hash (space) partitioning dimension to the hypertable.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="column">A lambda expression representing the column to partition by (e.g. <c>x =&gt; x.WarehouseId</c>).</param>
        /// <param name="numberOfPartitions">The number of hash partitions.</param>
        public static EntityTypeBuilder<TEntity> HasHashDimension<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, object>> column,
            int numberOfPartitions) where TEntity : class
            => AddDimension(entityTypeBuilder, Dimension.CreateHash(ExpressionHelper.GetPropertyName(column), numberOfPartitions));

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
            string[] columnNames = [.. chunkSkipColumns.Select(ExpressionHelper.GetPropertyName)];
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
            string[] columnNames = [.. segmentByColumns.Select(ExpressionHelper.GetPropertyName)];

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
        /// Specifies the columns to order by within each compressed segment, one selector per column.
        /// </summary>
        /// <remarks>
        /// Example: <c>.WithCompressionOrderBy(s => s.ByDescending(x => x.Time), s => s.By(x => x.Value))</c>
        /// </remarks>
        public static EntityTypeBuilder<TEntity> WithCompressionOrderBy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params Func<OrderBySelector<TEntity>, OrderBy>[] orderSelectors) where TEntity : class
        {
            OrderBySelector<TEntity> selector = new();

            return entityTypeBuilder.WithCompressionOrderBy([.. orderSelectors.Select(orderSelector => orderSelector(selector))]);
        }

        /// <summary>
        /// Configures sparse indexes for the columnstore using typed <see cref="SparseIndex"/> entries.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="indexes">One or more sparse index entries to configure.</param>
        public static EntityTypeBuilder<TEntity> WithSparseIndex<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params SparseIndex[] indexes) where TEntity : class
        {
            string annotationValue = string.Join(", ", indexes.Select(i => i.ToSql()));
            return entityTypeBuilder.WithSparseIndex(annotationValue);
        }

        /// <summary>
        /// Configures sparse indexes for the columnstore using selector lambdas, one per entry.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="selectors">One or more selector functions that produce a <see cref="SparseIndex"/> per entry.</param>
        public static EntityTypeBuilder<TEntity> WithSparseIndex<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            params Func<SparseIndexSelector<TEntity>, SparseIndex>[] selectors) where TEntity : class
        {
            SparseIndexSelector<TEntity> selector = new();
            SparseIndex[] indexes = [.. selectors.Select(s => s(selector))];
            return entityTypeBuilder.WithSparseIndex(indexes);
        }

        /// <summary>
        /// Configures sparse indexes for the columnstore using a raw string.
        /// Accepts a comma-separated list of <c>bloom(column)</c> and <c>minmax(column)</c> entries.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="sparseIndex">
        /// A comma-separated list of sparse index definitions, e.g. <c>"bloom(device_id), minmax(temperature)"</c>.
        /// </param>
        public static EntityTypeBuilder<TEntity> WithSparseIndex<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string sparseIndex) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, sparseIndex ?? string.Empty);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Explicitly disables auto-created sparse indexes on the columnstore
        /// (sets <c>timescaledb.sparse_index = ''</c>).
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        public static EntityTypeBuilder<TEntity> WithoutAutoSparseIndexes<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, string.Empty);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
            return entityTypeBuilder;
        }

        /// <summary>
        /// Sets the minimum time interval to use when merging chunks during compression.
        /// The value must be a multiple of the hypertable's <c>chunk_time_interval</c>.
        /// </summary>
        /// <remarks>
        /// WARNING: Chunk merges are irreversible — decreasing the value later cannot un-merge already merged chunks.
        /// Calling this method implicitly enables compression on the hypertable.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="interval">
        /// A PostgreSQL interval string specifying the chunk merge interval, e.g. <c>"24 hours"</c>.
        /// </param>
        public static EntityTypeBuilder<TEntity> WithCompressChunkTimeInterval<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string interval) where TEntity : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(interval);

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressChunkTimeInterval, interval);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
            return entityTypeBuilder;
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

    }
}
