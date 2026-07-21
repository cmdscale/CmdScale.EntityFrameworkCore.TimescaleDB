using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy
{
    /// <summary>
    /// Provides a fluent API for configuring optional TimescaleDB retention policy parameters
    /// whose types cannot be rendered by the scaffold code generator (e.g. <see cref="DateTime"/>).
    /// Returned by the scaffold-targeting overload of <c>WithRetentionPolicy</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the retention policy is applied to.</typeparam>
    public sealed class RetentionPolicyStringBuilder<TEntity> where TEntity : class
    {
        private readonly EntityTypeBuilder<TEntity> _builder;

        internal EntityTypeBuilder<TEntity> EntityTypeBuilder => _builder;

        internal RetentionPolicyStringBuilder(EntityTypeBuilder<TEntity> builder)
        {
            _builder = builder;
        }

        /// <summary>
        /// Sets the initial start time for the retention policy job.
        /// </summary>
        /// <param name="initialStart">The first time the policy job is scheduled to run.</param>
        /// <returns>The builder for method chaining.</returns>
        public RetentionPolicyStringBuilder<TEntity> WithInitialStart(DateTime initialStart)
        {
            _builder.HasAnnotation(RetentionPolicyAnnotations.InitialStart, initialStart);
            return this;
        }
    }
}
