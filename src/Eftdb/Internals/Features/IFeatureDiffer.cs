using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features
{
    /// <summary>
    /// Defines a contract for a component that can detect differences for a specific 
    /// TimescaleDB feature between two model states.
    /// </summary>
    internal interface IFeatureDiffer
    {
        /// <summary>
        /// Gets the migration operations needed to transition from the source to the target model.
        /// </summary>
        /// <param name="source">The source model (from the last migration).</param>
        /// <param name="target">The target model (the current state).</param>
        /// <param name="context">
        /// Cross-cutting diff information. When omitted, the differ behaves as if
        /// nothing was renamed or recreated (<see cref="FeatureDiffContext.Empty"/>).
        /// </param>
        /// <returns>A collection of migration operations.</returns>
        IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null);
    }
}
