using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Configurations
{
    public class TradeConfiguration : IEntityTypeConfiguration<Trade>
    {
        public void Configure(EntityTypeBuilder<Trade> builder)
        {
            builder.ToTable("Trades");
            builder.HasNoKey()
                   .IsHypertable(x => x.Timestamp)
                   .WithChunkTimeInterval("1 day");
            builder.WithReorderPolicy("Trades_Timestamp_idx", DateTime.Parse("2025-09-23T09:15:19.3905112Z"), "2 days", "10 minutes", null, "1 minute");
        }
    }
}
