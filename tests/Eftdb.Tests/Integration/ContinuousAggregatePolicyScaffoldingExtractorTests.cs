using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ContinuousAggregatePolicyScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Should_Extract_Minimal_ContinuousAggregatePolicy

    private class MinimalPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalPolicyAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class MinimalPolicyContext(string connectionString) : DbContext
    {
        public DbSet<MinimalPolicyMetric> Metrics => Set<MinimalPolicyMetric>();
        public DbSet<MinimalPolicyAggregate> HourlyAggregates => Set<MinimalPolicyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MinimalPolicyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<MinimalPolicyAggregate, MinimalPolicyMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_ContinuousAggregatePolicy()
    {
        // Arrange
        await using MinimalPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "hourly_aggregates_view")));

        object infoObj = result[("public", "hourly_aggregates_view")];
        Assert.IsType<ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo>(infoObj);

        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)infoObj;

        Assert.Equal("7 days", info.StartOffset);
        Assert.Equal("1 hour", info.EndOffset);
        Assert.Equal("01:00:00", info.ScheduleInterval);
        Assert.Null(info.InitialStart);
        Assert.Null(info.IncludeTieredData);
        Assert.Null(info.BucketsPerBatch);
        Assert.Null(info.MaxBatchesPerExecution);
        Assert.Null(info.RefreshNewestFirst);
    }

    #endregion

    #region Should_Extract_Fully_Configured_ContinuousAggregatePolicy

    private class FullPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullPolicyAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class FullPolicyContext(string connectionString) : DbContext
    {
        public DbSet<FullPolicyMetric> Metrics => Set<FullPolicyMetric>();
        public DbSet<FullPolicyAggregate> HourlyAggregates => Set<FullPolicyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FullPolicyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<FullPolicyAggregate, FullPolicyMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(
                        startOffset: "1 month",
                        endOffset: "1 hour",
                        scheduleInterval: "30 minutes")
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    .WithIncludeTieredData(true)
                    .WithBucketsPerBatch(5)
                    .WithMaxBatchesPerExecution(10)
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Fully_Configured_ContinuousAggregatePolicy()
    {
        // Arrange
        await using FullPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "hourly_aggregates_view")));

        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.Equal("1 month", info.StartOffset);
        Assert.Equal("1 hour", info.EndOffset);
        Assert.Equal("00:30:00", info.ScheduleInterval);
        Assert.NotNull(info.InitialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, info.InitialStart.Value);
        Assert.True(info.IncludeTieredData);
        Assert.Equal(5, info.BucketsPerBatch);
        Assert.Equal(10, info.MaxBatchesPerExecution);
        Assert.False(info.RefreshNewestFirst);
    }

    #endregion

    #region Should_Return_Empty_When_No_Policy

    private class NoPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoPolicyAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class NoPolicyContext(string connectionString) : DbContext
    {
        public DbSet<NoPolicyMetric> Metrics => Set<NoPolicyMetric>();
        public DbSet<NoPolicyAggregate> HourlyAggregates => Set<NoPolicyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoPolicyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<NoPolicyAggregate, NoPolicyMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg);
                // No WithRefreshPolicy call
            });
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Policy()
    {
        // Arrange
        await using NoPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Should_Extract_Policy_With_InitialStart

    private class InitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class InitialStartContext(string connectionString) : DbContext
    {
        public DbSet<InitialStartMetric> Metrics => Set<InitialStartMetric>();
        public DbSet<InitialStartAggregate> HourlyAggregates => Set<InitialStartAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<InitialStartAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<InitialStartAggregate, InitialStartMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(
                        startOffset: "7 days",
                        endOffset: "1 hour",
                        scheduleInterval: "2 hours")
                    .WithInitialStart(new DateTime(2024, 12, 25, 12, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Policy_With_InitialStart()
    {
        // Arrange
        await using InitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.NotNull(info.InitialStart);
        DateTime expectedDate = new(2024, 12, 25, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, info.InitialStart.Value);
    }

    #endregion

    #region Should_Extract_Multiple_Policies

    private class MultiplePoliciesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiplePoliciesHourlyAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class MultiplePoliciesDailyAggregate
    {
        public DateTime Bucket { get; set; }
        public double MaxValue { get; set; }
    }

    private class MultiplePoliciesContext(string connectionString) : DbContext
    {
        public DbSet<MultiplePoliciesMetric> Metrics => Set<MultiplePoliciesMetric>();
        public DbSet<MultiplePoliciesHourlyAggregate> HourlyAggregates => Set<MultiplePoliciesHourlyAggregate>();
        public DbSet<MultiplePoliciesDailyAggregate> DailyAggregates => Set<MultiplePoliciesDailyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiplePoliciesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultiplePoliciesHourlyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<MultiplePoliciesHourlyAggregate, MultiplePoliciesMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour");
            });

            modelBuilder.Entity<MultiplePoliciesDailyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("DailyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<MultiplePoliciesDailyAggregate, MultiplePoliciesMetric>(
                    "daily_aggregates_view",
                    "1 day",
                    source => source.Timestamp,
                    true,
                    "30 days")
                    .AddAggregateFunction(cagg => cagg.MaxValue, source => source.Value, EAggregateFunction.Max)
                    .WithRefreshPolicy(startOffset: "30 days", endOffset: "1 day", scheduleInterval: "1 day");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_Policies()
    {
        // Arrange
        await using MultiplePoliciesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "hourly_aggregates_view")));
        Assert.True(result.ContainsKey(("public", "daily_aggregates_view")));

        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo hourlyInfo =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];
        Assert.Equal("7 days", hourlyInfo.StartOffset);
        Assert.Equal("1 hour", hourlyInfo.EndOffset);

        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo dailyInfo =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "daily_aggregates_view")];
        Assert.Equal("30 days", dailyInfo.StartOffset);
        Assert.Equal("1 day", dailyInfo.EndOffset);
    }

    #endregion

    #region Should_Extract_Policy_With_IncludeTieredData

    private class TieredDataMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TieredDataAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class TieredDataContext(string connectionString) : DbContext
    {
        public DbSet<TieredDataMetric> Metrics => Set<TieredDataMetric>();
        public DbSet<TieredDataAggregate> HourlyAggregates => Set<TieredDataAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TieredDataMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<TieredDataAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<TieredDataAggregate, TieredDataMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithIncludeTieredData(false);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Policy_With_IncludeTieredData()
    {
        // Arrange
        await using TieredDataContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.False(info.IncludeTieredData);
    }

    #endregion

    #region Should_Extract_Policy_With_BucketsPerBatch

    private class BucketsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BucketsAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class BucketsContext(string connectionString) : DbContext
    {
        public DbSet<BucketsMetric> Metrics => Set<BucketsMetric>();
        public DbSet<BucketsAggregate> HourlyAggregates => Set<BucketsAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketsAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<BucketsAggregate, BucketsMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithBucketsPerBatch(10);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Policy_With_BucketsPerBatch()
    {
        // Arrange
        await using BucketsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.Equal(10, info.BucketsPerBatch);
    }

    #endregion

    #region Should_Extract_Policy_With_MaxBatchesPerExecution

    private class MaxBatchesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxBatchesAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class MaxBatchesContext(string connectionString) : DbContext
    {
        public DbSet<MaxBatchesMetric> Metrics => Set<MaxBatchesMetric>();
        public DbSet<MaxBatchesAggregate> HourlyAggregates => Set<MaxBatchesAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxBatchesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MaxBatchesAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<MaxBatchesAggregate, MaxBatchesMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithMaxBatchesPerExecution(100);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Policy_With_MaxBatchesPerExecution()
    {
        // Arrange
        await using MaxBatchesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.Equal(100, info.MaxBatchesPerExecution);
    }

    #endregion

    #region Should_Extract_Policy_With_RefreshNewestFirst

    private class RefreshOrderMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RefreshOrderAggregate
    {
        public DateTime Bucket { get; set; }
        public double AverageValue { get; set; }
    }

    private class RefreshOrderContext(string connectionString) : DbContext
    {
        public DbSet<RefreshOrderMetric> Metrics => Set<RefreshOrderMetric>();
        public DbSet<RefreshOrderAggregate> HourlyAggregates => Set<RefreshOrderAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshOrderMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RefreshOrderAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HourlyAggregates", "public", t => t.ExcludeFromMigrations());
                entity.IsContinuousAggregate<RefreshOrderAggregate, RefreshOrderMetric>(
                    "hourly_aggregates_view",
                    "1 hour",
                    source => source.Timestamp,
                    true,
                    "7 days")
                    .AddAggregateFunction(cagg => cagg.AverageValue, source => source.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Policy_With_RefreshNewestFirst()
    {
        // Arrange
        await using RefreshOrderContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        ContinuousAggregatePolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.Single(result);
        ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo info =
            (ContinuousAggregatePolicyScaffoldingExtractor.ContinuousAggregatePolicyInfo)result[("public", "hourly_aggregates_view")];

        Assert.False(info.RefreshNewestFirst);
    }

    #endregion
}
