using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;

public class TimescaleQueryFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg17")
        .WithDatabase("query_tests_db")
        .WithUsername(TimescaleConnectionHelper.Username)
        .WithPassword(TimescaleConnectionHelper.Password)
        .Build();

    public TestSqlLoggerFactory TestSqlLoggerFactory { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using QueryTestContext context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        DateTime baseTime = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 12; i++)
        {
            context.Metrics.Add(new QueryMetric
            {
                Timestamp = baseTime.AddMinutes(i),
                SequenceNumber = i,
                Value = (i + 1) * 10.0
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }

    public QueryTestContext CreateContext()
    {
        TestSqlLoggerFactory.Clear();
        return new QueryTestContext(_container.GetConnectionString(), TestSqlLoggerFactory);
    }

    public class QueryMetric
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int SequenceNumber { get; set; }
        public double Value { get; set; }
    }

    public class QueryTestContext(string connectionString, TestSqlLoggerFactory loggerFactory) : DbContext
    {
        public DbSet<QueryMetric> Metrics => Set<QueryMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql(connectionString)
                .UseTimescaleDb()
                .UseLoggerFactory(loggerFactory)
                .EnableSensitiveDataLogging();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QueryMetric>(entity =>
            {
                entity.ToTable("query_metrics");
                entity.HasKey(x => x.Id);
            });
        }
    }
}
