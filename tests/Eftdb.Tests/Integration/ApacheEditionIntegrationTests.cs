using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Verifies <c>UseApacheEdition()</c> against the Apache (OSS) TimescaleDB image: models that
/// request Community-only features apply through the migration pipeline without throwing, and
/// the Community-only side effects are omitted from the generated SQL.
/// </summary>
public class ApacheEditionIntegrationTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(TimescaleImages.Apache)
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Helper Methods

    private static async Task<bool> HasAnyPolicyJobAsync(DbContext context)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM timescaledb_information.jobs
            WHERE proc_name IN ('policy_retention', 'policy_reorder', 'policy_compression', 'policy_columnstore', 'policy_refresh_continuous_aggregate');
        ";

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is long count && count > 0;
    }

    private static async Task<int> GetContinuousAggregateCountAsync(DbContext context)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM timescaledb_information.continuous_aggregates;
        ";

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is long count ? (int)count : 0;
    }

    #endregion

    #region Should_Apply_HypertablePolicies_Model_Without_Throwing_On_Apache

    private class GuardedMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class GuardedPoliciesContext(string connectionString) : DbContext
    {
        public DbSet<GuardedMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb(o => o.UseApacheEdition());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GuardedMetric>(entity =>
            {
                entity.ToTable("apache_guarded_metrics");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .EnableCompression(true)
                      .WithCompressionOrderBy(s => s.ByDescending(x => x.Time))
                      .WithChunkSkipping(x => x.DeviceId);
                entity.WithRetentionPolicy(dropAfter: "30 days");
                entity.WithReorderPolicy("apache_guarded_metrics_time_idx");
                entity.WithCompressionPolicy(after: "7 days");
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("apache_guarded_metrics_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Apply_HypertablePolicies_Model_Without_Throwing_On_Apache()
    {
        await using GuardedPoliciesContext context = new(_connectionString!);

        await CreateDatabaseViaMigrationAsync(context);

        bool isHypertable = await HypertableProbe.IsHypertableAsync(context, "apache_guarded_metrics");
        Assert.True(isHypertable);

        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "apache_guarded_metrics");
        Assert.False(compressionEnabled);

        bool hasPolicyJob = await HasAnyPolicyJobAsync(context);
        Assert.False(hasPolicyJob);
    }

    #endregion

    #region Should_Apply_ContinuousAggregate_WithData_Model_Without_Throwing_On_Apache

    private class CaggSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class GuardedCaggContext(string connectionString) : DbContext
    {
        public DbSet<CaggSourceMetric> Metrics => Set<CaggSourceMetric>();
        public DbSet<CaggAggregate> Aggregates => Set<CaggAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb(o => o.UseApacheEdition());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggSourceMetric>(entity =>
            {
                entity.ToTable("apache_cagg_source");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CaggAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CaggAggregate, CaggSourceMetric>(
                        "apache_cagg_with_data",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithNoData(false)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");

                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.Property(x => x.AvgValue).HasColumnName("AvgValue");
            });
        }
    }

    [Fact]
    public async Task Should_Apply_ContinuousAggregate_WithData_Model_Without_Throwing_On_Apache()
    {
        await using GuardedCaggContext context = new(_connectionString!);

        await CreateDatabaseViaMigrationAsync(context);

        int caggCount = await GetContinuousAggregateCountAsync(context);
        Assert.Equal(0, caggCount);

        bool hasPolicyJob = await HasAnyPolicyJobAsync(context);
        Assert.False(hasPolicyJob);
    }

    #endregion
}
