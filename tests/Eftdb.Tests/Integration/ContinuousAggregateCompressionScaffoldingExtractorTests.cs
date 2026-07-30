using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ContinuousAggregateCompressionScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:latest-pg17")
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

    private async Task<string> GetTestConnectionStringAsync()
    {
        string testDbName = $"test_db_{Guid.NewGuid():N}";

        await using NpgsqlConnection adminConnection = new(_connectionString);
        await adminConnection.OpenAsync();

        await using (NpgsqlCommand createCmd = new($"CREATE DATABASE {testDbName}", adminConnection))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        string testConnectionString = _connectionString!.Replace("test_db", testDbName, StringComparison.OrdinalIgnoreCase);
        await using NpgsqlConnection testConnection = new(testConnectionString);
        await testConnection.OpenAsync();
        await using (NpgsqlCommand extCmd = new("CREATE EXTENSION IF NOT EXISTS timescaledb", testConnection))
        {
            await extCmd.ExecuteNonQueryAsync();
        }

        return testConnectionString;
    }

    // ── Extractor: compression fields ─────────────────────────────────────────

    #region Should_Extract_CompressionEnabled_True_For_Compressed_CAgg

    private class ScaffExtSourceMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffExtHourlyView1
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ScaffExtCompressedContext1(string connectionString) : DbContext
    {
        public DbSet<ScaffExtSourceMetric1> Metrics => Set<ScaffExtSourceMetric1>();
        public DbSet<ScaffExtHourlyView1> HourlyMetrics => Set<ScaffExtHourlyView1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffExtSourceMetric1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_ext1_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ScaffExtHourlyView1>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("scaff_ext1_hourly");
                entity.IsContinuousAggregate<ScaffExtHourlyView1, ScaffExtSourceMetric1>(
                    "scaff_ext1_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression(true);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionEnabled_True_For_Compressed_CAgg()
    {
        // Arrange
        string conn = await GetTestConnectionStringAsync();
        await using ScaffExtCompressedContext1 ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(conn);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "scaff_ext1_hourly")));
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "scaff_ext1_hourly")];

        Assert.True(info.CompressionEnabled);
    }

    #endregion

    #region Should_Extract_CompressionSegmentBy_For_Compressed_CAgg

    private class ScaffExtSourceMetric2
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class ScaffExtHourlyView2
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    private class ScaffExtSegByContext(string connectionString) : DbContext
    {
        public DbSet<ScaffExtSourceMetric2> Metrics => Set<ScaffExtSourceMetric2>();
        public DbSet<ScaffExtHourlyView2> HourlyMetrics => Set<ScaffExtHourlyView2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffExtSourceMetric2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_ext2_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ScaffExtHourlyView2>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("scaff_ext2_hourly");
                entity.IsContinuousAggregate<ScaffExtHourlyView2, ScaffExtSourceMetric2>(
                    "scaff_ext2_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.DeviceId)
                    .WithCompressionSegmentBy(x => x.DeviceId);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.Property(x => x.DeviceId).HasColumnName("DeviceId");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionSegmentBy_For_Compressed_CAgg()
    {
        // Arrange
        string conn = await GetTestConnectionStringAsync();
        await using ScaffExtSegByContext ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(conn);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "scaff_ext2_hourly")];

        Assert.True(info.CompressionEnabled);
        Assert.NotNull(info.CompressionSegmentBy);
        Assert.Contains("DeviceId", info.CompressionSegmentBy);
    }

    #endregion

    #region Should_Extract_CompressionOrderBy_For_Compressed_CAgg

    private class ScaffExtSourceMetric3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffExtHourlyView3
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ScaffExtOrderByContext(string connectionString) : DbContext
    {
        public DbSet<ScaffExtSourceMetric3> Metrics => Set<ScaffExtSourceMetric3>();
        public DbSet<ScaffExtHourlyView3> HourlyMetrics => Set<ScaffExtHourlyView3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffExtSourceMetric3>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_ext3_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ScaffExtHourlyView3>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("scaff_ext3_hourly");
                entity.IsContinuousAggregate<ScaffExtHourlyView3, ScaffExtSourceMetric3>(
                    "scaff_ext3_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Bucket)]);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionOrderBy_For_Compressed_CAgg()
    {
        // Arrange
        string conn = await GetTestConnectionStringAsync();
        await using ScaffExtOrderByContext ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(conn);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "scaff_ext3_hourly")];

        Assert.True(info.CompressionEnabled);
        Assert.NotNull(info.CompressionOrderBy);
        Assert.NotEmpty(info.CompressionOrderBy);
        Assert.Contains("DESC", info.CompressionOrderBy[0]);
    }

    #endregion

    #region Should_Extract_CompressionEnabled_False_For_Uncompressed_CAgg

    private class ScaffExtSourceMetric4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffExtHourlyView4
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ScaffExtUncompressedContext(string connectionString) : DbContext
    {
        public DbSet<ScaffExtSourceMetric4> Metrics => Set<ScaffExtSourceMetric4>();
        public DbSet<ScaffExtHourlyView4> HourlyMetrics => Set<ScaffExtHourlyView4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffExtSourceMetric4>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_ext4_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ScaffExtHourlyView4>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("scaff_ext4_hourly");
                entity.IsContinuousAggregate<ScaffExtHourlyView4, ScaffExtSourceMetric4>(
                    "scaff_ext4_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionEnabled_False_For_Uncompressed_CAgg()
    {
        // Arrange
        string conn = await GetTestConnectionStringAsync();
        await using ScaffExtUncompressedContext ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(conn);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "scaff_ext4_hourly")];

        Assert.False(info.CompressionEnabled);
        Assert.NotNull(info.CompressionSegmentBy);
        Assert.Empty(info.CompressionSegmentBy);
        Assert.NotNull(info.CompressionOrderBy);
        Assert.Empty(info.CompressionOrderBy);
    }

    #endregion

    // ── Extractor: materialization hypertable key resolution ──────────────────

    #region Should_Key_Result_By_View_Name_Not_Materialization_Hypertable

    private class ScaffExtSourceMetric5
    {
        public DateTime Timestamp { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class ScaffExtHourlyView5
    {
        public DateTime Bucket { get; set; }
        public string Region { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    private class ScaffExtViewKeyContext(string connectionString) : DbContext
    {
        public DbSet<ScaffExtSourceMetric5> Metrics => Set<ScaffExtSourceMetric5>();
        public DbSet<ScaffExtHourlyView5> HourlyMetrics => Set<ScaffExtHourlyView5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffExtSourceMetric5>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_ext5_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ScaffExtHourlyView5>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("scaff_ext5_hourly");
                entity.IsContinuousAggregate<ScaffExtHourlyView5, ScaffExtSourceMetric5>(
                    "scaff_ext5_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Region)
                    .WithCompressionSegmentBy(x => x.Region);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.Property(x => x.Region).HasColumnName("Region");
            });
        }
    }

    [Fact]
    public async Task Should_Key_Result_By_View_Name_Not_Materialization_Hypertable()
    {
        // Arrange
        string conn = await GetTestConnectionStringAsync();
        await using ScaffExtViewKeyContext ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(conn);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "scaff_ext5_hourly")));
        bool hasMatKey = result.Keys.Any(k =>
            k.TableName.StartsWith("_materialized_hypertable_", StringComparison.OrdinalIgnoreCase) ||
            k.Schema.Contains("_timescaledb_internal", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasMatKey);
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "scaff_ext5_hourly")];
        Assert.True(info.CompressionEnabled);
        Assert.NotNull(info.CompressionSegmentBy);
        Assert.NotEmpty(info.CompressionSegmentBy);
    }

    #endregion
}
