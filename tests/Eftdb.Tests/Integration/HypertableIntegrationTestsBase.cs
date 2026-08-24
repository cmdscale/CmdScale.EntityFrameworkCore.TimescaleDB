using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// License-neutral hypertable integration facts (plain creation, chunk interval, dimensions,
/// data operations) that hold on both the Community and Apache editions. Concrete subclasses
/// pin the container image through <see cref="Image"/>.
/// </summary>
public abstract class HypertableIntegrationTestsBase : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    protected string? _connectionString;

    protected abstract string Image { get; }

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(Image)
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

    protected static async Task<List<DimensionInfo>> GetDimensionsAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT column_name, num_partitions
                FROM timescaledb_information.dimensions
                WHERE hypertable_name = @tableName;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        List<DimensionInfo> dimensions = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dimensions.Add(new DimensionInfo
            {
                ColumnName = reader.GetString(0),
                NumberPartitions = reader.IsDBNull(1) ? null : reader.GetInt32(1)
            });
        }

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return dimensions;
    }

    protected static async Task<int> GetChunkCountAsync(DbContext context, string tableName)
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
                FROM timescaledb_information.chunks
                WHERE hypertable_schema = 'public' AND hypertable_name = @tableName;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is long longResult ? (int)longResult :
               result is int intResult ? intResult : 0;
    }

    protected class DimensionInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public int? NumberPartitions { get; set; }
    }

    #endregion

    #region Should_Create_Minimal_Hypertable

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
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Minimal_Hypertable()
    {
        await using MinimalHypertableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        DateTime timestamp = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        double value = 100.5;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Metrics\" (\"Timestamp\", \"Value\") VALUES ({timestamp}, {value})", TestContext.Current.CancellationToken);

        bool isHypertable = await HypertableProbe.IsHypertableAsync(context, "Metrics");
        Assert.True(isHypertable);

        List<MinimalHypertableMetric> metrics = await context.Metrics.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100.5, Assert.Single(metrics).Value);
    }

    #endregion

    #region Should_Create_Hypertable_With_CustomChunkInterval

    private class CustomChunkIntervalData
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Temperature { get; set; }
    }

    private class CustomChunkIntervalContext(string connectionString) : DbContext
    {
        public DbSet<CustomChunkIntervalData> SensorData => Set<CustomChunkIntervalData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomChunkIntervalData>(entity =>
            {
                entity.ToTable("sensor_data");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_CustomChunkInterval()
    {
        await using CustomChunkIntervalContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        string chunkInterval = await HypertableProbe.GetChunkIntervalAsync(context, "sensor_data");

        Assert.Contains("1 day", chunkInterval);
    }

    #endregion

    #region Should_Create_Hypertable_With_HashDimension

    private class HashDimensionData
    {
        public DateTime Timestamp { get; set; }
        public int LocationId { get; set; }
        public double Value { get; set; }
    }

    private class HashDimensionContext(string connectionString) : DbContext
    {
        public DbSet<HashDimensionData> PartitionedData => Set<HashDimensionData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HashDimensionData>(entity =>
            {
                entity.ToTable("partitioned_data");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .HasDimension(Dimension.CreateHash("LocationId", 4));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_HashDimension()
    {
        await using HashDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "partitioned_data");

        Assert.Equal(2, dimensions.Count);

        DimensionInfo? hashDimension = dimensions.FirstOrDefault(d => d.ColumnName == "LocationId");
        Assert.NotNull(hashDimension);
        Assert.Equal(4, hashDimension.NumberPartitions);
    }

    #endregion

    #region Should_Create_Hypertable_With_RangeDimension

    private class RangeDimensionData
    {
        public DateTime Timestamp { get; set; }
        public DateTime ProcessedTime { get; set; }
        public double Value { get; set; }
    }

    private class RangeDimensionContext(string connectionString) : DbContext
    {
        public DbSet<RangeDimensionData> MultiTimeData => Set<RangeDimensionData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RangeDimensionData>(entity =>
            {
                entity.ToTable("multi_time_data");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .HasDimension(Dimension.CreateRange("ProcessedTime", "7 days"));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_RangeDimension()
    {
        await using RangeDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "multi_time_data");

        Assert.Equal(2, dimensions.Count);

        DimensionInfo? rangeDimension = dimensions.FirstOrDefault(d => d.ColumnName == "ProcessedTime");
        Assert.NotNull(rangeDimension);
    }

    #endregion

    #region Should_Create_Hypertable_With_RangeDimension_IntegerInterval

    private class IntegerRangeDimensionData
    {
        public DateTime Timestamp { get; set; }
        public int SequenceNumber { get; set; }
        public double Value { get; set; }
    }

    private class IntegerRangeDimensionContext(string connectionString) : DbContext
    {
        public DbSet<IntegerRangeDimensionData> SequencedData => Set<IntegerRangeDimensionData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntegerRangeDimensionData>(entity =>
            {
                entity.ToTable("sequenced_data");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .HasDimension(Dimension.CreateRange("SequenceNumber", "10000"));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_RangeDimension_IntegerInterval()
    {
        await using IntegerRangeDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "sequenced_data");

        Assert.Equal(2, dimensions.Count);

        DimensionInfo? rangeDimension = dimensions.FirstOrDefault(d => d.ColumnName == "SequenceNumber");
        Assert.NotNull(rangeDimension);
        Assert.Null(rangeDimension.NumberPartitions);
    }

    #endregion

    #region Should_Create_Hypertable_With_RangeDimension_TimeInterval

    private class TimeRangeDimensionData
    {
        public DateTime EventTime { get; set; }
        public DateTime ProcessingTime { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class TimeRangeDimensionContext(string connectionString) : DbContext
    {
        public DbSet<TimeRangeDimensionData> DualTimeData => Set<TimeRangeDimensionData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeRangeDimensionData>(entity =>
            {
                entity.ToTable("dual_time_events");
                entity.HasNoKey();
                entity.IsHypertable(x => x.EventTime)
                       .HasDimension(Dimension.CreateRange("ProcessingTime", "2 hours"));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_RangeDimension_TimeInterval()
    {
        await using TimeRangeDimensionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "dual_time_events");

        Assert.Equal(2, dimensions.Count);

        DimensionInfo? rangeDimension = dimensions.FirstOrDefault(d => d.ColumnName == "ProcessingTime");
        Assert.NotNull(rangeDimension);
        Assert.Null(rangeDimension.NumberPartitions);
    }

    #endregion

    #region Should_Create_Hypertable_With_MultipleDimensions

    private class MultipleDimensionsData
    {
        public DateTime EventTime { get; set; }
        public int DeviceId { get; set; }
        public string Region { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }

    private class MultipleDimensionsContext(string connectionString) : DbContext
    {
        public DbSet<MultipleDimensionsData> EventData => Set<MultipleDimensionsData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleDimensionsData>(entity =>
            {
                entity.ToTable("distributed_events");
                entity.HasNoKey();
                entity.IsHypertable(x => x.EventTime)
                       .HasDimension(Dimension.CreateHash("DeviceId", 4))
                       .HasDimension(Dimension.CreateHash("Region", 2));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_MultipleDimensions()
    {
        await using MultipleDimensionsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "distributed_events");

        Assert.Equal(3, dimensions.Count);
        Assert.Contains(dimensions, d => d.ColumnName == "EventTime");
        Assert.Contains(dimensions, d => d.ColumnName == "DeviceId");
        Assert.Contains(dimensions, d => d.ColumnName == "Region");
    }

    #endregion

    #region Should_Insert_And_Query_Data_From_Hypertable

    private class IoTDataRecord
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    private class DataOperationsContext(string connectionString) : DbContext
    {
        public DbSet<IoTDataRecord> IoTData => Set<IoTDataRecord>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IoTDataRecord>(entity =>
            {
                entity.ToTable("IoTData");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public async Task Should_Insert_And_Query_Data_From_Hypertable()
    {
        await using DataOperationsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""IoTData"" (""Timestamp"", ""DeviceId"", ""Temperature"", ""Humidity"")
            VALUES
                ({new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc)}, {"device_1"}, {20.5}, {45.0}),
                ({new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc)}, {"device_1"}, {21.0}, {46.0}),
                ({new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc)}, {"device_2"}, {19.5}, {50.0})", TestContext.Current.CancellationToken);

        List<IoTDataRecord> data = await context.IoTData.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, data.Count);

        List<IoTDataRecord> device1Data = await context.IoTData.Where(d => d.DeviceId == "device_1").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, device1Data.Count);

        int chunkCount = await GetChunkCountAsync(context, "IoTData");
        Assert.True(chunkCount >= 1);
    }

    #endregion

    #region Should_Handle_LargeDataset

    private class PerformanceTestData
    {
        public DateTime Timestamp { get; set; }
        public int SensorId { get; set; }
        public double Value { get; set; }
    }

    private class PerformanceTestContext(string connectionString) : DbContext
    {
        public DbSet<PerformanceTestData> PerformanceTest => Set<PerformanceTestData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PerformanceTestData>(entity =>
            {
                entity.ToTable("PerformanceTest");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 hour");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_LargeDataset()
    {
        await using PerformanceTestContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        DateTime baseTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        List<string> valueRows = [];

        for (int i = 0; i < 100; i++)
        {
            DateTime timestamp = baseTime.AddMinutes(i);
            valueRows.Add(FormattableString.Invariant($"('{timestamp:yyyy-MM-dd HH:mm:ss}+00', {i % 10}, {15.0 + i * 0.1})"));
        }

        string sql = $@"INSERT INTO ""PerformanceTest"" (""Timestamp"", ""SensorId"", ""Value"")
            VALUES {string.Join(", ", valueRows)}";
        await context.Database.ExecuteSqlRawAsync(sql, [], TestContext.Current.CancellationToken);

        int count = await context.PerformanceTest.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100, count);

        List<PerformanceTestData> sensor0Data = await context.PerformanceTest
            .Where(d => d.SensorId == 0)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, sensor0Data.Count);

        int chunkCount = await GetChunkCountAsync(context, "PerformanceTest");
        Assert.True(chunkCount >= 1);
    }

    #endregion
}
