using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy
{
    /// <summary>
    /// Validates that every continuous aggregate configured with a compression policy has compression
    /// enabled. Runs at model finalization so that fluent API configuration applied in
    /// <c>OnModelCreating</c> is visible — unlike <see cref="IEntityTypeAddedConvention"/>, which fires
    /// before <c>OnModelCreating</c> executes.
    /// </summary>
    internal class CompressionPolicyPrerequisiteValidationConvention : IModelFinalizedConvention
    {
        /// <summary>
        /// Called once the model has been finalized and all conventions have run.
        /// </summary>
        /// <param name="model">The finalized model.</param>
        /// <returns>The unchanged model.</returns>
        public IModel ProcessModelFinalized(IModel model)
        {
            foreach (IEntityType entityType in model.GetEntityTypes())
            {
                ValidateContinuousAggregateCompressionPolicyPrerequisite(entityType);
            }

            return model;
        }

        private static void ValidateContinuousAggregateCompressionPolicyPrerequisite(IEntityType entityType)
        {
            bool hasPolicy = entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value as bool? ?? false;
            if (!hasPolicy)
            {
                return;
            }

            bool isContinuousAggregate = !string.IsNullOrWhiteSpace(
                entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string);

            if (!isContinuousAggregate)
            {
                return;
            }

            if (IsCompressionConfigured(entityType))
            {
                return;
            }

            string displayName = entityType.ClrType?.Name ?? entityType.Name;

            throw new InvalidOperationException(
                $"Compression policy on '{displayName}': Compression must be enabled on the continuous aggregate before adding a compression policy. " +
                "Enable compression first via [ContinuousAggregate(EnableCompression = true)] or .IsContinuousAggregate(...).WithCompression().");
        }

        /// <summary>
        /// Reads the compression annotations from the entity and evaluates the shared
        /// compression-state rule in <see cref="CompressionSettingsSqlHelper"/>.
        /// </summary>
        private static bool IsCompressionConfigured(IEntityType entityType)
            => CompressionSettingsSqlHelper.IsCompressionEnabled(
                entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value as bool? == true,
                entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string,
                entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string,
                entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value as string);
    }
}
