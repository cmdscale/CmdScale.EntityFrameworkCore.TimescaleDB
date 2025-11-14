using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Configurations
{
    public class TradeAggregateConfiguration : IEntityTypeConfiguration<TradeAggregate>
    {
        public void Configure(EntityTypeBuilder<TradeAggregate> builder)
        {
            builder.HasNoKey();
            builder.IsContinuousAggregate<TradeAggregate, Trade>("trade_aggregate_view", "1 hour", x => x.Timestamp, true, "7 days")
                .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Max)
                .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Min)
                .AddGroupByColumn(x => x.Exchange)
                .AddGroupByColumn("1, 2")
                .Where("\"ticker\" = 'MCRS'")
                .MaterializedOnly();
        }
    }
}
