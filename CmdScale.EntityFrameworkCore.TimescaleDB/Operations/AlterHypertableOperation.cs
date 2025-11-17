using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AlterHypertableOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string ChunkTimeInterval { get; set; } = string.Empty;
        public bool EnableCompression { get; set; }

        // For chunk skipping, you need to enable it with <code>SET timescaledb.enable_chunk_skipping = 'on'</coder>
        // Only timestamp-like and Integer-like columns are supported for chunk skipping
        // Cannot be reverted once enabled
        public IReadOnlyList<string>? ChunkSkipColumns { get; set; }
        public IReadOnlyList<Dimension>? AdditionalDimensions { get; set; }

        public string OldChunkTimeInterval { get; set; } = string.Empty;
        public bool OldEnableCompression { get; set; }
        public IReadOnlyList<string>? OldChunkSkipColumns { get; set; }
        public IReadOnlyList<Dimension>? OldAdditionalDimensions { get; set; }
    }
}
