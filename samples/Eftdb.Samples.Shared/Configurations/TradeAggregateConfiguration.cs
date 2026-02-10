using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
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
                .MaterializedOnly()
                .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                .WithRefreshNewestFirst(true);
        }
    }
}
