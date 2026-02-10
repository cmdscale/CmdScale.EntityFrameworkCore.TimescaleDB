using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class TradeConfiguration : IEntityTypeConfiguration<Trade>
    {
        public void Configure(EntityTypeBuilder<Trade> builder)
        {
            builder.ToTable("Trades");
            builder.HasNoKey()
                   .IsHypertable(x => x.Timestamp)
                   .WithChunkTimeInterval("1 day");
            builder.HasIndex(x => x.Timestamp).HasDatabaseName("Trades_Timestamp_idx");
            builder.WithReorderPolicy("Trades_Timestamp_idx", DateTime.Parse("2025-09-23T09:15:19.3905112Z"), "2 days", "10 minutes", -1, "1 minute");
        }
    }
}
