using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared
{
    /// <seealso cref="DbContext" />
    public class TimescaleContext(DbContextOptions<TimescaleContext> options) : DbContext(options)
    {
        public DbSet<DeviceReading> DeviceReadings { get; set; }
        public DbSet<WeatherData> WeatherData { get; set; }
        public DbSet<OrderStatusEvent> OrderStatusEvents { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeWithId> TradesWithId { get; set; }
        public DbSet<TradeAggregate> TradeAggregates { get; set; }
        public DbSet<WeatherAggregate> WeatherAggregates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimescaleContext).Assembly);
            modelBuilder.HasDefaultSchema("custom_schema");
        }
    }
}