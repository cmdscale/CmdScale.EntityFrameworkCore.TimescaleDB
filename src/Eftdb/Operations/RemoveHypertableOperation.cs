using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Signals that an entity lost its hypertable designation while its table still exists in the target model.
    /// TimescaleDB cannot convert a hypertable back into a plain table, so this operation applies no SQL; it only
    /// surfaces a warning so the model/database drift is visible.
    /// </summary>
    public class RemoveHypertableOperation : MigrationOperation
    {
        public string Schema { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
    }
}
