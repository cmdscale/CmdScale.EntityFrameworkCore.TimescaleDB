using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    /// <summary>
    /// Provides extension methods for configuring TimescaleDB hypertables reorder policies using the EF Core Fluent API.
    /// </summary>
    public static class ReorderPolicyTypeBuilder
    {
        /// <summary>
        /// Configures a TimescaleDB reorder policy for the entity using a fluent API.
        /// </summary>
        /// <remarks>
        /// A reorder policy physically reorders data on disk according to a specified index to improve query performance.
        /// This method applies the necessary annotations to the entity's metadata, which are then used by the custom
        /// migrations infrastructure to generate the appropriate <c>add_reorder_policy()</c> and <c>alter_job()</c> SQL commands.
        /// </remarks>
        /// <example>
        /// <code>
        /// modelBuilder.Entity&lt;DeviceReading&gt;()
        ///     .WithReorderPolicy(
        ///         indexName: "IX_DeviceReadings_DeviceId_Time",
        ///         scheduleInterval: "2 days",
        ///         maxRetries: 5);
        /// </code>
        /// </example>
        /// <typeparam name="TEntity">The type of the entity being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="indexName">The name of the existing index that the reorder policy will use to sort the data.</param>
        /// <param name="initialStart">The first time the policy job is scheduled to run. If null, it's based on the schedule interval.</param>
        /// <param name="scheduleInterval">The interval at which the reorder policy job runs. Defaults to '1 day' if not specified.</param>
        /// <param name="maxRuntime">The maximum amount of time the job is allowed to run. If null, there is no time limit.</param>
        /// <param name="maxRetries">The number of times the job is retried if it fails. Defaults to -1 (retry indefinitely) if not specified.</param>
        /// <param name="retryPeriod">The amount of time the scheduler waits between retries. Defaults to '1 hour' if not specified.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public static EntityTypeBuilder<TEntity> WithReorderPolicy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string indexName,
            DateTime? initialStart = null,
            string? scheduleInterval = null,
            string? maxRuntime = null,
            int? maxRetries = null,
            string? retryPeriod = null) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
            entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.IndexName, indexName);

            if (initialStart.HasValue)
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.InitialStart, initialStart);

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.ScheduleInterval, scheduleInterval);

            if (!string.IsNullOrWhiteSpace(maxRuntime))
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.MaxRuntime, maxRuntime);

            if (maxRetries.HasValue)
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.MaxRetries, maxRetries.Value);

            if (!string.IsNullOrWhiteSpace(retryPeriod))
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.RetryPeriod, retryPeriod);

            return entityTypeBuilder;
        }
    }
}
