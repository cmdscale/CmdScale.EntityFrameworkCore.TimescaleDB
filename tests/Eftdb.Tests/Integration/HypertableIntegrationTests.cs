using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Community-edition hypertable integration tests. Inherits the license-neutral facts from
/// <see cref="HypertableIntegrationTestsBase"/> and adds the compression and chunk-skipping
/// facts that only apply on the Community edition.
/// </summary>
public class HypertableIntegrationTests : HypertableIntegrationTestsBase
{
    protected override string Image => TimescaleImages.Community;

    private class CompressionSettingInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public int? SegmentByIndex { get; set; }
        public int? OrderByIndex { get; set; }
        public bool IsAscending { get; set; }
        public bool IsNullsFirst { get; set; }
    }

    #region Helper Methods

    private static async Task<List<CompressionSettingInfo>> GetCompressionSettingsAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT
                    attname,
                    segmentby_column_index,
                    orderby_column_index,
                    orderby_asc,
                    orderby_nullsfirst
                FROM timescaledb_information.compression_settings
                WHERE hypertable_name = @tableName
                ORDER BY segmentby_column_index, orderby_column_index;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        List<CompressionSettingInfo> settings = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            settings.Add(new CompressionSettingInfo
            {
                ColumnName = reader.GetString(0),
                SegmentByIndex = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                OrderByIndex = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                IsAscending = !reader.IsDBNull(3) && reader.GetBoolean(3),
                IsNullsFirst = !reader.IsDBNull(4) && reader.GetBoolean(4)
            });
        }

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return settings;
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

        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "compressed_metrics");

        Assert.True(compressionEnabled);
    }

    #endregion

    #region Should_Create_Hypertable_With_CompressionSegmentBy

    private class SegmentByMetric
    {
        public DateTime Timestamp { get; set; }
        public int TenantId { get; set; }
        public double Value { get; set; }
    }

    private class SegmentByContext(string connectionString) : DbContext
    {
        public DbSet<SegmentByMetric> Metrics => Set<SegmentByMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SegmentByMetric>(entity =>
            {
                entity.ToTable("segment_by_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithCompressionSegmentBy(x => x.TenantId);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_CompressionSegmentBy()
    {
        await using SegmentByContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool isCompressed = await HypertableProbe.IsCompressionEnabledAsync(context, "segment_by_metrics");
        Assert.True(isCompressed);

        List<CompressionSettingInfo> settings = await GetCompressionSettingsAsync(context, "segment_by_metrics");

        CompressionSettingInfo? tenantSetting = settings.FirstOrDefault(s => s.ColumnName == "TenantId");
        Assert.NotNull(tenantSetting);

        Assert.Equal(1, tenantSetting.SegmentByIndex);
        Assert.Null(tenantSetting.OrderByIndex);
    }

    #endregion

    #region Should_Create_Hypertable_With_CompressionOrderBy

    private class OrderByMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderByContext(string connectionString) : DbContext
    {
        public DbSet<OrderByMetric> Metrics => Set<OrderByMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderByMetric>(entity =>
            {
                entity.ToTable("order_by_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithCompressionOrderBy(s => [
                           s.ByDescending(x => x.Timestamp),
                           s.By(x => x.Value, nullsFirst: true)
                       ]);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_CompressionOrderBy()
    {
        await using OrderByContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool isCompressed = await HypertableProbe.IsCompressionEnabledAsync(context, "order_by_metrics");
        Assert.True(isCompressed);

        List<CompressionSettingInfo> settings = await GetCompressionSettingsAsync(context, "order_by_metrics");

        CompressionSettingInfo tsSetting = settings.First(s => s.ColumnName == "Timestamp");
        Assert.NotNull(tsSetting.OrderByIndex);
        Assert.False(tsSetting.IsAscending);

        CompressionSettingInfo valSetting = settings.First(s => s.ColumnName == "Value");
        Assert.NotNull(valSetting.OrderByIndex);
        Assert.True(valSetting.IsAscending);
        Assert.True(valSetting.IsNullsFirst);
    }

    #endregion

    #region Should_Create_Hypertable_With_FullCompressionSettings

    private class FullCompressionMetric
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class FullCompressionContext(string connectionString) : DbContext
    {
        public DbSet<FullCompressionMetric> Metrics => Set<FullCompressionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullCompressionMetric>(entity =>
            {
                entity.ToTable("full_comp_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithCompressionSegmentBy(x => x.DeviceId)
                       .WithCompressionOrderBy(s => [s.ByDescending(x => x.Timestamp)]);
            });
        }
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_FullCompressionSettings()
    {
        await using FullCompressionContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        List<CompressionSettingInfo> settings = await GetCompressionSettingsAsync(context, "full_comp_metrics");

        CompressionSettingInfo deviceSetting = settings.First(s => s.ColumnName == "DeviceId");
        Assert.Equal(1, deviceSetting.SegmentByIndex);

        CompressionSettingInfo tsSetting = settings.First(s => s.ColumnName == "Timestamp");
        Assert.Equal(1, tsSetting.OrderByIndex);
        Assert.False(tsSetting.IsAscending);
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

        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "skippable_data");
        Assert.True(compressionEnabled);
        Assert.Contains("DeviceId", skipColumns);
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

        bool isHypertable = await HypertableProbe.IsHypertableAsync(context, "comprehensive_table");
        string chunkInterval = await HypertableProbe.GetChunkIntervalAsync(context, "comprehensive_table");
        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "comprehensive_table");
        List<string> skipColumns = await GetChunkSkipColumnsAsync(context, "comprehensive_table");
        List<DimensionInfo> dimensions = await GetDimensionsAsync(context, "comprehensive_table");

        Assert.True(isHypertable);
        Assert.Contains("12:00:00", chunkInterval);
        Assert.True(compressionEnabled);
        Assert.Contains("SensorId", skipColumns);
        Assert.Equal(2, dimensions.Count);
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

        bool isHypertable = await HypertableProbe.IsHypertableAsync(context, "ordered_ops");
        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "ordered_ops");

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

        bool compressionEnabled = await HypertableProbe.IsCompressionEnabledAsync(context, "compression_chunk_skip");
        List<string> skipColumns = await GetChunkSkipColumnsAsync(context, "compression_chunk_skip");

        Assert.True(compressionEnabled);
        Assert.NotEmpty(skipColumns);
    }

    #endregion
}
