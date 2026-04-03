using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class RetentionPolicyScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
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

    #region Should_Extract_Minimal_RetentionPolicy_With_DropAfter

    private class DropAfterMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropAfterContext(string connectionString) : DbContext
    {
        public DbSet<DropAfterMetric> Metrics => Set<DropAfterMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropAfterMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_drop_after");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_RetentionPolicy_With_DropAfter()
    {
        await using DropAfterContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_retention_drop_after")));

        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_drop_after")];
        Assert.Equal("7 days", info.DropAfter);
        Assert.Null(info.DropCreatedBefore);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_DropCreatedBefore

    private class DropCreatedBeforeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<DropCreatedBeforeMetric> Metrics => Set<DropCreatedBeforeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropCreatedBeforeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_dcb");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropCreatedBefore: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_RetentionPolicy_With_DropCreatedBefore()
    {
        await using DropCreatedBeforeContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_retention_dcb")));

        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_dcb")];
        Assert.Null(info.DropAfter);
        Assert.Equal("30 days", info.DropCreatedBefore);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_All_Job_Settings

    private class AllSettingsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AllSettingsContext(string connectionString) : DbContext
    {
        public DbSet<AllSettingsMetric> Metrics => Set<AllSettingsMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllSettingsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_all_settings");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(
                          dropAfter: "14 days",
                          scheduleInterval: "1 day",
                          maxRuntime: "02:00:00",
                          maxRetries: 5,
                          retryPeriod: "00:15:00");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_RetentionPolicy_With_All_Job_Settings()
    {
        await using AllSettingsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_all_settings")];

        Assert.Equal("14 days", info.DropAfter);
        Assert.Null(info.DropCreatedBefore);
        Assert.Equal("1 day", info.ScheduleInterval);
        Assert.Equal("02:00:00", info.MaxRuntime);
        Assert.Equal(5, info.MaxRetries);
        Assert.Equal("00:15:00", info.RetryPeriod);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_InitialStart

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
                entity.ToTable("scaff_retention_initial_start");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public async Task Should_Extract_RetentionPolicy_With_InitialStart()
    {
        await using InitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_initial_start")];

        Assert.NotNull(info.InitialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, info.InitialStart.Value);
    }

    #endregion

    #region Should_Extract_Multiple_RetentionPolicies

    private class MultiPolicyMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiPolicyMetric2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiPolicyContext(string connectionString) : DbContext
    {
        public DbSet<MultiPolicyMetric1> Metrics1 => Set<MultiPolicyMetric1>();
        public DbSet<MultiPolicyMetric2> Metrics2 => Set<MultiPolicyMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiPolicyMetric1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_multi_1");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });

            modelBuilder.Entity<MultiPolicyMetric2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_multi_2");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_RetentionPolicies()
    {
        await using MultiPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "scaff_retention_multi_1")));
        Assert.True(result.ContainsKey(("public", "scaff_retention_multi_2")));

        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info1 =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_multi_1")];
        Assert.Equal("7 days", info1.DropAfter);

        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info2 =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("public", "scaff_retention_multi_2")];
        Assert.Equal("30 days", info2.DropAfter);
    }

    #endregion

    #region Should_Return_Empty_When_No_Policies

    private class NoPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoPolicyContext(string connectionString) : DbContext
    {
        public DbSet<NoPolicyMetric> Metrics => Set<NoPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_none");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Policies()
    {
        await using NoPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Empty(result);
    }

    #endregion

    #region Should_Handle_Connection_Already_Open

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
                entity.ToTable("scaff_retention_open_conn");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_Connection_Already_Open()
    {
        await using OpenConnectionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_retention_open_conn")));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    #endregion

    #region Should_Handle_Connection_Closed

    private class ClosedConnectionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ClosedConnectionContext(string connectionString) : DbContext
    {
        public DbSet<ClosedConnectionMetric> Metrics => Set<ClosedConnectionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClosedConnectionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_closed_conn");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_Connection_Closed()
    {
        await using ClosedConnectionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);

        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_retention_closed_conn")));
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_Custom_Schema

    private class CustomSchemaMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomSchemaContext(string connectionString) : DbContext
    {
        public DbSet<CustomSchemaMetric> Metrics => Set<CustomSchemaMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("analytics");

            modelBuilder.Entity<CustomSchemaMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_retention_custom_schema");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_RetentionPolicy_With_Custom_Schema()
    {
        await using CustomSchemaContext context = new(_connectionString!);
        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS analytics;", [], TestContext.Current.CancellationToken);
        await CreateDatabaseViaMigrationAsync(context);

        RetentionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("analytics", "scaff_retention_custom_schema")));

        RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo info =
            (RetentionPolicyScaffoldingExtractor.RetentionPolicyInfo)result[("analytics", "scaff_retention_custom_schema")];
        Assert.Equal("7 days", info.DropAfter);
    }

    #endregion
}
