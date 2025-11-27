using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extensions;

/// <summary>
/// Integration tests for TimescaleDbCopyExtensions bulk copy functionality.
/// These tests verify bulk copy operations with various configurations, data types, and scenarios.
/// </summary>
public class BulkCopyExtensionsTests : IAsyncLifetime
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

    #region Should_BulkCopy_With_Default_Config

    private class DefaultConfigEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    private class DefaultConfigContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultConfigEntity>(entity =>
            {
                entity.ToTable("DefaultConfigEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Default_Config()
    {
        // Arrange
        using DefaultConfigContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<DefaultConfigEntity> data =
        [
            new() { Id = 1, Name = "Test1", Timestamp = DateTime.UtcNow },
            new() { Id = 2, Name = "Test2", Timestamp = DateTime.UtcNow.AddHours(1) },
            new() { Id = 3, Name = "Test3", Timestamp = DateTime.UtcNow.AddHours(2) }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        int count = await context.Set<DefaultConfigEntity>().CountAsync();
        Assert.Equal(3, count);

        List<DefaultConfigEntity> inserted = await context.Set<DefaultConfigEntity>().OrderBy(e => e.Id).ToListAsync();
        Assert.Equal("Test1", inserted[0].Name);
        Assert.Equal("Test2", inserted[1].Name);
        Assert.Equal("Test3", inserted[2].Name);
    }

    #endregion

    #region Should_BulkCopy_With_Custom_Table_Name

    private class CustomTableEntity
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private class CustomTableContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomTableEntity>(entity =>
            {
                entity.ToTable("MyCustomTable");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Custom_Table_Name()
    {
        // Arrange
        using CustomTableContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<CustomTableEntity> data =
        [
            new() { Id = 1, Value = "Alpha" },
            new() { Id = 2, Value = "Beta" }
        ];

        TimescaleCopyConfig<CustomTableEntity> config = new TimescaleCopyConfig<CustomTableEntity>()
            .ToTable("MyCustomTable");

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<CustomTableEntity>().CountAsync();
        Assert.Equal(2, count);
    }

    #endregion

    #region Should_BulkCopy_With_Custom_Workers_And_BatchSize

    private class WorkerConfigEntity
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    private class WorkerConfigContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkerConfigEntity>(entity =>
            {
                entity.ToTable("WorkerConfigEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Data).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Custom_Workers_And_BatchSize()
    {
        // Arrange
        using WorkerConfigContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<WorkerConfigEntity> data = [];
        for (int i = 1; i <= 100; i++)
        {
            data.Add(new WorkerConfigEntity { Id = i, Data = $"Data_{i}" });
        }

        TimescaleCopyConfig<WorkerConfigEntity> config = new TimescaleCopyConfig<WorkerConfigEntity>()
            .ToTable("WorkerConfigEntity")
            .WithWorkers(8)
            .WithBatchSize(25);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<WorkerConfigEntity>().CountAsync();
        Assert.Equal(100, count);
    }

    #endregion

    #region Should_BulkCopy_With_Manual_Column_Mapping

    private class MappingEntity
    {
        public int Identifier { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    private class MappingContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MappingEntity>(entity =>
            {
                entity.ToTable("MappingEntity");
                entity.HasKey(e => e.Identifier);
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Manual_Column_Mapping()
    {
        // Arrange
        using MappingContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<MappingEntity> data =
        [
            new() { Identifier = 1, Description = "Item1", Amount = 100.50m },
            new() { Identifier = 2, Description = "Item2", Amount = 200.75m }
        ];

        TimescaleCopyConfig<MappingEntity> config = new TimescaleCopyConfig<MappingEntity>()
            .ToTable("MappingEntity")
            .MapColumn("Identifier", e => e.Identifier, NpgsqlDbType.Integer)
            .MapColumn("Description", e => e.Description, NpgsqlDbType.Text)
            .MapColumn("Amount", e => e.Amount, NpgsqlDbType.Numeric);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<MappingEntity>().CountAsync();
        Assert.Equal(2, count);

        List<MappingEntity> inserted = await context.Set<MappingEntity>().OrderBy(e => e.Identifier).ToListAsync();
        Assert.Equal(100.50m, inserted[0].Amount);
        Assert.Equal(200.75m, inserted[1].Amount);
    }

    #endregion

    #region Should_BulkCopy_Various_Data_Types

    private class DataTypeEntity
    {
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public short ShortValue { get; set; }
        public double DoubleValue { get; set; }
        public float FloatValue { get; set; }
        public decimal DecimalValue { get; set; }
        public bool BoolValue { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public DateTime DateTimeValue { get; set; }
        public Guid GuidValue { get; set; }
        public byte[] ByteArrayValue { get; set; } = [];
    }

    private class DataTypeContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DataTypeEntity>(entity =>
            {
                entity.ToTable("DataTypeEntity");
                entity.HasKey(e => e.IntValue);
                entity.Property(e => e.StringValue).IsRequired();
                entity.Property(e => e.ByteArrayValue).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_Various_Data_Types()
    {
        // Arrange
        using DataTypeContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        Guid testGuid = Guid.NewGuid();
        DateTime testDateTime = DateTime.UtcNow;
        byte[] testBytes = [1, 2, 3, 4, 5];

        List<DataTypeEntity> data =
        [
            new()
            {
                IntValue = 42,
                LongValue = 9223372036854775807L,
                ShortValue = 32767,
                DoubleValue = 3.14159,
                FloatValue = 2.71828f,
                DecimalValue = 123.456m,
                BoolValue = true,
                StringValue = "Test",
                DateTimeValue = testDateTime,
                GuidValue = testGuid,
                ByteArrayValue = testBytes
            }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        DataTypeEntity? inserted = await context.Set<DataTypeEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(42, inserted.IntValue);
        Assert.Equal(9223372036854775807L, inserted.LongValue);
        Assert.Equal(32767, inserted.ShortValue);
        Assert.Equal(3.14159, inserted.DoubleValue, 5);
        Assert.Equal(2.71828f, inserted.FloatValue, 5);
        Assert.Equal(123.456m, inserted.DecimalValue);
        Assert.True(inserted.BoolValue);
        Assert.Equal("Test", inserted.StringValue);
        Assert.Equal(testGuid, inserted.GuidValue);
        Assert.Equal(testBytes, inserted.ByteArrayValue);
    }

    #endregion

    #region Should_BulkCopy_To_Hypertable

    private class HypertableEntity
    {
        public DateTime Timestamp { get; set; }
        public int SensorId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    private class HypertableContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HypertableEntity>(entity =>
            {
                entity.ToTable("HypertableEntity");
                entity.HasKey(e => new { e.Timestamp, e.SensorId });
                entity.IsHypertable(e => e.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_To_Hypertable()
    {
        // Arrange
        using HypertableContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        DateTime baseTime = DateTime.UtcNow;
        List<HypertableEntity> data =
        [
            new() { Timestamp = baseTime, SensorId = 1, Temperature = 22.5, Humidity = 45.0 },
            new() { Timestamp = baseTime.AddMinutes(1), SensorId = 1, Temperature = 22.6, Humidity = 45.2 },
            new() { Timestamp = baseTime.AddMinutes(2), SensorId = 2, Temperature = 23.1, Humidity = 46.5 }
        ];

        TimescaleCopyConfig<HypertableEntity> config = new TimescaleCopyConfig<HypertableEntity>()
            .ToTable("HypertableEntity")
            .WithWorkers(2)
            .WithBatchSize(1000);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<HypertableEntity>().CountAsync();
        Assert.Equal(3, count);

        // Verify data integrity
        List<HypertableEntity> inserted = await context.Set<HypertableEntity>()
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.SensorId)
            .ToListAsync();
        Assert.Equal(22.5, inserted[0].Temperature);
        Assert.Equal(45.0, inserted[0].Humidity);
    }

    #endregion

    #region Should_BulkCopy_Empty_Collection

    private class EmptyCollectionEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class EmptyCollectionContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EmptyCollectionEntity>(entity =>
            {
                entity.ToTable("EmptyCollectionEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_Empty_Collection()
    {
        // Arrange
        using EmptyCollectionContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<EmptyCollectionEntity> data = [];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        int count = await context.Set<EmptyCollectionEntity>().CountAsync();
        Assert.Equal(0, count);
    }

    #endregion

    #region Should_BulkCopy_Single_Item

    private class SingleItemEntity
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private class SingleItemContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleItemEntity>(entity =>
            {
                entity.ToTable("SingleItemEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_Single_Item()
    {
        // Arrange
        using SingleItemContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<SingleItemEntity> data =
        [
            new() { Id = 1, Value = "OnlyOne" }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        int count = await context.Set<SingleItemEntity>().CountAsync();
        Assert.Equal(1, count);

        SingleItemEntity? inserted = await context.Set<SingleItemEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal("OnlyOne", inserted.Value);
    }

    #endregion

    #region Should_BulkCopy_Large_Dataset

    private class LargeDatasetEntity
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    private class LargeDatasetContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LargeDatasetEntity>(entity =>
            {
                entity.ToTable("LargeDatasetEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Data).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_Large_Dataset()
    {
        // Arrange
        using LargeDatasetContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<LargeDatasetEntity> data = [];
        DateTime baseTime = DateTime.UtcNow;
        for (int i = 1; i <= 10000; i++)
        {
            data.Add(new LargeDatasetEntity
            {
                Id = i,
                Data = $"Record_{i}",
                CreatedAt = baseTime.AddSeconds(i)
            });
        }

        TimescaleCopyConfig<LargeDatasetEntity> config = new TimescaleCopyConfig<LargeDatasetEntity>()
            .ToTable("LargeDatasetEntity")
            .WithWorkers(4)
            .WithBatchSize(2500);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<LargeDatasetEntity>().CountAsync();
        Assert.Equal(10000, count);
    }

    #endregion

    #region Should_BulkCopy_With_Nullable_Types

    private class NullableTypeEntity
    {
        public int Id { get; set; }
        public int? NullableInt { get; set; }
        public DateTime? NullableDateTime { get; set; }
        public bool? NullableBool { get; set; }
        public double? NullableDouble { get; set; }
        public string? NullableString { get; set; }
    }

    private class NullableTypeContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullableTypeEntity>(entity =>
            {
                entity.ToTable("NullableTypeEntity");
                entity.HasKey(e => e.Id);
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Nullable_Types()
    {
        // Arrange
        using NullableTypeContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        List<NullableTypeEntity> data =
        [
            new() { Id = 1, NullableInt = 42, NullableDateTime = DateTime.UtcNow, NullableBool = true, NullableDouble = 3.14, NullableString = "Test" },
            new() { Id = 2, NullableInt = null, NullableDateTime = null, NullableBool = null, NullableDouble = null, NullableString = null }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        int count = await context.Set<NullableTypeEntity>().CountAsync();
        Assert.Equal(2, count);

        List<NullableTypeEntity> inserted = await context.Set<NullableTypeEntity>().OrderBy(e => e.Id).ToListAsync();

        // First record with values
        Assert.Equal(42, inserted[0].NullableInt);
        Assert.NotNull(inserted[0].NullableDateTime);
        Assert.True(inserted[0].NullableBool);
        Assert.Equal(3.14, inserted[0].NullableDouble);
        Assert.Equal("Test", inserted[0].NullableString);

        // Second record with nulls
        Assert.Null(inserted[1].NullableInt);
        Assert.Null(inserted[1].NullableDateTime);
        Assert.Null(inserted[1].NullableBool);
        Assert.Null(inserted[1].NullableDouble);
        Assert.Null(inserted[1].NullableString);
    }

    #endregion

    #region Should_BulkCopy_With_DateOnly_And_TimeOnly

    private class DateTimeOnlyEntity
    {
        public int Id { get; set; }
        public DateOnly DateValue { get; set; }
        public TimeOnly TimeValue { get; set; }
    }

    private class DateTimeOnlyContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DateTimeOnlyEntity>(entity =>
            {
                entity.ToTable("DateTimeOnlyEntity");
                entity.HasKey(e => e.Id);
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_DateOnly_And_TimeOnly()
    {
        // Arrange
        using DateTimeOnlyContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        DateOnly testDate = new(2024, 3, 15);
        TimeOnly testTime = new(14, 30, 45);

        List<DateTimeOnlyEntity> data =
        [
            new() { Id = 1, DateValue = testDate, TimeValue = testTime }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        DateTimeOnlyEntity? inserted = await context.Set<DateTimeOnlyEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(testDate, inserted.DateValue);
        Assert.Equal(testTime, inserted.TimeValue);
    }

    #endregion

    #region Should_BulkCopy_With_TimeSpan

    private class TimeSpanEntity
    {
        public int Id { get; set; }
        public TimeSpan Duration { get; set; }
    }

    private class TimeSpanContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeSpanEntity>(entity =>
            {
                entity.ToTable("TimeSpanEntity");
                entity.HasKey(e => e.Id);
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_TimeSpan()
    {
        // Arrange
        using TimeSpanContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        TimeSpan testDuration = new(2, 30, 45); // 2 hours, 30 minutes, 45 seconds

        List<TimeSpanEntity> data =
        [
            new() { Id = 1, Duration = testDuration }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        TimeSpanEntity? inserted = await context.Set<TimeSpanEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(testDuration, inserted.Duration);
    }

    #endregion

    #region Should_BulkCopy_With_Guid

    private class GuidEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class GuidContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GuidEntity>(entity =>
            {
                entity.ToTable("GuidEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Guid()
    {
        // Arrange
        using GuidContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        Guid testGuid = Guid.NewGuid();

        List<GuidEntity> data =
        [
            new() { Id = testGuid, Name = "GuidTest" }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        GuidEntity? inserted = await context.Set<GuidEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(testGuid, inserted.Id);
        Assert.Equal("GuidTest", inserted.Name);
    }

    #endregion

    #region Should_BulkCopy_With_Multiple_Workers_Small_Dataset

    private class MultiWorkerSmallEntity
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    private class MultiWorkerSmallContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiWorkerSmallEntity>(entity =>
            {
                entity.ToTable("MultiWorkerSmallEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Data).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Multiple_Workers_Small_Dataset()
    {
        // Arrange
        using MultiWorkerSmallContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        // Only 3 items but 10 workers - should handle gracefully
        List<MultiWorkerSmallEntity> data =
        [
            new() { Id = 1, Data = "Item1" },
            new() { Id = 2, Data = "Item2" },
            new() { Id = 3, Data = "Item3" }
        ];

        TimescaleCopyConfig<MultiWorkerSmallEntity> config = new TimescaleCopyConfig<MultiWorkerSmallEntity>()
            .ToTable("MultiWorkerSmallEntity")
            .WithWorkers(10);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        int count = await context.Set<MultiWorkerSmallEntity>().CountAsync();
        Assert.Equal(3, count);
    }

    #endregion

    #region Should_BulkCopy_With_Byte_Array

    private class ByteArrayEntity
    {
        public int Id { get; set; }
        public byte[] BinaryData { get; set; } = [];
    }

    private class ByteArrayContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ByteArrayEntity>(entity =>
            {
                entity.ToTable("ByteArrayEntity");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BinaryData).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_With_Byte_Array()
    {
        // Arrange
        using ByteArrayContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        byte[] testBytes = [0xFF, 0xAA, 0x55, 0x00, 0x11, 0x22, 0x33, 0x44];

        List<ByteArrayEntity> data =
        [
            new() { Id = 1, BinaryData = testBytes }
        ];

        // Act
        await data.BulkCopyAsync(_connectionString!);

        // Assert
        ByteArrayEntity? inserted = await context.Set<ByteArrayEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(testBytes, inserted.BinaryData);
    }

    #endregion

    #region Should_BulkCopy_Respecting_Column_Order

    private class ColumnOrderEntity
    {
        public string Column3 { get; set; } = string.Empty;
        public int Column1 { get; set; }
        public DateTime Column2 { get; set; }
    }

    private class ColumnOrderContext(string connectionString) : DbContext
    {
        private readonly string _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString).UseTimescaleDb();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ColumnOrderEntity>(entity =>
            {
                entity.ToTable("ColumnOrderEntity");
                entity.HasKey(e => e.Column1);
                entity.Property(e => e.Column3).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Should_BulkCopy_Respecting_Column_Order()
    {
        // Arrange
        using ColumnOrderContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        DateTime testTime = DateTime.UtcNow;

        List<ColumnOrderEntity> data =
        [
            new() { Column1 = 100, Column2 = testTime, Column3 = "Test" }
        ];

        // Map columns in specific order to match database
        TimescaleCopyConfig<ColumnOrderEntity> config = new TimescaleCopyConfig<ColumnOrderEntity>()
            .ToTable("ColumnOrderEntity")
            .MapColumn("Column1", e => e.Column1, NpgsqlDbType.Integer)
            .MapColumn("Column2", e => e.Column2, NpgsqlDbType.TimestampTz)
            .MapColumn("Column3", e => e.Column3, NpgsqlDbType.Text);

        // Act
        await data.BulkCopyAsync(_connectionString!, config);

        // Assert
        ColumnOrderEntity? inserted = await context.Set<ColumnOrderEntity>().FirstOrDefaultAsync();
        Assert.NotNull(inserted);
        Assert.Equal(100, inserted.Column1);
        Assert.Equal("Test", inserted.Column3);
    }

    #endregion
}
