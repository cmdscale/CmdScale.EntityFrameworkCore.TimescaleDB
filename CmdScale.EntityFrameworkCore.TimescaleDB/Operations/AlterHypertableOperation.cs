using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AlterHypertableOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string ChunkTimeInterval { get; set; } = DefaultValues.ChunkTimeInterval;
        public bool EnableCompression { get; set; } = false;

        // For chunk skipping, you need to enable it with <code>SET timescaledb.enable_chunk_skipping = 'on'</coder>
        // Only timestamp-like and Integer-like columns are supported for chunk skipping
        // Cannot be reverted once enabled
        public IReadOnlyList<string>? ChunkSkipColumns { get; set; } = null;

        public string OldChunkTimeInterval { get; set; } = DefaultValues.ChunkTimeInterval;
        public bool OldEnableCompression { get; set; } = false;
        public IReadOnlyList<string>? OldChunkSkipColumns { get; set; } = null;
    }
}
