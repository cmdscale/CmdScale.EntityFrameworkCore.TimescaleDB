using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ContinuousAggregateCompressionIntegrationTests : MigrationTestBase, IAsyncLifetime
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
        GC.SuppressFinalize(this);
    }

    private async Task<string> GetIsolatedConnectionStringAsync()
    {
        string dbName = $"test_db_{Guid.NewGuid():N}";

        await using NpgsqlConnection admin = new(_connectionString);
        await admin.OpenAsync();
        await using (NpgsqlCommand cmd = new($"CREATE DATABASE {dbName}", admin))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        string isolated = _connectionString!.Replace("test_db", dbName, StringComparison.OrdinalIgnoreCase);
        await using NpgsqlConnection conn = new(isolated);
        await conn.OpenAsync();
        await using (NpgsqlCommand ext = new("CREATE EXTENSION IF NOT EXISTS timescaledb", conn))
        {
            await ext.ExecuteNonQueryAsync();
        }

        return isolated;
    }

    private static async Task<bool> IsCompressionEnabledAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT compression_enabled
            FROM timescaledb_information.continuous_aggregates
            WHERE view_name = @viewName;";
        cmd.Parameters.AddWithValue("viewName", viewName);
        object? result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    private static async Task<List<string>> GetCompressionSegmentByAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cs.attname
            FROM timescaledb_information.compression_settings cs
            INNER JOIN _timescaledb_catalog.continuous_agg cagg
                ON cagg.mat_hypertable_id = (
                    SELECT h.id FROM _timescaledb_catalog.hypertable h
                    WHERE h.schema_name = cs.hypertable_schema
                      AND h.table_name  = cs.hypertable_name
                )
            WHERE cagg.user_view_name = @viewName
              AND cs.segmentby_column_index IS NOT NULL
            ORDER BY cs.segmentby_column_index;";
        cmd.Parameters.AddWithValue("viewName", viewName);

        List<string> cols = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cols.Add(reader.GetString(0));
        }
        return cols;
    }

    private static async Task<List<string>> GetCompressionOrderByAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cs.attname, cs.orderby_asc
            FROM timescaledb_information.compression_settings cs
            INNER JOIN _timescaledb_catalog.continuous_agg cagg
                ON cagg.mat_hypertable_id = (
                    SELECT h.id FROM _timescaledb_catalog.hypertable h
                    WHERE h.schema_name = cs.hypertable_schema
                      AND h.table_name  = cs.hypertable_name
                )
            WHERE cagg.user_view_name = @viewName
              AND cs.orderby_column_index IS NOT NULL
            ORDER BY cs.orderby_column_index;";
        cmd.Parameters.AddWithValue("viewName", viewName);

        List<string> cols = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string col = reader.GetString(0);
            bool asc = reader.GetBoolean(1);
            cols.Add($"{col} {(asc ? "ASC" : "DESC")}");
        }
        return cols;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    #region Should_Create_CAgg_With_Compression_Enabled

    private class CompSourceMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompHourlyView1
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CompressedCaggContext1(string connectionString) : DbContext
    {
        public DbSet<CompSourceMetric1> Metrics => Set<CompSourceMetric1>();
        public DbSet<CompHourlyView1> HourlyMetrics => Set<CompHourlyView1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompSourceMetric1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_comp1_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CompHourlyView1>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_comp1_hourly");
                entity.IsContinuousAggregate<CompHourlyView1, CompSourceMetric1>(
                    "cagg_comp1_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression(true);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Create_CAgg_With_Compression_Enabled()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();

        // Act
        await using CompressedCaggContext1 ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Assert
        bool enabled = await IsCompressionEnabledAsync(conn, "cagg_comp1_hourly");
        Assert.True(enabled);
    }

    #endregion

    #region Should_Create_CAgg_With_CompressionSegmentBy_And_OrderBy

    private class CompSourceMetric2
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class CompHourlyView2
    {
        public DateTime TimeBucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    private class CompressedSegByContext(string connectionString) : DbContext
    {
        public DbSet<CompSourceMetric2> Metrics => Set<CompSourceMetric2>();
        public DbSet<CompHourlyView2> HourlyMetrics => Set<CompHourlyView2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompSourceMetric2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_comp2_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CompHourlyView2>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_comp2_hourly");
                entity.IsContinuousAggregate<CompHourlyView2, CompSourceMetric2>(
                    "cagg_comp2_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.DeviceId)
                    .WithCompressionSegmentBy(x => x.DeviceId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.TimeBucket)]);

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.Property(x => x.DeviceId).HasColumnName("DeviceId");
            });
        }
    }

    [Fact]
    public async Task Should_Create_CAgg_With_CompressionSegmentBy_And_OrderBy()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();

        // Act
        await using CompressedSegByContext ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Assert
        bool enabled = await IsCompressionEnabledAsync(conn, "cagg_comp2_hourly");
        Assert.True(enabled);

        List<string> segmentBy = await GetCompressionSegmentByAsync(conn, "cagg_comp2_hourly");
        Assert.Equal("DeviceId", Assert.Single(segmentBy));

        List<string> orderBy = await GetCompressionOrderByAsync(conn, "cagg_comp2_hourly");
        Assert.Equal("time_bucket DESC", Assert.Single(orderBy));
    }

    #endregion

    #region Should_Alter_CAgg_To_Change_CompressionOrderBy

    private class AltSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AltHourlyView
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AltOrderByInitialContext(string connectionString) : DbContext
    {
        public DbSet<AltSourceMetric> Metrics => Set<AltSourceMetric>();
        public DbSet<AltHourlyView> HourlyMetrics => Set<AltHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AltSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_alt_ord_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AltHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_alt_ord_hourly");
                entity.IsContinuousAggregate<AltHourlyView, AltSourceMetric>(
                    "cagg_alt_ord_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompressionOrderBy(s => [s.ByAscending(x => x.TimeBucket)]);

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    private class AltOrderByModifiedContext(string connectionString) : DbContext
    {
        public DbSet<AltSourceMetric> Metrics => Set<AltSourceMetric>();
        public DbSet<AltHourlyView> HourlyMetrics => Set<AltHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AltSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_alt_ord_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AltHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_alt_ord_hourly");
                entity.IsContinuousAggregate<AltHourlyView, AltSourceMetric>(
                    "cagg_alt_ord_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.TimeBucket)]);

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_CAgg_To_Change_CompressionOrderBy()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();
        await using AltOrderByInitialContext initial = new(conn);
        await CreateDatabaseViaMigrationAsync(initial);

        List<string> before = await GetCompressionOrderByAsync(conn, "cagg_alt_ord_hourly");
        Assert.Contains("ASC", Assert.Single(before));

        // Act
        await using AltOrderByModifiedContext modified = new(conn);
        await AlterDatabaseViaMigrationAsync(initial, modified);

        // Assert
        List<string> after = await GetCompressionOrderByAsync(conn, "cagg_alt_ord_hourly");
        Assert.Contains("DESC", Assert.Single(after));
    }

    #endregion

    #region Should_Disable_CAgg_Compression

    private class DisableSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DisableHourlyView
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DisableCompressedContext(string connectionString) : DbContext
    {
        public DbSet<DisableSourceMetric> Metrics => Set<DisableSourceMetric>();
        public DbSet<DisableHourlyView> HourlyMetrics => Set<DisableHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DisableSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_dis_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DisableHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_dis_hourly");
                entity.IsContinuousAggregate<DisableHourlyView, DisableSourceMetric>(
                    "cagg_dis_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression(true);

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    private class DisableUncompressedContext(string connectionString) : DbContext
    {
        public DbSet<DisableSourceMetric> Metrics => Set<DisableSourceMetric>();
        public DbSet<DisableHourlyView> HourlyMetrics => Set<DisableHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DisableSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_dis_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DisableHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_dis_hourly");
                entity.IsContinuousAggregate<DisableHourlyView, DisableSourceMetric>(
                    "cagg_dis_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    [Fact]
    public async Task Should_Disable_CAgg_Compression()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();
        await using DisableCompressedContext compressed = new(conn);
        await CreateDatabaseViaMigrationAsync(compressed);

        bool before = await IsCompressionEnabledAsync(conn, "cagg_dis_hourly");
        Assert.True(before);

        // Act
        await using DisableUncompressedContext uncompressed = new(conn);
        await AlterDatabaseViaMigrationAsync(compressed, uncompressed);

        // Assert
        bool after = await IsCompressionEnabledAsync(conn, "cagg_dis_hourly");
        Assert.False(after);
    }

    #endregion

    #region Should_Add_CompressionPolicy_On_Compressed_CAgg

    private class PolicySourceMetric5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class PolicyHourlyView5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggWithRefreshContext5(string connectionString) : DbContext
    {
        public DbSet<PolicySourceMetric5> Metrics => Set<PolicySourceMetric5>();
        public DbSet<PolicyHourlyView5> HourlyMetrics => Set<PolicyHourlyView5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceMetric5>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_comp5_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<PolicyHourlyView5>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_comp5_hourly");
                entity.IsContinuousAggregate<PolicyHourlyView5, PolicySourceMetric5>(
                    "cagg_comp5_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression(true)
                    .WithRefreshPolicy(startOffset: "3 hours", endOffset: "1 hour", scheduleInterval: "1 hour");

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    private class CaggWithCompressionPolicyContext5(string connectionString) : DbContext
    {
        public DbSet<PolicySourceMetric5> Metrics => Set<PolicySourceMetric5>();
        public DbSet<PolicyHourlyView5> HourlyMetrics => Set<PolicyHourlyView5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceMetric5>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_comp5_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<PolicyHourlyView5>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_comp5_hourly");
                entity.IsContinuousAggregate<PolicyHourlyView5, PolicySourceMetric5>(
                    "cagg_comp5_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression(true)
                    .WithRefreshPolicy(startOffset: "3 hours", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.WithCompressionPolicy(after: "7 days");

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            });
        }
    }

    private static async Task<bool> HasCompressionPolicyOnCaggAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.jobs
            WHERE proc_name IN ('policy_compression', 'policy_columnstore')
              AND hypertable_name = @viewName;";
        cmd.Parameters.AddWithValue("viewName", viewName);
        object? result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    private static async Task<string?> GetCaggCompressionPolicyConfigAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT config::text
            FROM timescaledb_information.jobs
            WHERE proc_name IN ('policy_compression', 'policy_columnstore')
              AND hypertable_name = @viewName
            LIMIT 1;";
        cmd.Parameters.AddWithValue("viewName", viewName);
        object? result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    [Fact]
    public async Task Should_Add_CompressionPolicy_On_Compressed_CAgg()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();
        await using CaggWithRefreshContext5 initial = new(conn);
        await CreateDatabaseViaMigrationAsync(initial);

        // Act
        await using CaggWithCompressionPolicyContext5 withPolicy = new(conn);
        await AlterDatabaseViaMigrationAsync(initial, withPolicy);

        // Assert
        bool compressionEnabled = await IsCompressionEnabledAsync(conn, "cagg_comp5_hourly");
        Assert.True(compressionEnabled);
        bool hasPolicy = await HasCompressionPolicyOnCaggAsync(conn, "cagg_comp5_hourly");
        Assert.True(hasPolicy);
        string? config = await GetCaggCompressionPolicyConfigAsync(conn, "cagg_comp5_hourly");
        Assert.NotNull(config);
        Assert.Contains("compress_after", config);
    }

    #endregion

    #region Should_Create_CAgg_With_RefreshPolicy_And_CompressionPolicy_In_Single_Migration

    [Fact]
    public async Task Should_Create_CAgg_With_RefreshPolicy_And_CompressionPolicy_In_Single_Migration()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();
        await using CaggWithCompressionPolicyContext5 context = new(conn);

        // Act
        await CreateDatabaseViaMigrationAsync(context);

        // Assert
        bool hasPolicy = await HasCompressionPolicyOnCaggAsync(conn, "cagg_comp5_hourly");
        Assert.True(hasPolicy);
        string? config = await GetCaggCompressionPolicyConfigAsync(conn, "cagg_comp5_hourly");
        Assert.NotNull(config);
        Assert.Contains("compress_after", config);
    }

    #endregion
}
