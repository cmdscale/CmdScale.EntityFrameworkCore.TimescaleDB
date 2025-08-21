using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess
{

    /// <summary>
    /// Add migration: <code>dotnet ef migrations add --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example *MigrationName*</code>
    /// Rollout migration: <code>dotnet ef database update --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example</code>
    /// Reset all migrations: <code>dotnet ef database update 0 --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example</code>
    /// </summary>
    /// <seealso cref="DbContext" />
    public class TimescaleContext : DbContext
    {
        public DbSet<DeviceReading> DeviceReadings { get; set; }
        public DbSet<WeatherData> WeatherData { get; set; }

        public TimescaleContext(DbContextOptions<TimescaleContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimescaleContext).Assembly);
        }
    }
}