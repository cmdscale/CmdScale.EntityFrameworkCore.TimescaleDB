using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Represents a migration operation that removes an existing compression policy from a hypertable
    /// or continuous aggregate by calling <c>remove_compression_policy()</c>.
    /// </summary>
    public class DropCompressionPolicyOperation : MigrationOperation
    {
        /// <summary>Gets or sets the table (or materialized view) name.</summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema of the table or materialized view.</summary>
        public string Schema { get; set; } = string.Empty;
    }
}
