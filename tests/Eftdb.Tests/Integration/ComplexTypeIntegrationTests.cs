using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using System.ComponentModel.DataAnnotations.Schema;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ComplexTypeIntegrationTests : MigrationTestBase, IAsyncLifetime
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

    private static async Task<bool> ContinuousAggregateExistsAsync(string connectionString, string viewName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.continuous_aggregates
            WHERE view_name = @viewName;";
        cmd.Parameters.AddWithValue("viewName", viewName);
        object? result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    private static async Task<bool> IsHypertableAsync(string connectionString, string tableName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.hypertables
            WHERE hypertable_name = @tableName;";
        cmd.Parameters.AddWithValue("tableName", tableName);
        object? result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    private static IReadOnlyList<MigrationOperation> GetOperations(DbContext? source, DbContext target)
    {
        IMigrationsModelDiffer differ = target.GetService<IMigrationsModelDiffer>();
        IRelationalModel? sourceModel = source?.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        IRelationalModel targetModel = target.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        return differ.GetDifferences(sourceModel, targetModel);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    #region Should_Create_Hypertable_With_ComplexType_TimeColumn

    [ComplexType]
    private class HtMeta1
    {
        public DateTime Timestamp { get; set; }
    }

    private class HtComplexEntity1
    {
        public double Value { get; set; }
        public HtMeta1 Meta { get; set; } = new();
    }

    private class HtComplexContext1(string connectionString) : DbContext
    {
        public DbSet<HtComplexEntity1> Metrics => Set<HtComplexEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HtComplexEntity1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ct_ht1_metrics");
                entity.IsHypertable<HtComplexEntity1, DateTime>(x => x.Meta.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_ComplexType_TimeColumn()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();

        // Act
        await using HtComplexContext1 ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Assert
        bool isHypertable = await IsHypertableAsync(conn, "ct_ht1_metrics");
        Assert.True(isHypertable);
    }

    #endregion

    #region Should_Create_ContinuousAggregate_With_ComplexType_AggregateFunction_And_GroupBy

    [ComplexType]
    private class CaMeta2
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class CaSource2
    {
        public double Value { get; set; }
        public CaMeta2 Meta { get; set; } = new();
    }

    private class CaAggregate2
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    private class CaComplexContext2(string connectionString) : DbContext
    {
        public DbSet<CaSource2> Metrics => Set<CaSource2>();
        public DbSet<CaAggregate2> HourlyMetrics => Set<CaAggregate2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaSource2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ct_ca2_metrics");
                entity.IsHypertable<CaSource2, DateTime>(x => x.Meta.Timestamp);
            });

            modelBuilder.Entity<CaAggregate2>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("ct_ca2_hourly");
                entity.IsContinuousAggregate<CaAggregate2, CaSource2, DateTime>(
                    "ct_ca2_hourly",
                    "1 hour",
                    x => x.Meta.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                ).AddGroupByColumn(x => x.Meta.DeviceId);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.Property(x => x.DeviceId).HasColumnName("DeviceId");
            });
        }
    }

    [Fact]
    public async Task Should_Create_ContinuousAggregate_With_ComplexType_AggregateFunction_And_GroupBy()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();

        // Act
        await using CaComplexContext2 ctx = new(conn);
        await CreateDatabaseViaMigrationAsync(ctx);

        // Assert
        bool caExists = await ContinuousAggregateExistsAsync(conn, "ct_ca2_hourly");
        Assert.True(caExists);
    }

    #endregion

    #region Should_Produce_Zero_Operations_On_Round_Trip_Diff_With_ComplexType

    [ComplexType]
    private class RtMeta3
    {
        public DateTime Timestamp { get; set; }
        public string TenantId { get; set; } = string.Empty;
    }

    private class RtSource3
    {
        public double Value { get; set; }
        public RtMeta3 Meta { get; set; } = new();
    }

    private class RtContext3(string connectionString) : DbContext
    {
        public DbSet<RtSource3> Metrics => Set<RtSource3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RtSource3>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ct_rt3_metrics");
                entity.IsHypertable<RtSource3, DateTime>(x => x.Meta.Timestamp)
                      .WithCompressionSegmentBy(x => x.Meta.TenantId);
            });
        }
    }

    [Fact]
    public async Task Should_Produce_Zero_Operations_On_Round_Trip_Diff_With_ComplexType()
    {
        // Arrange
        string conn = await GetIsolatedConnectionStringAsync();
        await using RtContext3 initial = new(conn);
        await CreateDatabaseViaMigrationAsync(initial);

        // Act
        await using RtContext3 same = new(conn);
        IReadOnlyList<MigrationOperation> ops = GetOperations(initial, same);

        // Assert
        Assert.Empty(ops);
    }

    #endregion
}
