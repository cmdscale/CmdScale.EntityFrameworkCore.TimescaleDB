using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class HypertableScaffoldingExtractorTests : MigrationTestBase, IAsyncLifetime
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

    #region Should_Extract_Minimal_Hypertable

    private class MinimalMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class MinimalHypertableContext(string connectionString) : DbContext
    {
        public DbSet<MinimalMetric> Metrics => Set<MinimalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Minimal_Hypertable()
    {
        await using MinimalHypertableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        Assert.True(result.ContainsKey(("public", "Metrics")));

        object infoObj = result[("public", "Metrics")];
        Assert.IsType<HypertableScaffoldingExtractor.HypertableInfo>(infoObj);

        HypertableScaffoldingExtractor.HypertableInfo info = (HypertableScaffoldingExtractor.HypertableInfo)infoObj;
        Assert.Equal("Timestamp", info.TimeColumnName);
        Assert.NotNull(info.ChunkTimeInterval);
        Assert.False(info.CompressionEnabled);
        Assert.Empty(info.ChunkSkipColumns);
        Assert.Empty(info.AdditionalDimensions);
    }

    #endregion

    #region Should_Return_Empty_When_No_Hypertables

    private class PlainEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class EmptyDatabaseContext(string connectionString) : DbContext
    {
        public DbSet<PlainEntity> Entities => Set<PlainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainEntity>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.ToTable("Plain");
            });
        }
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Hypertables()
    {
        await using EmptyDatabaseContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Empty(result);
    }

    #endregion

    #region Should_Extract_Hypertable_With_Compression_Enabled

    private class CompressionMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class CompressionEnabledContext(string connectionString) : DbContext
    {
        public DbSet<CompressionMetric> Metrics => Set<CompressionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Hypertable_With_Compression_Enabled()
    {
        await using CompressionEnabledContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        HypertableScaffoldingExtractor.HypertableInfo info = (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "Metrics")];
        Assert.True(info.CompressionEnabled);
    }

    #endregion

    #region Should_Extract_ChunkSkipColumns

    private class ChunkSkippingMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class ChunkSkippingContext(string connectionString) : DbContext
    {
        public DbSet<ChunkSkippingMetric> Metrics => Set<ChunkSkippingMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkSkippingMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression()
                      .WithChunkSkipping(x => x.SensorId);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_ChunkSkipColumns()
    {
        await using ChunkSkippingContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Metrics\" (\"Timestamp\", \"DeviceId\", \"Location\", \"Value\", \"SensorId\") VALUES (NOW(), 'device1', 'location1', 100.0, 1)");
        await context.Database.ExecuteSqlRawAsync(
            "SELECT compress_chunk(i) FROM show_chunks('\"Metrics\"') AS i;");

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        HypertableScaffoldingExtractor.HypertableInfo info = (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "Metrics")];
        Assert.NotEmpty(info.ChunkSkipColumns);
        Assert.Contains("SensorId", info.ChunkSkipColumns);
    }

    #endregion

    #region Should_Extract_Hash_Dimension

    private class HashDimensionMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
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
    public async Task Should_Extract_Hash_Dimension()
    {
        await using HashDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        HypertableScaffoldingExtractor.HypertableInfo info = (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "Metrics")];
        Dimension dimension = Assert.Single(info.AdditionalDimensions);
        Assert.Equal("DeviceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
        Assert.Equal(4, dimension.NumberOfPartitions);
    }

    #endregion

    #region Should_Extract_Multiple_Dimensions

    private class MultipleDimensionsMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class MultipleDimensionsContext(string connectionString) : DbContext
    {
        public DbSet<MultipleDimensionsMetric> Metrics => Set<MultipleDimensionsMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleDimensionsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4))
                      .HasDimension(Dimension.CreateRange("SensorId", "1000"));
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_Dimensions()
    {
        await using MultipleDimensionsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Single(result);
        HypertableScaffoldingExtractor.HypertableInfo info = (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "Metrics")];
        Assert.Equal(2, info.AdditionalDimensions.Count);

        Dimension hashDim = info.AdditionalDimensions[0];
        Assert.Equal("DeviceId", hashDim.ColumnName);
        Assert.Equal(EDimensionType.Hash, hashDim.Type);

        Dimension rangeDim = info.AdditionalDimensions[1];
        Assert.Equal("SensorId", rangeDim.ColumnName);
        Assert.Equal(EDimensionType.Range, rangeDim.Type);
    }

    #endregion

    #region Should_Extract_Multiple_Hypertables

    private class MultipleHypertablesMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
        public int SensorId { get; set; }
    }

    private class MultipleHypertablesEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultipleHypertablesContext(string connectionString) : DbContext
    {
        public DbSet<MultipleHypertablesMetric> Metrics => Set<MultipleHypertablesMetric>();
        public DbSet<MultipleHypertablesEvent> Events => Set<MultipleHypertablesEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleHypertablesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleHypertablesEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Extract_Multiple_Hypertables()
    {
        await using MultipleHypertablesContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("public", "Metrics")));
        Assert.True(result.ContainsKey(("public", "Events")));
    }

    #endregion
}
