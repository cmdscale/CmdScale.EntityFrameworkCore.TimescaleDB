using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class CreateHypertableOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string TimeColumnName { get; set; } = string.Empty;
        public string ChunkTimeInterval { get; set; } = string.Empty;
        public bool EnableCompression { get; set; }
        public IReadOnlyList<string>? ChunkSkipColumns { get; set; }
        public IReadOnlyList<Dimension>? AdditionalDimensions { get; set; }
    }
}
