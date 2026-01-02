using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// Extension methods for adding refresh policy configuration to continuous aggregates.
    /// </summary>
    public static class ContinuousAggregateBuilderPolicyExtensions
    {
        /// <summary>
        /// Configures a continuous aggregate refresh policy that automatically refreshes the materialized view on a schedule.
        /// </summary>
        /// <typeparam name="TEntity">The continuous aggregate entity type.</typeparam>
        /// <typeparam name="TSourceEntity">The source hypertable entity type.</typeparam>
        /// <param name="builder">The continuous aggregate builder.</param>
        /// <param name="startOffset">Window start as interval relative to execution time. NULL equals earliest data.</param>
        /// <param name="endOffset">Window end as interval relative to execution time. NULL equals latest data.</param>
        /// <param name="scheduleInterval">Interval between refresh executions. Defaults to "24 hours" if not specified.</param>
        /// <returns>A policy builder for configuring additional refresh policy options.</returns>
        /// <example>
        /// <code>
        /// builder.IsContinuousAggregate&lt;HourlyMetric, Metric&gt;("hourly_metrics", "1 hour", x => x.Timestamp)
        ///     .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
        ///     .WithTimezone("UTC")
        ///     .WithRefreshNewestFirst(true);
        /// </code>
        /// </example>
        public static ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity> WithRefreshPolicy<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            string? startOffset = null,
            string? endOffset = null,
            string? scheduleInterval = null)
            where TEntity : class
            where TSourceEntity : class
        {
            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);

            if (!string.IsNullOrWhiteSpace(startOffset))
                builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, startOffset);

            if (!string.IsNullOrWhiteSpace(endOffset))
                builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, endOffset);

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, scheduleInterval);

            return new ContinuousAggregatePolicyBuilder<TEntity, TSourceEntity>(builder);
        }
    }
}
