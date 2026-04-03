using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class ApiRequestAggregateConfiguration : IEntityTypeConfiguration<ApiRequestAggregate>
    {
        public void Configure(EntityTypeBuilder<ApiRequestAggregate> builder)
        {
            builder.HasNoKey();
            builder.IsContinuousAggregate<ApiRequestAggregate, ApiRequestLog>("api_request_hourly_stats", "1 hour", x => x.Time, true)
                .AddAggregateFunction(x => x.AverageDurationMs, x => x.DurationMs, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.MaxDurationMs, x => x.DurationMs, EAggregateFunction.Max)
                .AddAggregateFunction(x => x.MinDurationMs, x => x.DurationMs, EAggregateFunction.Min)
                .AddGroupByColumn(x => x.ServiceName)
                .WithRefreshPolicy(startOffset: "2 days", endOffset: "1 hour", scheduleInterval: "1 hour");
            builder.WithRetentionPolicy(
                dropAfter: "90 days",
                scheduleInterval: "1 day",
                maxRetries: 3,
                retryPeriod: "15 minutes");
        }
    }
}
