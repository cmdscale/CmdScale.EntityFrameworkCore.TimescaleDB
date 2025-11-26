using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ContinuousAggregateScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
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

    private async Task<string> GetTestConnectionStringAsync()
    {
        string testDbName = $"test_db_{Guid.NewGuid():N}".Replace("-", "");

        await using NpgsqlConnection adminConnection = new(_connectionString);
        await adminConnection.OpenAsync();

        await using (NpgsqlCommand createCmd = new($"CREATE DATABASE {testDbName}", adminConnection))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        string testConnectionString = _connectionString!.Replace("test_db", testDbName);
        await using NpgsqlConnection testConnection = new(testConnectionString);
        await testConnection.OpenAsync();
        await using (NpgsqlCommand extCmd = new("CREATE EXTENSION IF NOT EXISTS timescaledb", testConnection))
        {
            await extCmd.ExecuteNonQueryAsync();
        }

        return testConnectionString;
    }

    #region Should_Extract_Minimal_ContinuousAggregate

    private class MinimalSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class MinimalHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MinimalAggregateContext(string connectionString) : DbContext
    {
        public DbSet<MinimalSourceMetric> Metrics => Set<MinimalSourceMetric>();
        public DbSet<MinimalHourlyMetric> HourlyMetrics => Set<MinimalHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MinimalHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<MinimalHourlyMetric, MinimalSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_ContinuousAggregate()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MinimalAggregateContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "hourly_metrics")));

        object infoObj = result[("public", "hourly_metrics")];
        Assert.IsType<ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo>(infoObj);

        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)infoObj;

        Assert.Equal("hourly_metrics", info.MaterializedViewName);
        Assert.Equal("public", info.Schema);
        Assert.Equal("Metrics", info.SourceHypertableName);
        Assert.Equal("public", info.SourceSchema);
        Assert.NotNull(info.ViewDefinition);
    }

    #endregion

    #region Should_Return_Empty_When_No_ContinuousAggregates

    private class NoAggregateSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class NoAggregateContext(string connectionString) : DbContext
    {
        public DbSet<NoAggregateSourceMetric> Metrics => Set<NoAggregateSourceMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoAggregateSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_ContinuousAggregates()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using NoAggregateContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Empty(result);
    }

    #endregion

    #region Should_Extract_MaterializedOnly_True

    private class MatTrueSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class MatTrueHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyTrueContext(string connectionString) : DbContext
    {
        public DbSet<MatTrueSourceMetric> Metrics => Set<MatTrueSourceMetric>();
        public DbSet<MatTrueHourlyMetric> HourlyMetrics => Set<MatTrueHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatTrueSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MatTrueHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<MatTrueHourlyMetric, MatTrueSourceMetric>(
                    "hourly_metrics_mat_true",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                ).MaterializedOnly(true);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_MaterializedOnly_True()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MaterializedOnlyTrueContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "hourly_metrics_mat_true")];

        Assert.True(info.MaterializedOnly);
    }

    #endregion

    #region Should_Extract_MaterializedOnly_False

    private class MatFalseSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class MatFalseHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyFalseContext(string connectionString) : DbContext
    {
        public DbSet<MatFalseSourceMetric> Metrics => Set<MatFalseSourceMetric>();
        public DbSet<MatFalseHourlyMetric> HourlyMetrics => Set<MatFalseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatFalseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MatFalseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<MatFalseHourlyMetric, MatFalseSourceMetric>(
                    "hourly_metrics_mat_false",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                ).MaterializedOnly(false);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_MaterializedOnly_False()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MaterializedOnlyFalseContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "hourly_metrics_mat_false")];

        Assert.False(info.MaterializedOnly);
    }

    #endregion

    #region Should_Extract_ChunkInterval

    private class ChunkIntervalSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class ChunkIntervalHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ChunkIntervalContext(string connectionString) : DbContext
    {
        public DbSet<ChunkIntervalSourceMetric> Metrics => Set<ChunkIntervalSourceMetric>();
        public DbSet<ChunkIntervalHourlyMetric> HourlyMetrics => Set<ChunkIntervalHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ChunkIntervalHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<ChunkIntervalHourlyMetric, ChunkIntervalSourceMetric>(
                    "hourly_metrics_chunk",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "1 day"
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_ChunkInterval()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using ChunkIntervalContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "hourly_metrics_chunk")];

        Assert.NotNull(info.ChunkInterval);
        Assert.Contains("24:00:00", info.ChunkInterval);
    }

    #endregion

    #region Should_Extract_ViewDefinition_With_AggregateFunctions

    private class AggregatesSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class AggregatesHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
        public double MaxValue { get; set; }
    }

    private class AggregatesContext(string connectionString) : DbContext
    {
        public DbSet<AggregatesSourceMetric> Metrics => Set<AggregatesSourceMetric>();
        public DbSet<AggregatesHourlyMetric> HourlyMetrics => Set<AggregatesHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AggregatesSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregatesHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<AggregatesHourlyMetric, AggregatesSourceMetric>(
                    "hourly_metrics_agg",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                ).AddAggregateFunction(
                    x => x.MaxValue,
                    x => x.Value,
                    EAggregateFunction.Max
                );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_ViewDefinition_With_AggregateFunctions()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using AggregatesContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "hourly_metrics_agg")];

        Assert.NotNull(info.ViewDefinition);
        Assert.Contains("time_bucket", info.ViewDefinition);
        Assert.Contains("avg", info.ViewDefinition.ToLower());
        Assert.Contains("max", info.ViewDefinition.ToLower());
    }

    #endregion

    #region Should_Extract_Multiple_ContinuousAggregates

    private class MultipleSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class MultipleHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultipleDailyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultipleAggregatesContext(string connectionString) : DbContext
    {
        public DbSet<MultipleSourceMetric> Metrics => Set<MultipleSourceMetric>();
        public DbSet<MultipleHourlyMetric> HourlyMetrics => Set<MultipleHourlyMetric>();
        public DbSet<MultipleDailyMetric> DailyMetrics => Set<MultipleDailyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<MultipleHourlyMetric, MultipleSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });

            modelBuilder.Entity<MultipleDailyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("daily_metrics");
                entity.IsContinuousAggregate<MultipleDailyMetric, MultipleSourceMetric>(
                    "daily_metrics",
                    "1 day",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_ContinuousAggregates()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MultipleAggregatesContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(testConnectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "hourly_metrics")));
        Assert.True(result.ContainsKey(("public", "daily_metrics")));

        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo hourlyInfo =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "hourly_metrics")];
        Assert.Equal("hourly_metrics", hourlyInfo.MaterializedViewName);
        Assert.Contains("01:00:00", hourlyInfo.ViewDefinition);

        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo dailyInfo =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "daily_metrics")];
        Assert.Equal("daily_metrics", dailyInfo.MaterializedViewName);
        Assert.Contains("1 day", dailyInfo.ViewDefinition);
    }

    #endregion
}
