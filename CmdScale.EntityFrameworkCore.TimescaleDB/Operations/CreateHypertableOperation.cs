using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class CreateHypertableOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string TimeColumnName { get; set; } = string.Empty;
        public string ChunkTimeInterval { get; set; } = DefaultValues.ChunkTimeInterval;
        public bool EnableCompression { get; set; } = false;
        public IReadOnlyList<string>? ChunkSkipColumns { get; set; } = null;
    }
}
