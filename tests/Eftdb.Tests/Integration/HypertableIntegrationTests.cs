using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class HypertableIntegrationTests : MigrationTestBase, IAsyncLifetime
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

    #region Helper Methods

    private static async Task<bool> IsHypertableAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT COUNT(*) > 0
                FROM timescaledb_information.hypertables
                WHERE hypertable_name = @tableName;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool boolResult && boolResult;
    }

    private static async Task<string> GetChunkIntervalAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT time_interval::text
                FROM timescaledb_information.dimensions
                WHERE hypertable_name = @tableName
                  AND dimension_type = 'Time'
                LIMIT 1;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result?.ToString() ?? string.Empty;
    }

    private static async Task<bool> IsCompressionEnabledAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT compression_enabled
                FROM timescaledb_information.hypertables
                WHERE hypertable_name = @tableName;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool boolResult && boolResult;
    }

    private static async Task<List<string>> GetChunkSkipColumnsAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT column_name
                FROM _timescaledb_catalog.chunk_column_stats ccs
                JOIN _timescaledb_catalog.hypertable h ON ccs.hypertable_id = h.id
                WHERE h.table_name = @tableName
                GROUP BY column_name;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        List<string> columns = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return columns;
    }

    private static async Task<List<DimensionInfo>> GetDimensionsAsync(DbContext context, string tableName)
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

    private static async Task<int> GetChunkCountAsync(DbContext context, string tableName)
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

    private class DimensionInfo
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
            $"INSERT INTO \"Metrics\" (\"Timestamp\", \"Value\") VALUES ({timestamp}, {value})");

        bool isHypertable = await IsHypertableAsync(context, "Metrics");
        Assert.True(isHypertable);

        List<MinimalHypertableMetric> metrics = await context.Metrics.ToListAsync();
        Assert.Single(metrics);
        Assert.Equal(100.5, metrics[0].Value);
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

        string chunkInterval = await GetChunkIntervalAsync(context, "sensor_data");

        Assert.Contains("1 day", chunkInterval);
    }

    #endregion

    #region Should_Create_Hypertable_With_Compression_Enabled

    private class CompressionEnabledMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionEnabledContext(string connectionString) : DbContext
    {
        public DbSet<CompressionEnabledMetric> Metrics => Set<CompressionEnabledMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionEnabledMetric>(entity =>
            {
                entity.ToTable("compressed_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .EnableCompression(true);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_Compression_Enabled()
    {
        await using CompressionEnabledContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool compressionEnabled = await IsCompressionEnabledAsync(context, "compressed_metrics");

        Assert.True(compressionEnabled);
    }

    #endregion

    #region Should_Create_Hypertable_With_ChunkSkipping

    private class ChunkSkippingData
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Temperature { get; set; }
    }

    private class ChunkSkippingContext(string connectionString) : DbContext
    {
        public DbSet<ChunkSkippingData> SkippableData => Set<ChunkSkippingData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkSkippingData>(entity =>
            {
                entity.ToTable("skippable_data");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkSkipping(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_ChunkSkipping()
    {
        await using ChunkSkippingContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<string> skipColumns = await GetChunkSkipColumnsAsync(context, "skippable_data");

        bool compressionEnabled = await IsCompressionEnabledAsync(context, "skippable_data");
        Assert.True(compressionEnabled);
        Assert.Contains("DeviceId", skipColumns);
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

    #region Should_Create_Hypertable_With_AllOptions

    private class AllOptionsData
    {
        public DateTime Timestamp { get; set; }
        public int SensorId { get; set; }
        public string Location { get; set; } = string.Empty;
        public double Temperature { get; set; }
    }

    private class AllOptionsContext(string connectionString) : DbContext
    {
        public DbSet<AllOptionsData> ComprehensiveData => Set<AllOptionsData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllOptionsData>(entity =>
            {
                entity.ToTable("comprehensive_table");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("12 hours")
                       .EnableCompression(true)
                       .WithChunkSkipping(x => x.SensorId)
                       .HasDimension(Dimension.CreateHash("Location", 8));
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_AllOptions()
    {
        await using AllOptionsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool isHypertable = await IsHypertableAsync(context, "comprehensive_table");
        string chunkInterval = await GetChunkIntervalAsync(context, "comprehensive_table");
        bool compressionEnabled = await IsCompressionEnabledAsync(context, "comprehensive_table");
        List<string> skipColumns = await GetChunkSkipColumnsAsync(context, "comprehensive_table");
        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "comprehensive_table");

        Assert.True(isHypertable);
        Assert.Contains("12:00:00", chunkInterval);
        Assert.True(compressionEnabled);
        Assert.Contains("SensorId", skipColumns);
        Assert.Equal(2, dimensions.Count);
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
                ({new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc)}, {"device_2"}, {19.5}, {50.0})");

        List<IoTDataRecord> data = await context.IoTData.ToListAsync();
        Assert.Equal(3, data.Count);

        List<IoTDataRecord> device1Data = await context.IoTData.Where(d => d.DeviceId == "device_1").ToListAsync();
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
            valueRows.Add($"('{timestamp:yyyy-MM-dd HH:mm:ss}+00', {i % 10}, {15.0 + i * 0.1})");
        }

        string sql = $@"INSERT INTO ""PerformanceTest"" (""Timestamp"", ""SensorId"", ""Value"")
            VALUES {string.Join(", ", valueRows)}";
        await context.Database.ExecuteSqlRawAsync(sql);

        int count = await context.PerformanceTest.CountAsync();
        Assert.Equal(100, count);

        List<PerformanceTestData> sensor0Data = await context.PerformanceTest
            .Where(d => d.SensorId == 0)
            .ToListAsync();
        Assert.Equal(10, sensor0Data.Count);

        int chunkCount = await GetChunkCountAsync(context, "PerformanceTest");
        Assert.True(chunkCount >= 1);
    }

    #endregion

    #region Should_Create_Hypertable_Before_Compression

    private class OperationOrderingMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OperationOrderingContext(string connectionString) : DbContext
    {
        public DbSet<OperationOrderingMetric> Metrics => Set<OperationOrderingMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OperationOrderingMetric>(entity =>
            {
                entity.ToTable("ordered_ops");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .EnableCompression(true);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_Before_Compression()
    {
        await using OperationOrderingContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool isHypertable = await IsHypertableAsync(context, "ordered_ops");
        bool compressionEnabled = await IsCompressionEnabledAsync(context, "ordered_ops");

        Assert.True(isHypertable);
        Assert.True(compressionEnabled);
    }

    #endregion

    #region Should_Enable_Compression_Before_ChunkSkipping

    private class CompressionChunkSkippingData
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Temperature { get; set; }
    }

    private class CompressionChunkSkippingContext(string connectionString) : DbContext
    {
        public DbSet<CompressionChunkSkippingData> SkippableData => Set<CompressionChunkSkippingData>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionChunkSkippingData>(entity =>
            {
                entity.ToTable("compression_chunk_skip");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkSkipping(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public async Task Should_Enable_Compression_Before_ChunkSkipping()
    {
        await using CompressionChunkSkippingContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool compressionEnabled = await IsCompressionEnabledAsync(context, "compression_chunk_skip");
        List<string> skipColumns = await GetChunkSkipColumnsAsync(context, "compression_chunk_skip");

        Assert.True(compressionEnabled);
        Assert.NotEmpty(skipColumns);
    }

    #endregion
}
