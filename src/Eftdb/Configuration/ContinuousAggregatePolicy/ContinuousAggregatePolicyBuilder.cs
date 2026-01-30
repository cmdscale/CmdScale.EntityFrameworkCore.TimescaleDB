using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// Provides a fluent API for configuring a TimescaleDB continuous aggregate refresh policy.
    /// This builder is returned from WithRefreshPolicy() and ensures policy-specific methods
    /// can only be called after establishing the base refresh policy configuration.
    /// </summary>
    /// <typeparam name="TEntity">The class representing the continuous aggregate view.</typeparam>
    /// <typeparam name="TSourceEntity">The class representing the source hypertable.</typeparam>
    public class ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity>
        where TEntity : class
        where TSourceEntity : class
    {
        /// <summary>
        /// Gets the underlying continuous aggregate builder.
        /// </summary>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> ContinuousAggregateBuilder { get; }

        internal ContinuousAggregatePolicyBuilder(ContinuousAggregateBuilder<TEntity, TSourceEntity> continuousAggregateBuilder)
        {
            ContinuousAggregateBuilder = continuousAggregateBuilder;
        }

        /// <summary>
        /// Gets the entity type builder for advanced configuration.
        /// </summary>
        internal EntityTypeBuilder<TEntity> EntityTypeBuilder => ContinuousAggregateBuilder.EntityTypeBuilder;

        /// <summary>
        /// Sets the initial start time for the continuous aggregate refresh policy.
        /// </summary>
        /// <param name="initialStart">The first time the policy job is scheduled to run.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithInitialStart(DateTime initialStart)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, initialStart);
            return this;
        }

        /// <summary>
        /// Configures whether to issue a notice instead of an error if the policy job already exists.
        /// </summary>
        /// <param name="ifNotExists">True to issue a notice instead of an error if job exists.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithIfNotExists(bool ifNotExists = true)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists, ifNotExists);
            return this;
        }

        /// <summary>
        /// Configures whether to override tiered read settings for the continuous aggregate refresh policy.
        /// </summary>
        /// <param name="includeTieredData">True to include tiered data, false to exclude it.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithIncludeTieredData(bool includeTieredData)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData, includeTieredData);
            return this;
        }

        /// <summary>
        /// Sets the number of buckets processed per batch transaction for the continuous aggregate refresh policy.
        /// </summary>
        /// <param name="bucketsPerBatch">The number of buckets to process per batch. Defaults to 1.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithBucketsPerBatch(int bucketsPerBatch)
        {
            if (bucketsPerBatch < 1)
                throw new ArgumentException("BucketsPerBatch must be at least 1.", nameof(bucketsPerBatch));

            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch, bucketsPerBatch);
            return this;
        }

        /// <summary>
        /// Sets the maximum number of batches per execution for the continuous aggregate refresh policy.
        /// </summary>
        /// <param name="maxBatchesPerExecution">Maximum batches per run. 0 means unlimited. Defaults to 0.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithMaxBatchesPerExecution(int maxBatchesPerExecution)
        {
            if (maxBatchesPerExecution < 0)
                throw new ArgumentException("MaxBatchesPerExecution must be 0 (unlimited) or greater.", nameof(maxBatchesPerExecution));

            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, maxBatchesPerExecution);
            return this;
        }

        /// <summary>
        /// Sets the direction of incremental refresh for the continuous aggregate refresh policy.
        /// </summary>
        /// <param name="refreshNewestFirst">True to refresh newest data first, false to refresh oldest first. Defaults to true.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithRefreshNewestFirst(bool refreshNewestFirst = true)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, refreshNewestFirst);
            return this;
        }
    }
}
