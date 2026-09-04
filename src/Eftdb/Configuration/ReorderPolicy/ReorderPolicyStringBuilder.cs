using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    /// <summary>
    /// Provides a fluent API for configuring optional TimescaleDB reorder policy parameters
    /// whose types cannot be rendered by the scaffold code generator (e.g. <see cref="DateTime"/>).
    /// Returned by the scaffold-targeting overloads of <c>WithReorderPolicy</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the reorder policy is applied to.</typeparam>
    public sealed class ReorderPolicyStringBuilder<TEntity> where TEntity : class
    {
        private readonly EntityTypeBuilder<TEntity> _builder;

        internal ReorderPolicyStringBuilder(EntityTypeBuilder<TEntity> builder)
        {
            _builder = builder;
        }

        /// <summary>
        /// Sets the initial start time for the reorder policy job.
        /// </summary>
        /// <param name="initialStart">The first time the policy job is scheduled to run.</param>
        /// <returns>The builder for method chaining.</returns>
        public ReorderPolicyStringBuilder<TEntity> WithInitialStart(DateTime initialStart)
        {
            PolicyJobBuilderCore.WithInitialStart(_builder, ReorderPolicyAnnotations.InitialStart, initialStart);
            return this;
        }
    }
}
