using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy
{
    /// <summary>
    /// Provides a fluent API for configuring optional TimescaleDB compression policy parameters
    /// whose types cannot be rendered by the scaffold code generator (e.g. <see cref="DateTime"/>).
    /// Returned by the scaffold-targeting overloads of <c>WithCompressionPolicy</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the compression policy is applied to.</typeparam>
    public sealed class CompressionPolicyStringBuilder<TEntity> where TEntity : class
    {
        private readonly EntityTypeBuilder<TEntity> _builder;

        internal EntityTypeBuilder<TEntity> EntityTypeBuilder => _builder;

        internal CompressionPolicyStringBuilder(EntityTypeBuilder<TEntity> builder)
        {
            _builder = builder;
        }

        /// <summary>
        /// Sets the initial start time for the compression policy job.
        /// </summary>
        /// <param name="initialStart">The first time the policy job is scheduled to run.</param>
        /// <returns>The builder for method chaining.</returns>
        public CompressionPolicyStringBuilder<TEntity> WithInitialStart(DateTime initialStart)
        {
            PolicyJobBuilderCore.WithInitialStart(_builder, CompressionPolicyAnnotations.InitialStart, initialStart);
            return this;
        }
    }
}
