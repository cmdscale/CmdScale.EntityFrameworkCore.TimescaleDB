using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Diagnostics.Internal;
using System.Diagnostics;
using Testcontainers.PostgreSql;

#pragma warning disable EF1001 // Internal EF Core API usage required for testing scaffolding infrastructure

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Integration tests for TimescaleDatabaseModelFactory.
/// Tests the full scaffolding pipeline from database to DatabaseModel with annotations.
/// </summary>
public class TimescaleDatabaseModelFactoryTests : MigrationTestBase, IAsyncLifetime
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
        string testDbName = $"test_db_{Guid.NewGuid():N}";

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

    private static TimescaleDatabaseModelFactory CreateFactory()
    {
        LoggerFactory loggerFactory = new();
        DiagnosticsLogger<DbLoggerCategory.Scaffolding> logger = new(
            loggerFactory,
            new LoggingOptions(),
            new DiagnosticListener("Test"),
            new NpgsqlLoggingDefinitions(),
            new NullDbContextLogger());

        return new TimescaleDatabaseModelFactory(logger);
    }

    private sealed class NullDbContextLogger : IDbContextLogger
    {
        public void Log(EventData eventData) { }
        public bool ShouldLog(EventId eventId, LogLevel logLevel) => false;
    }

    #region Should_Scaffold_Minimal_Hypertable

    private class MinimalHypertableMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalHypertableContext(string connectionString) : DbContext
    {
        public DbSet<MinimalHypertableMetric> Metrics => Set<MinimalHypertableMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalHypertableMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Minimal_Hypertable()
    {
        await using MinimalHypertableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        // Verify hypertable annotations
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
        Assert.Equal("Timestamp", metricsTable[HypertableAnnotations.HypertableTimeColumn]);
        Assert.NotNull(metricsTable[HypertableAnnotations.ChunkTimeInterval]);
        Assert.Equal(false, metricsTable[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Scaffold_Hypertable_With_Compression

    private class CompressionHypertableMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class CompressionHypertableContext(string connectionString) : DbContext
    {
        public DbSet<CompressionHypertableMetric> Metrics => Set<CompressionHypertableMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionHypertableMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Hypertable_With_Compression()
    {
        await using CompressionHypertableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
        Assert.Equal(true, metricsTable[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Scaffold_Hypertable_With_Hash_Dimension

    private class HashDimensionMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class HashDimensionContext(string connectionString) : DbContext
    {
        public DbSet<HashDimensionMetric> Metrics => Set<HashDimensionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HashDimensionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Hypertable_With_Hash_Dimension()
    {
        await using HashDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
        Assert.NotNull(metricsTable[HypertableAnnotations.AdditionalDimensions]);

        string? dimensionsJson = metricsTable[HypertableAnnotations.AdditionalDimensions] as string;
        Assert.NotNull(dimensionsJson);
        Assert.Contains("DeviceId", dimensionsJson);
        // EDimensionType.Hash = 1 in the enum, serialized as integer
        Assert.Contains("\"Type\":1", dimensionsJson);
    }

    #endregion

    #region Should_Scaffold_Hypertable_With_Reorder_Policy

    private class ReorderPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderPolicyContext(string connectionString) : DbContext
    {
        public DbSet<ReorderPolicyMetric> Metrics => Set<ReorderPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderPolicyMetric>(entity =>
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
    public async Task Should_Scaffold_Hypertable_With_Reorder_Policy()
    {
        await using ReorderPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        // Verify both hypertable and reorder policy annotations
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
        Assert.Equal(true, metricsTable[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("metrics_time_idx", metricsTable[ReorderPolicyAnnotations.IndexName]);
    }

    #endregion

    #region Should_Scaffold_Reorder_Policy_With_Custom_Parameters

    private class CustomReorderPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomReorderPolicyContext(string connectionString) : DbContext
    {
        public DbSet<CustomReorderPolicyMetric> Metrics => Set<CustomReorderPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomReorderPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          scheduleInterval: "12:00:00",
                          maxRetries: 5
                      );
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Reorder_Policy_With_Custom_Parameters()
    {
        await using CustomReorderPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        Assert.Equal(true, metricsTable[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("12:00:00", metricsTable[ReorderPolicyAnnotations.ScheduleInterval]);
        Assert.Equal(5, metricsTable[ReorderPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Scaffold_Continuous_Aggregate

    private class CaggSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class CaggHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ContinuousAggregateContext(string connectionString) : DbContext
    {
        public DbSet<CaggSourceMetric> Metrics => Set<CaggSourceMetric>();
        public DbSet<CaggHourlyMetric> HourlyMetrics => Set<CaggHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CaggHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<CaggHourlyMetric, CaggSourceMetric>(
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
    public async Task Should_Scaffold_Continuous_Aggregate()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using ContinuousAggregateContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(testConnectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics", "hourly_metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Verify source hypertable
        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);

        // Verify continuous aggregate
        DatabaseTable? caggTable = model.Tables.FirstOrDefault(t => t.Name == "hourly_metrics");
        Assert.NotNull(caggTable);
        Assert.Equal("hourly_metrics", caggTable[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("Metrics", caggTable[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion

    #region Should_Scaffold_Continuous_Aggregate_With_MaterializedOnly

    private class MatOnlySourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MatOnlyHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyContext(string connectionString) : DbContext
    {
        public DbSet<MatOnlySourceMetric> Metrics => Set<MatOnlySourceMetric>();
        public DbSet<MatOnlyHourlyMetric> HourlyMetrics => Set<MatOnlyHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatOnlySourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MatOnlyHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics_mat");
                entity.IsContinuousAggregate<MatOnlyHourlyMetric, MatOnlySourceMetric>(
                    "hourly_metrics_mat",
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
    public async Task Should_Scaffold_Continuous_Aggregate_With_MaterializedOnly()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MaterializedOnlyContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(testConnectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics", "hourly_metrics_mat"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? caggTable = model.Tables.FirstOrDefault(t => t.Name == "hourly_metrics_mat");
        Assert.NotNull(caggTable);
        Assert.Equal(true, caggTable[ContinuousAggregateAnnotations.MaterializedOnly]);
    }

    #endregion

    #region Should_Not_Apply_Hypertable_Annotations_To_Regular_Table

    private class RegularEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class RegularTableContext(string connectionString) : DbContext
    {
        public DbSet<RegularEntity> Entities => Set<RegularEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RegularEntity>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.ToTable("Entities");
            });
        }
    }

    [Fact]
    public async Task Should_Not_Apply_Hypertable_Annotations_To_Regular_Table()
    {
        await using RegularTableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Entities"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? entitiesTable = model.Tables.FirstOrDefault(t => t.Name == "Entities");
        Assert.NotNull(entitiesTable);

        // Should NOT have any TimescaleDB annotations
        Assert.Null(entitiesTable[HypertableAnnotations.IsHypertable]);
        Assert.Null(entitiesTable[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Null(entitiesTable[ReorderPolicyAnnotations.HasReorderPolicy]);
    }

    #endregion

    #region Should_Scaffold_Multiple_Hypertables

    private class MultiMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiMetric2
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultipleHypertablesContext(string connectionString) : DbContext
    {
        public DbSet<MultiMetric1> Metrics1 => Set<MultiMetric1>();
        public DbSet<MultiMetric2> Metrics2 => Set<MultiMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiMetric1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics1");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultiMetric2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics2");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Multiple_Hypertables()
    {
        await using MultipleHypertablesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics1", "Metrics2"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? table1 = model.Tables.FirstOrDefault(t => t.Name == "Metrics1");
        DatabaseTable? table2 = model.Tables.FirstOrDefault(t => t.Name == "Metrics2");

        Assert.NotNull(table1);
        Assert.NotNull(table2);

        Assert.Equal(true, table1[HypertableAnnotations.IsHypertable]);
        Assert.Equal(false, table1[HypertableAnnotations.EnableCompression]);

        Assert.Equal(true, table2[HypertableAnnotations.IsHypertable]);
        Assert.Equal(true, table2[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Scaffold_Mixed_Regular_And_Hypertables

    private class MixedRegularEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class MixedHypertableMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MixedContext(string connectionString) : DbContext
    {
        public DbSet<MixedRegularEntity> Entities => Set<MixedRegularEntity>();
        public DbSet<MixedHypertableMetric> Metrics => Set<MixedHypertableMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MixedRegularEntity>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.ToTable("Entities");
            });

            modelBuilder.Entity<MixedHypertableMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Mixed_Regular_And_Hypertables()
    {
        await using MixedContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Entities", "Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? entitiesTable = model.Tables.FirstOrDefault(t => t.Name == "Entities");
        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");

        Assert.NotNull(entitiesTable);
        Assert.NotNull(metricsTable);

        // Regular table should NOT have hypertable annotations
        Assert.Null(entitiesTable[HypertableAnnotations.IsHypertable]);

        // Hypertable should have annotations
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
    }

    #endregion

    #region Should_Handle_Table_With_Null_Schema

    private class NullSchemaMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullSchemaContext(string connectionString) : DbContext
    {
        public DbSet<NullSchemaMetric> Metrics => Set<NullSchemaMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullSchemaMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics", schema: "public");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Handle_Table_In_Public_Schema()
    {
        await using NullSchemaContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);
        Assert.Equal("public", metricsTable.Schema);
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
    }

    #endregion

    #region Should_Scaffold_Full_Configuration

    private class FullConfigMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class FullConfigHourly
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class FullConfigurationContext(string connectionString) : DbContext
    {
        public DbSet<FullConfigMetric> Metrics => Set<FullConfigMetric>();
        public DbSet<FullConfigHourly> HourlyMetrics => Set<FullConfigHourly>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullConfigMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.HasIndex(x => x.Timestamp, "metrics_time_idx");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression()
                      .HasDimension(Dimension.CreateHash("DeviceId", 4))
                      .WithReorderPolicy("metrics_time_idx");
            });

            modelBuilder.Entity<FullConfigHourly>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<FullConfigHourly, FullConfigMetric>(
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
    public async Task Should_Scaffold_Full_Configuration()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using FullConfigurationContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(testConnectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics", "hourly_metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Verify hypertable with all features
        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
        Assert.Equal(true, metricsTable[HypertableAnnotations.EnableCompression]);
        Assert.NotNull(metricsTable[HypertableAnnotations.AdditionalDimensions]);
        Assert.Equal(true, metricsTable[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("metrics_time_idx", metricsTable[ReorderPolicyAnnotations.IndexName]);

        // Verify continuous aggregate
        DatabaseTable? caggTable = model.Tables.FirstOrDefault(t => t.Name == "hourly_metrics");
        Assert.NotNull(caggTable);
        Assert.Equal("hourly_metrics", caggTable[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("Metrics", caggTable[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion

    #region Should_Scaffold_With_Already_Open_Connection

    private class OpenConnectionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OpenConnectionContext(string connectionString) : DbContext
    {
        public DbSet<OpenConnectionMetric> Metrics => Set<OpenConnectionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OpenConnectionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_With_Already_Open_Connection()
    {
        await using OpenConnectionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);
        connection.Open();

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Connection should remain open
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
    }

    #endregion

    #region Should_Handle_Empty_Database

    private class EmptyDbEntity
    {
        public int Id { get; set; }
    }

    private class EmptyDatabaseContext(string connectionString) : DbContext
    {
        public DbSet<EmptyDbEntity> Entities => Set<EmptyDbEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();
    }

    [Fact]
    public async Task Should_Handle_Empty_Database()
    {
        // Don't create any tables - just scaffold an empty database
        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: [], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Model should be valid but have no TimescaleDB-specific tables
        Assert.NotNull(model);
    }

    #endregion

    #region Should_Scaffold_Hypertable_With_Custom_Chunk_Interval

    private class CustomChunkMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomChunkIntervalContext(string connectionString) : DbContext
    {
        public DbSet<CustomChunkMetric> Metrics => Set<CustomChunkMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomChunkMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("86400000"); // 1 day in milliseconds
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Hypertable_With_Custom_Chunk_Interval()
    {
        await using CustomChunkIntervalContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);

        // Verify chunk interval is extracted
        object? chunkInterval = metricsTable[HypertableAnnotations.ChunkTimeInterval];
        Assert.NotNull(chunkInterval);
    }

    #endregion

    #region Should_Preserve_Base_Model_Structure

    private class PreserveStructureMetric
    {
        public DateTime Timestamp { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class PreserveStructureContext(string connectionString) : DbContext
    {
        public DbSet<PreserveStructureMetric> Metrics => Set<PreserveStructureMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PreserveStructureMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.Property(x => x.Name).HasMaxLength(100);
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Preserve_Base_Model_Structure()
    {
        await using PreserveStructureContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(_connectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? metricsTable = model.Tables.FirstOrDefault(t => t.Name == "Metrics");
        Assert.NotNull(metricsTable);

        // Verify columns are preserved
        Assert.Contains(metricsTable.Columns, c => c.Name == "Timestamp");
        Assert.Contains(metricsTable.Columns, c => c.Name == "Name");
        Assert.Contains(metricsTable.Columns, c => c.Name == "Value");

        // Verify TimescaleDB annotations are added
        Assert.Equal(true, metricsTable[HypertableAnnotations.IsHypertable]);
    }

    #endregion

    #region Should_Scaffold_Multiple_Continuous_Aggregates

    private class MultiCaggSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiCaggHourly
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultiCaggDaily
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultipleCaggContext(string connectionString) : DbContext
    {
        public DbSet<MultiCaggSource> Metrics => Set<MultiCaggSource>();
        public DbSet<MultiCaggHourly> HourlyMetrics => Set<MultiCaggHourly>();
        public DbSet<MultiCaggDaily> DailyMetrics => Set<MultiCaggDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiCaggSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultiCaggHourly>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.IsContinuousAggregate<MultiCaggHourly, MultiCaggSource>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });

            modelBuilder.Entity<MultiCaggDaily>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("daily_metrics");
                entity.IsContinuousAggregate<MultiCaggDaily, MultiCaggSource>(
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
    public async Task Should_Scaffold_Multiple_Continuous_Aggregates()
    {
        string testConnectionString = await GetTestConnectionStringAsync();
        await using MultipleCaggContext context = new(testConnectionString);
        await CreateDatabaseViaMigrationAsync(context);

        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(testConnectionString);

        DatabaseModelFactoryOptions options = new(tables: ["Metrics", "hourly_metrics", "daily_metrics"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        DatabaseTable? hourlyTable = model.Tables.FirstOrDefault(t => t.Name == "hourly_metrics");
        DatabaseTable? dailyTable = model.Tables.FirstOrDefault(t => t.Name == "daily_metrics");

        Assert.NotNull(hourlyTable);
        Assert.NotNull(dailyTable);

        Assert.Equal("hourly_metrics", hourlyTable[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("daily_metrics", dailyTable[ContinuousAggregateAnnotations.MaterializedViewName]);

        // Both should reference the same source
        Assert.Equal("Metrics", hourlyTable[ContinuousAggregateAnnotations.ParentName]);
        Assert.Equal("Metrics", dailyTable[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion
}
