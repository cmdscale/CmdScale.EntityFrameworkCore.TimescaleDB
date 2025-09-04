using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Configurations
{
    public class TradeWithIdConfiguration : IEntityTypeConfiguration<TradeWithId>
    {
        public void Configure(EntityTypeBuilder<TradeWithId> builder)
        {
            builder.ToTable("TradesWithId");
            builder.HasKey(x => new { x.Id, x.Timestamp });
            builder.IsHypertable(x => x.Timestamp)
                   .WithChunkTimeInterval("1 day");
        }
    }
}
