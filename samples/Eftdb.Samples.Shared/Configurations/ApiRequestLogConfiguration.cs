using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class ApiRequestLogConfiguration : IEntityTypeConfiguration<ApiRequestLog>
    {
        public void Configure(EntityTypeBuilder<ApiRequestLog> builder)
        {
            builder.ToTable("ApiRequestLogs");
            builder.HasNoKey()
                   .IsHypertable(x => x.Time)
                   .WithChunkTimeInterval("1 day");
            builder.WithRetentionPolicy(
                dropCreatedBefore: "30 days",
                scheduleInterval: "1 day",
                maxRetries: 5,
                retryPeriod: "10 minutes");
        }
    }
}
