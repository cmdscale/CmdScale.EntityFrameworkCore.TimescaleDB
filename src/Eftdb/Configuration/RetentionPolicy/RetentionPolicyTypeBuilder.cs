using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy
{
    /// <summary>
    /// Provides extension methods for configuring TimescaleDB retention policies using the EF Core Fluent API.
    /// </summary>
    public static class RetentionPolicyTypeBuilder
    {
        /// <summary>
        /// Configures a TimescaleDB retention policy for the entity using a fluent API.
        /// Exactly one of <paramref name="dropAfter"/> or <paramref name="dropCreatedBefore"/> must be specified.
        /// </summary>
        /// <remarks>
        /// A retention policy automatically drops chunks whose data is older than a specified interval.
        /// <br/><br/>
        /// NOTE: When you use DropCreatedBefore, instead of DropAfter, arguments related to the alter_job function like MaxRuntime, MaxRetries, 
        /// or RetryPeriod are not supported.
        /// The reason for this is a bug in TimescaleDB itself. See <a href="https://github.com/timescale/timescaledb/issues/9446">this issue</a> for further information.
        /// </remarks>
        /// <example>
        /// <code>
        /// modelBuilder.Entity&lt;DeviceReading&gt;()
        ///     .WithRetentionPolicy(
        ///         dropAfter: "7 days",
        ///         scheduleInterval: "1 day",
        ///         maxRetries: 5);
        /// </code>
        /// </example>
        /// <typeparam name="TEntity">The type of the entity being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="dropAfter">The interval after which chunks are dropped. Mutually exclusive with <paramref name="dropCreatedBefore"/>.</param>
        /// <param name="dropCreatedBefore">The interval before which chunks created are dropped. Mutually exclusive with <paramref name="dropAfter"/>. Not supported for continuous aggregates.</param>
        /// <param name="initialStart">The first time the policy job is scheduled to run. If null, it's based on the schedule interval.</param>
        /// <param name="scheduleInterval">The interval at which the retention policy job runs.</param>
        /// <param name="maxRuntime">The maximum amount of time the job is allowed to run. If null, there is no time limit.</param>
        /// <param name="maxRetries">The number of times the job is retried if it fails. Defaults to -1 (retry indefinitely) if not specified.</param>
        /// <param name="retryPeriod">The amount of time the scheduler waits between retries.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public static EntityTypeBuilder<TEntity> WithRetentionPolicy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string? dropAfter = null,
            string? dropCreatedBefore = null,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null) where TEntity : class
        {
            bool hasDropAfter = !string.IsNullOrWhiteSpace(dropAfter);
            bool hasDropCreatedBefore = !string.IsNullOrWhiteSpace(dropCreatedBefore);

            if (hasDropAfter && hasDropCreatedBefore)
            {
                throw new InvalidOperationException("WithRetentionPolicy: 'dropAfter' and 'dropCreatedBefore' are mutually exclusive. Specify exactly one.");
            }

            if (!hasDropAfter && !hasDropCreatedBefore)
            {
                throw new InvalidOperationException("WithRetentionPolicy: Exactly one of 'dropAfter' or 'dropCreatedBefore' must be specified.");
            }

            entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);

            if (hasDropAfter)
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.DropAfter, dropAfter!);

            if (hasDropCreatedBefore)
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.DropCreatedBefore, dropCreatedBefore!);

            if (initialStart.HasValue)
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.InitialStart, initialStart);

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.ScheduleInterval, scheduleInterval);

            if (!string.IsNullOrWhiteSpace(maxRuntime))
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.MaxRuntime, maxRuntime);

            if (maxRetries.HasValue)
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.MaxRetries, maxRetries.Value);

            if (!string.IsNullOrWhiteSpace(retryPeriod))
                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.RetryPeriod, retryPeriod);

            return entityTypeBuilder;
        }
    }
}
