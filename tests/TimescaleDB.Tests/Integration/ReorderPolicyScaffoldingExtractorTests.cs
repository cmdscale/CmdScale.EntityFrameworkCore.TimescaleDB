using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ReorderPolicyScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
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

    #region Should_Extract_Minimal_ReorderPolicy

    private class MinimalPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalReorderPolicyContext(string connectionString) : DbContext
    {
        public DbSet<MinimalPolicyMetric> Metrics => Set<MinimalPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_ReorderPolicy()
    {
        await using MinimalReorderPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "Metrics")));

        object infoObj = result[("public", "Metrics")];
        Assert.IsType<ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo>(infoObj);

        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)infoObj;
        Assert.Equal("metrics_time_idx", info.IndexName);
        Assert.Null(info.InitialStart);
        Assert.NotNull(info.ScheduleInterval);
        Assert.NotNull(info.MaxRuntime);
        Assert.NotNull(info.MaxRetries);
        Assert.NotNull(info.RetryPeriod);
    }

    #endregion

    #region Should_Return_Empty_When_No_ReorderPolicies

    private class NoPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoReorderPolicyContext(string connectionString) : DbContext
    {
        public DbSet<NoPolicyMetric> Metrics => Set<NoPolicyMetric>();

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
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_ReorderPolicies()
    {
        await using NoReorderPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Empty(result);
    }

    #endregion

    #region Should_Extract_InitialStart

    private class InitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext(string connectionString) : DbContext
    {
        public DbSet<InitialStartMetric> Metrics => Set<InitialStartMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_InitialStart()
    {
        await using InitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.NotNull(info.InitialStart);

        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, info.InitialStart.Value);
    }

    #endregion

    #region Should_Extract_ScheduleInterval

    private class ScheduleIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalContext(string connectionString) : DbContext
    {
        public DbSet<ScheduleIntervalMetric> Metrics => Set<ScheduleIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          scheduleInterval: "12:00:00"
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_ScheduleInterval()
    {
        await using ScheduleIntervalContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.Equal("12:00:00", info.ScheduleInterval);
    }

    #endregion

    #region Should_Extract_MaxRuntime

    private class MaxRuntimeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRuntimeContext(string connectionString) : DbContext
    {
        public DbSet<MaxRuntimeMetric> Metrics => Set<MaxRuntimeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          maxRuntime: "01:00:00"
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_MaxRuntime()
    {
        await using MaxRuntimeContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.Equal("01:00:00", info.MaxRuntime);
    }

    #endregion

    #region Should_Extract_MaxRetries

    private class MaxRetriesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesContext(string connectionString) : DbContext
    {
        public DbSet<MaxRetriesMetric> Metrics => Set<MaxRetriesMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          maxRetries: 5
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_MaxRetries()
    {
        await using MaxRetriesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.Equal(5, info.MaxRetries);
    }

    #endregion

    #region Should_Extract_RetryPeriod

    private class RetryPeriodMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetryPeriodContext(string connectionString) : DbContext
    {
        public DbSet<RetryPeriodMetric> Metrics => Set<RetryPeriodMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetryPeriodMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          retryPeriod: "00:10:00"
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_RetryPeriod()
    {
        await using RetryPeriodContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.Equal("00:10:00", info.RetryPeriod);
    }

    #endregion

    #region Should_Extract_Multiple_ReorderPolicies

    private class MultiplePoliciesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiplePoliciesEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultiplePoliciesContext(string connectionString) : DbContext
    {
        public DbSet<MultiplePoliciesMetric> Metrics => Set<MultiplePoliciesMetric>();
        public DbSet<MultiplePoliciesEvent> Events => Set<MultiplePoliciesEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiplePoliciesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });

            modelBuilder.Entity<MultiplePoliciesEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.HasIndex(x => x.Timestamp, "events_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("events_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_ReorderPolicies()
    {
        await using MultiplePoliciesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "Metrics")));
        Assert.True(result.ContainsKey(("public", "Events")));

        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo metricsInfo = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];
        Assert.Equal("metrics_time_idx", metricsInfo.IndexName);

        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo eventsInfo = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Events")];
        Assert.Equal("events_time_idx", eventsInfo.IndexName);
    }

    #endregion

    #region Should_Extract_Fully_Configured_ReorderPolicy

    private class FullyConfiguredMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredContext(string connectionString) : DbContext
    {
        public DbSet<FullyConfiguredMetric> Metrics => Set<FullyConfiguredMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                          scheduleInterval: "06:00:00",
                          maxRuntime: "02:00:00",
                          maxRetries: 3,
                          retryPeriod: "00:15:00"
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Fully_Configured_ReorderPolicy()
    {
        await using FullyConfiguredContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        ReorderPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo info = (ReorderPolicyScaffoldingExtractor.ReorderPolicyInfo)result[("public", "Metrics")];

        Assert.Equal("metrics_time_idx", info.IndexName);
        Assert.NotNull(info.InitialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, info.InitialStart.Value);
        Assert.Equal("06:00:00", info.ScheduleInterval);
        Assert.Equal("02:00:00", info.MaxRuntime);
        Assert.Equal(3, info.MaxRetries);
        Assert.Equal("00:15:00", info.RetryPeriod);
    }

    #endregion
}
