using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess
{
    /// <seealso cref="DbContext" />
    public class TimescaleContext(DbContextOptions<TimescaleContext> options) : DbContext(options)
    {
        public DbSet<DeviceReading> DeviceReadings { get; set; }
        public DbSet<WeatherData> WeatherData { get; set; }
        public DbSet<OrderStatusEvent> OrderStatusEvents { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeWithId> TradesWithId { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimescaleContext).Assembly);
            modelBuilder.HasDefaultSchema("custom_schema");
        }
    }
}