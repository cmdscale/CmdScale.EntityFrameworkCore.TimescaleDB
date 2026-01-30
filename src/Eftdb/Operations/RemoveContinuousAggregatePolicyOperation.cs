using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Represents a migration operation to remove a continuous aggregate refresh policy in TimescaleDB.
    /// This operation generates a call to TimescaleDB's remove_continuous_aggregate_policy() function.
    /// </summary>
    public class RemoveContinuousAggregatePolicyOperation : MigrationOperation
    {
        /// <summary>
        /// Gets or sets the name of the materialized view (continuous aggregate) from which to remove the policy.
        /// </summary>
        public string MaterializedViewName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema name of the materialized view.
        /// </summary>
        public string Schema { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to print a warning instead of erroring if the policy doesn't exist.
        /// Defaults to false.
        /// </summary>
        public bool IfExists { get; init; } = false;
    }
}
