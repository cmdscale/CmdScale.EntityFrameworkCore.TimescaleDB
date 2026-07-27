using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class CompressionPolicyScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
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

    #region Should_Extract_Minimal_CompressionPolicy_With_After

    private class AfterMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AfterContext(string connectionString) : DbContext
    {
        public DbSet<AfterMetric> Metrics => Set<AfterMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AfterMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_after");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_CompressionPolicy_With_After()
    {
        await using AfterContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_compression_after")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_after")];
        Assert.Equal("7 days", info.After);
        Assert.Null(info.CreatedBefore);
    }

    #endregion

    #region Should_Extract_CompressionPolicy_With_CreatedBefore

    private class CreatedBeforeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<CreatedBeforeMetric> Metrics => Set<CreatedBeforeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreatedBeforeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_cb");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionPolicy_With_CreatedBefore()
    {
        await using CreatedBeforeContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_compression_cb")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_cb")];
        Assert.Null(info.After);
        Assert.Equal("30 days", info.CreatedBefore);
    }

    #endregion

    #region Should_Extract_CompressionPolicy_With_ScheduleInterval

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
                entity.ToTable("scaff_compression_schedule");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "14 days", scheduleInterval: "6 hours");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionPolicy_With_ScheduleInterval()
    {
        await using ScheduleIntervalContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.True(result.ContainsKey(("public", "scaff_compression_schedule")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_schedule")];
        Assert.Equal("14 days", info.After);
        Assert.Equal("6 hours", info.ScheduleInterval);
    }

    #endregion

    #region Should_Extract_CompressionPolicy_With_InitialStart

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
                entity.ToTable("scaff_compression_initial_start");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(
                          after: "7 days",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionPolicy_With_InitialStart()
    {
        await using InitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.True(result.ContainsKey(("public", "scaff_compression_initial_start")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_initial_start")];
        Assert.NotNull(info.InitialStart);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), info.InitialStart.Value.ToUniversalTime());
    }

    #endregion

    #region Should_Extract_Multiple_CompressionPolicies

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
                entity.ToTable("scaff_compression_multi_1");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "7 days");
            });

            modelBuilder.Entity<MultiPolicyMetric2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_multi_2");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_CompressionPolicies()
    {
        await using MultiPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "scaff_compression_multi_1")));
        Assert.True(result.ContainsKey(("public", "scaff_compression_multi_2")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info1 =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_multi_1")];
        Assert.Equal("7 days", info1.After);

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info2 =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_multi_2")];
        Assert.Equal("30 days", info2.After);
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
                entity.ToTable("scaff_compression_none");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp));
            });
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Policies()
    {
        await using NoPolicyContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
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
                entity.ToTable("scaff_compression_open_conn");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_Connection_Already_Open()
    {
        await using OpenConnectionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_compression_open_conn")));
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
                entity.ToTable("scaff_compression_closed_conn");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_Connection_Closed()
    {
        await using ClosedConnectionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);

        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "scaff_compression_closed_conn")));
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    #endregion

    #region Should_Extract_IfNotExists_Is_Always_Null

    private class IfNotExistsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IfNotExistsContext(string connectionString) : DbContext
    {
        public DbSet<IfNotExistsMetric> Metrics => Set<IfNotExistsMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IfNotExistsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_if_not_exists");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "7 days", ifNotExists: true);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_IfNotExists_Is_Always_Null()
    {
        await using IfNotExistsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.True(result.ContainsKey(("public", "scaff_compression_if_not_exists")));

        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_if_not_exists")];
        Assert.Null(info.IfNotExists);
    }

    #endregion

    #region Should_Suppress_ScheduleInterval_Annotation_When_Default_For_Sub_Day_Chunk_Interval

    private class SubDayChunkMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SubDayChunkContext(string connectionString) : DbContext
    {
        public DbSet<SubDayChunkMetric> Metrics => Set<SubDayChunkMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubDayChunkMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_subday");
                entity.IsHypertable(x => x.Timestamp).WithChunkTimeInterval("4 hours")
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "1 day");
            });
        }
    }

    [Fact]
    public async Task Should_Suppress_ScheduleInterval_Annotation_When_Default_For_Sub_Day_Chunk_Interval()
    {
        // Arrange
        await using SubDayChunkContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);

        // Act
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "scaff_compression_subday")));
        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_subday")];

        DatabaseTable table = new() { Name = "scaff_compression_subday", Schema = "public" };
        table[HypertableAnnotations.ChunkTimeInterval] = "4 hours";
        CompressionPolicyAnnotationApplier applier = new();
        applier.ApplyAnnotations(table, info);
        Assert.Null(table[CompressionPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Extract_CompressionPolicy_With_Timezone

    private class TimezoneMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimezoneContext(string connectionString) : DbContext
    {
        public DbSet<TimezoneMetric> Metrics => Set<TimezoneMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimezoneMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_tz");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(
                          after: "7 days",
                          initialStart: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                          timezone: "Europe/Berlin");
            });
        }
    }

    [Fact]
    public async Task Should_Extract_CompressionPolicy_With_Timezone()
    {
        // Arrange
        await using TimezoneContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);

        // Act
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "scaff_compression_tz")));
        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_tz")];

        Assert.Equal("Europe/Berlin", info.Timezone);

        DatabaseTable table = new() { Name = "scaff_compression_tz", Schema = "public" };
        table[HypertableAnnotations.ChunkTimeInterval] = "7 days";
        CompressionPolicyAnnotationApplier applier = new();
        applier.ApplyAnnotations(table, info);

        Assert.Equal("Europe/Berlin", table[CompressionPolicyAnnotations.Timezone]);
    }

    #endregion

    #region Should_RoundTrip_CompressionPolicy_With_All_Fields

    private class RoundTripMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RoundTripContext(string connectionString) : DbContext
    {
        public DbSet<RoundTripMetric> Metrics => Set<RoundTripMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RoundTripMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaff_compression_roundtrip");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(
                          after: "14 days",
                          scheduleInterval: "6 hours",
                          initialStart: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public async Task Should_RoundTrip_CompressionPolicy_With_All_Fields()
    {
        // Arrange
        await using RoundTripContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        CompressionPolicyScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);

        // Act
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "scaff_compression_roundtrip")));
        CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo info =
            (CompressionPolicyScaffoldingExtractor.CompressionPolicyInfo)result[("public", "scaff_compression_roundtrip")];

        DatabaseTable table = new() { Name = "scaff_compression_roundtrip", Schema = "public" };
        table[HypertableAnnotations.ChunkTimeInterval] = "7 days";
        CompressionPolicyAnnotationApplier applier = new();
        applier.ApplyAnnotations(table, info);

        Assert.Equal(true, table[CompressionPolicyAnnotations.HasCompressionPolicy]);
        Assert.Equal("14 days", table[CompressionPolicyAnnotations.After]);
        Assert.Null(table[CompressionPolicyAnnotations.CreatedBefore]);
        Assert.Equal("6 hours", table[CompressionPolicyAnnotations.ScheduleInterval]);

        DateTime expectedInitialStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime actualInitialStart = Assert.IsType<DateTime>(table[CompressionPolicyAnnotations.InitialStart]);
        Assert.Equal(expectedInitialStart, actualInitialStart.ToUniversalTime());
    }

    #endregion
}
