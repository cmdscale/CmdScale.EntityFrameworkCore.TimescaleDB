using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class TimeBucketIntegrationTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(TimescaleImages.Community)
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Should_Translate_TimeBucket_In_Select

    private class TimeBucketSelectMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimeBucketSelectContext(string connectionString) : DbContext
    {
        public DbSet<TimeBucketSelectMetric> Metrics => Set<TimeBucketSelectMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeBucketSelectMetric>(entity =>
            {
                entity.ToTable("tb_select_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_TimeBucket_In_Select()
    {
        // Arrange
        await using TimeBucketSelectContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        DateTime baseTime = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
        {
            DateTime ts = baseTime.AddMinutes(i);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"tb_select_metrics\" (\"Timestamp\", \"Value\") VALUES ({ts}, {(double)(i * 10)})",
                TestContext.Current.CancellationToken);
        }

        // Act
        List<DateTime> buckets = await context.Metrics
            .Select(m => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), m.Timestamp))
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(buckets);
    }

    #endregion

    #region Should_Translate_TimeBucket_In_GroupBy

    private class TimeBucketGroupByMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimeBucketGroupByContext(string connectionString) : DbContext
    {
        public DbSet<TimeBucketGroupByMetric> Metrics => Set<TimeBucketGroupByMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeBucketGroupByMetric>(entity =>
            {
                entity.ToTable("tb_groupby_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_TimeBucket_In_GroupBy()
    {
        // Arrange
        await using TimeBucketGroupByContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        DateTime baseTime = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 12; i++)
        {
            DateTime ts = baseTime.AddMinutes(i);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"tb_groupby_metrics\" (\"Timestamp\", \"Value\") VALUES ({ts}, {(double)(i + 1)})",
                TestContext.Current.CancellationToken);
        }

        // Act
        var results = await context.Metrics
            .GroupBy(m => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), m.Timestamp))
            .Select(g => new
            {
                Bucket = g.Key,
                Total = g.Sum(m => m.Value),
                Count = g.Count()
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);

        Assert.Equal(5, results[0].Count);
        Assert.Equal(15, results[0].Total);

        Assert.Equal(5, results[1].Count);
        Assert.Equal(40, results[1].Total);

        Assert.Equal(2, results[2].Count);
        Assert.Equal(23, results[2].Total);
    }

    #endregion

    #region Should_Translate_TimeBucket_With_Integer_Arguments

    private class TimeBucketIntMetric
    {
        public int Id { get; set; }
        public int SequenceNumber { get; set; }
        public double Value { get; set; }
    }

    private class TimeBucketIntContext(string connectionString) : DbContext
    {
        public DbSet<TimeBucketIntMetric> Metrics => Set<TimeBucketIntMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeBucketIntMetric>(entity =>
            {
                entity.ToTable("tb_int_metrics");
                entity.HasKey(x => x.Id);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_TimeBucket_With_Integer_Arguments()
    {
        // Arrange
        await using TimeBucketIntContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        for (int i = 0; i < 20; i++)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"tb_int_metrics\" (\"Id\", \"SequenceNumber\", \"Value\") VALUES ({i + 1}, {i}, {(double)(i * 10)})",
                TestContext.Current.CancellationToken);
        }

        // Act
        var results = await context.Metrics
            .GroupBy(m => EF.Functions.TimeBucket(5, m.SequenceNumber))
            .Select(g => new
            {
                Bucket = g.Key,
                Count = g.Count()
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.Equal(5, r.Count));
    }

    #endregion
}
