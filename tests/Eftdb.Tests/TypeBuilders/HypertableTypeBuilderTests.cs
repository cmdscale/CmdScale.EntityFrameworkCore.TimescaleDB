using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify HypertableTypeBuilder Fluent API methods correctly apply annotations.
/// </summary>
public class HypertableTypeBuilderTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region IsHypertable_Should_Set_IsHypertable_Annotation

    private class IsHypertableAnnotationEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsHypertableAnnotationContext : DbContext
    {
        public DbSet<IsHypertableAnnotationEntity> Metrics => Set<IsHypertableAnnotationEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsHypertableAnnotationEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Set_IsHypertable_Annotation()
    {
        using IsHypertableAnnotationContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsHypertableAnnotationEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Set_TimeColumn_From_Expression

    private class TimeColumnExpressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimeColumnExpressionContext : DbContext
    {
        public DbSet<TimeColumnExpressionEntity> Metrics => Set<TimeColumnExpressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeColumnExpressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Set_TimeColumn_From_Expression()
    {
        using TimeColumnExpressionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(TimeColumnExpressionEntity))!;

        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Handle_ValueType_Property_Expression

    private class ValueTypePropertyEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ValueTypePropertyContext : DbContext
    {
        public DbSet<ValueTypePropertyEntity> Metrics => Set<ValueTypePropertyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ValueTypePropertyEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Handle_ValueType_Property_Expression()
    {
        using ValueTypePropertyContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ValueTypePropertyEntity))!;

        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
    }

    #endregion

    #region WithChunkTimeInterval_Should_Set_ChunkTimeInterval_Annotation

    private class ChunkTimeIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChunkTimeIntervalContext : DbContext
    {
        public DbSet<ChunkTimeIntervalEntity> Metrics => Set<ChunkTimeIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkTimeIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public void WithChunkTimeInterval_Should_Set_ChunkTimeInterval_Annotation()
    {
        using ChunkTimeIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ChunkTimeIntervalEntity))!;

        Assert.Equal("1 day", entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
    }

    #endregion

    #region WithChunkTimeInterval_Should_Support_Various_Interval_Formats

    private class HourlyIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MonthlyIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MicrosecondIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleIntervalFormatsContext : DbContext
    {
        public DbSet<HourlyIntervalEntity> Hourly => Set<HourlyIntervalEntity>();
        public DbSet<MonthlyIntervalEntity> Monthly => Set<MonthlyIntervalEntity>();
        public DbSet<MicrosecondIntervalEntity> Microsecond => Set<MicrosecondIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HourlyIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Hourly");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 hour");
            });

            modelBuilder.Entity<MonthlyIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Monthly");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 month");
            });

            modelBuilder.Entity<MicrosecondIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Microsecond");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("86400000000");
            });
        }
    }

    [Fact]
    public void WithChunkTimeInterval_Should_Support_Various_Interval_Formats()
    {
        using MultipleIntervalFormatsContext context = new();
        IModel model = GetModel(context);

        IEntityType entity1 = model.FindEntityType(typeof(HourlyIntervalEntity))!;
        Assert.Equal("1 hour", entity1.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);

        IEntityType entity2 = model.FindEntityType(typeof(MonthlyIntervalEntity))!;
        Assert.Equal("1 month", entity2.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);

        IEntityType entity3 = model.FindEntityType(typeof(MicrosecondIntervalEntity))!;
        Assert.Equal("86400000000", entity3.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
    }

    #endregion

    #region EnableCompression_Should_Set_EnableCompression_Annotation_True_By_Default

    private class CompressionEnabledEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionEnabledContext : DbContext
    {
        public DbSet<CompressionEnabledEntity> Metrics => Set<CompressionEnabledEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionEnabledEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public void EnableCompression_Should_Set_EnableCompression_Annotation_True_By_Default()
    {
        using CompressionEnabledContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressionEnabledEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region EnableCompression_Should_Support_Explicit_False

    private class CompressionDisabledEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionDisabledContext : DbContext
    {
        public DbSet<CompressionDisabledEntity> Metrics => Set<CompressionDisabledEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionDisabledEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression(false);
            });
        }
    }

    [Fact]
    public void EnableCompression_Should_Support_Explicit_False()
    {
        using CompressionDisabledContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressionDisabledEntity))!;

        Assert.Equal(false, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region WithChunkSkipping_Should_Set_ChunkSkipColumns_Annotation

    private class ChunkSkippingEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class ChunkSkippingContext : DbContext
    {
        public DbSet<ChunkSkippingEntity> Metrics => Set<ChunkSkippingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkSkippingEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value);
            });
        }
    }

    [Fact]
    public void WithChunkSkipping_Should_Set_ChunkSkipColumns_Annotation()
    {
        using ChunkSkippingContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ChunkSkippingEntity))!;

        Assert.Equal("Value", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
    }

    #endregion

    #region WithChunkSkipping_Should_Support_Multiple_Columns

    private class MultipleChunkSkipEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class MultipleChunkSkipColumnsContext : DbContext
    {
        public DbSet<MultipleChunkSkipEntity> Metrics => Set<MultipleChunkSkipEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleChunkSkipEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value, x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void WithChunkSkipping_Should_Support_Multiple_Columns()
    {
        using MultipleChunkSkipColumnsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MultipleChunkSkipEntity))!;

        string? chunkSkipColumns = entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value as string;
        Assert.NotNull(chunkSkipColumns);
        Assert.Contains("Value", chunkSkipColumns);
        Assert.Contains("DeviceId", chunkSkipColumns);
    }

    #endregion

    #region WithChunkSkipping_Should_Join_Multiple_Columns_With_Comma

    private class CommaJoinedChunkSkipEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class CommaJoinedChunkSkipContext : DbContext
    {
        public DbSet<CommaJoinedChunkSkipEntity> Metrics => Set<CommaJoinedChunkSkipEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CommaJoinedChunkSkipEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value, x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void WithChunkSkipping_Should_Join_Multiple_Columns_With_Comma()
    {
        using CommaJoinedChunkSkipContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CommaJoinedChunkSkipEntity))!;

        Assert.Equal("Value,DeviceId", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
    }

    #endregion

    #region HasDimension_Should_Add_Hash_Dimension

    private class HashDimensionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class HashDimensionContext : DbContext
    {
        public DbSet<HashDimensionEntity> Metrics => Set<HashDimensionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HashDimensionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void HasDimension_Should_Add_Hash_Dimension()
    {
        using HashDimensionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(HashDimensionEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);
        Assert.Equal("DeviceId", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[0].Type);
        Assert.Equal(4, dimensions[0].NumberOfPartitions);
    }

    #endregion

    #region HasDimension_Should_Add_Range_Dimension

    private class RangeDimensionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Location { get; set; }
    }

    private class RangeDimensionContext : DbContext
    {
        public DbSet<RangeDimensionEntity> Metrics => Set<RangeDimensionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RangeDimensionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateRange("Location", "1000"));
            });
        }
    }

    [Fact]
    public void HasDimension_Should_Add_Range_Dimension()
    {
        using RangeDimensionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RangeDimensionEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);
        Assert.Equal("Location", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Range, dimensions[0].Type);
        Assert.Equal("1000", dimensions[0].Interval);
    }

    #endregion

    #region HasDimension_Should_Support_Multiple_Dimensions

    private class MultipleDimensionsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
        public string? Location { get; set; }
    }

    private class MultipleDimensionsContext : DbContext
    {
        public DbSet<MultipleDimensionsEntity> Metrics => Set<MultipleDimensionsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleDimensionsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4))
                      .HasDimension(Dimension.CreateRange("Location", "1000"));
            });
        }
    }

    [Fact]
    public void HasDimension_Should_Support_Multiple_Dimensions()
    {
        using MultipleDimensionsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MultipleDimensionsEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Equal(2, dimensions.Count);

        Assert.Equal("DeviceId", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[0].Type);

        Assert.Equal("Location", dimensions[1].ColumnName);
        Assert.Equal(EDimensionType.Range, dimensions[1].Type);
    }

    #endregion

    #region FluentAPI_Should_Support_Method_Chaining

    private class FullyConfiguredEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class FullyConfiguredHypertableContext : DbContext
    {
        public DbSet<FullyConfiguredEntity> Metrics => Set<FullyConfiguredEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day")
                      .EnableCompression()
                      .WithChunkSkipping(x => x.Value)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void FluentAPI_Should_Support_Method_Chaining()
    {
        using FullyConfiguredHypertableContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        Assert.Equal("Value", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);
        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions!);
    }

    #endregion
}
