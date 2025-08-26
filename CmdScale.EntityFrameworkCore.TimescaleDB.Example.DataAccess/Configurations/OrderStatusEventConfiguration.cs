using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Configurations
{
    public class OrderStatusEventConfiguration : IEntityTypeConfiguration<OrderStatusEvent>
    {
        public void Configure(EntityTypeBuilder<OrderStatusEvent> builder)
        {
            // Create a composite primary key that includes the partitioning column.
            builder.HasKey(e => new { e.Id, e.EventTimestamp, e.OrderPlacedTimestamp, e.WarehouseId });

            // Define the hypertable with its three dimensions
            builder
                .IsHypertable(e => e.EventTimestamp)
                .WithChunkTimeInterval("7 days")
                .HasDimension(Dimension.CreateRange("OrderPlacedTimestamp", "1 month"))
                .HasDimension(Dimension.CreateHash("WarehouseId", 4));
        }
    }
}
