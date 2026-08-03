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

    #region WithCompressionSegmentBy_Should_Set_Annotation_And_Enable_Compression

    private class SegmentByEntity
    {
        public DateTime Timestamp { get; set; }
        public int TenantId { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class SegmentByContext : DbContext
    {
        public DbSet<SegmentByEntity> Metrics => Set<SegmentByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SegmentByEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionSegmentBy(x => x.TenantId, x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void WithCompressionSegmentBy_Should_Set_Annotation_And_Enable_Compression()
    {
        using SegmentByContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SegmentByEntity))!;

        Assert.Equal("TenantId, DeviceId", entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region WithCompressionOrderBy_Builder_Syntax_Should_Set_Annotation

    private class OrderByBuilderEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderByBuilderContext : DbContext
    {
        public DbSet<OrderByBuilderEntity> Metrics => Set<OrderByBuilderEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderByBuilderEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(
                          OrderByBuilder.For<OrderByBuilderEntity>(x => x.Timestamp).Descending(),
                          OrderByBuilder.For<OrderByBuilderEntity>(x => x.Value).Ascending(nullsFirst: true)
                      );
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_Builder_Syntax_Should_Set_Annotation()
    {
        using OrderByBuilderContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(OrderByBuilderEntity))!;

        Assert.Equal("Timestamp DESC, Value ASC NULLS FIRST", entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region WithCompressionOrderBy_Selector_Syntax_Should_Set_Annotation

    private class OrderBySelectorEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderBySelectorContext : DbContext
    {
        public DbSet<OrderBySelectorEntity> Metrics => Set<OrderBySelectorEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderBySelectorEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => [
                          s.ByDescending(x => x.Timestamp),
                          s.ByAscending(x => x.Value, nullsFirst: true)
                      ]);
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_Selector_Syntax_Should_Set_Annotation()
    {
        using OrderBySelectorContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(OrderBySelectorEntity))!;

        Assert.Equal("Timestamp DESC, Value ASC NULLS FIRST", entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Support_Chaining_All_Compression_Methods

    private class FullCompressionEntity
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class FullCompressionContext : DbContext
    {
        public DbSet<FullCompressionEntity> Metrics => Set<FullCompressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullCompressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("7 days")
                      .WithCompressionSegmentBy(x => x.DeviceId)
                      .WithCompressionOrderBy(s => [s.ByDescending(x => x.Timestamp)])
                      .WithChunkSkipping(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Support_Chaining_All_Compression_Methods()
    {
        using FullCompressionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullCompressionEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);

        Assert.Equal("DeviceId", entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value);
        Assert.Equal("Timestamp DESC", entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value);
        Assert.Equal("DeviceId", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
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

    #region WithMigrateData_Should_Set_MigrateData_Annotation_True_By_Default

    private class MigrateDataDefaultEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataDefaultContext : DbContext
    {
        public DbSet<MigrateDataDefaultEntity> Metrics => Set<MigrateDataDefaultEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataDefaultEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithMigrateData();
            });
        }
    }

    [Fact]
    public void WithMigrateData_Should_Set_MigrateData_Annotation_True_By_Default()
    {
        using MigrateDataDefaultContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataDefaultEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
    }

    #endregion

    #region WithMigrateData_Should_Support_Explicit_True

    private class MigrateDataExplicitTrueEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataExplicitTrueContext : DbContext
    {
        public DbSet<MigrateDataExplicitTrueEntity> Metrics => Set<MigrateDataExplicitTrueEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataExplicitTrueEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithMigrateData(true);
            });
        }
    }

    [Fact]
    public void WithMigrateData_Should_Support_Explicit_True()
    {
        using MigrateDataExplicitTrueContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataExplicitTrueEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
    }

    #endregion

    #region WithMigrateData_Should_Support_Explicit_False

    private class MigrateDataExplicitFalseEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataExplicitFalseContext : DbContext
    {
        public DbSet<MigrateDataExplicitFalseEntity> Metrics => Set<MigrateDataExplicitFalseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataExplicitFalseEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithMigrateData(false);
            });
        }
    }

    [Fact]
    public void WithMigrateData_Should_Support_Explicit_False()
    {
        using MigrateDataExplicitFalseContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataExplicitFalseEntity))!;

        Assert.Equal(false, entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
    }

    #endregion

    #region WithMigrateData_Should_Support_Method_Chaining

    private class MigrateDataChainingEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataChainingContext : DbContext
    {
        public DbSet<MigrateDataChainingEntity> Metrics => Set<MigrateDataChainingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataChainingEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithMigrateData()
                      .WithChunkTimeInterval("1 hour")
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public void WithMigrateData_Should_Support_Method_Chaining()
    {
        using MigrateDataChainingContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataChainingEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_DateTimeOffset_TimeColumn

    private class DateTimeOffsetTimeColumnEntity
    {
        public DateTimeOffset EventTime { get; set; }
        public double Value { get; set; }
    }

    private class DateTimeOffsetTimeColumnContext : DbContext
    {
        public DbSet<DateTimeOffsetTimeColumnEntity> Metrics => Set<DateTimeOffsetTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DateTimeOffsetTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_datetimeoffset_time");
                entity.IsHypertable(x => x.EventTime);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_DateTimeOffset_TimeColumn()
    {
        using DateTimeOffsetTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DateTimeOffsetTimeColumnEntity))!;

        Assert.Equal("EventTime", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_DateOnly_TimeColumn

    private class DateOnlyTimeColumnEntity
    {
        public DateOnly EventDate { get; set; }
        public double Value { get; set; }
    }

    private class DateOnlyTimeColumnContext : DbContext
    {
        public DbSet<DateOnlyTimeColumnEntity> Metrics => Set<DateOnlyTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DateOnlyTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_dateonly_time");
                entity.IsHypertable(x => x.EventDate);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_DateOnly_TimeColumn()
    {
        using DateOnlyTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DateOnlyTimeColumnEntity))!;

        Assert.Equal("EventDate", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_Long_TimeColumn

    private class LongTimeColumnEntity
    {
        public long EventTimestamp { get; set; }
        public double Value { get; set; }
    }

    private class LongTimeColumnContext : DbContext
    {
        public DbSet<LongTimeColumnEntity> Metrics => Set<LongTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LongTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_long_time");
                entity.IsHypertable(x => x.EventTimestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_Long_TimeColumn()
    {
        using LongTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(LongTimeColumnEntity))!;

        Assert.Equal("EventTimestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_Int_TimeColumn

    private class IntTimeColumnEntity
    {
        public int EventTimestamp { get; set; }
        public double Value { get; set; }
    }

    private class IntTimeColumnContext : DbContext
    {
        public DbSet<IntTimeColumnEntity> Metrics => Set<IntTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_int_time");
                entity.IsHypertable(x => x.EventTimestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_Int_TimeColumn()
    {
        using IntTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IntTimeColumnEntity))!;

        Assert.Equal("EventTimestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_Short_TimeColumn

    private class ShortTimeColumnEntity
    {
        public short EventTimestamp { get; set; }
        public double Value { get; set; }
    }

    private class ShortTimeColumnContext : DbContext
    {
        public DbSet<ShortTimeColumnEntity> Metrics => Set<ShortTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShortTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_short_time");
                entity.IsHypertable(x => x.EventTimestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_Short_TimeColumn()
    {
        using ShortTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ShortTimeColumnEntity))!;

        Assert.Equal("EventTimestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region IsHypertable_Should_Accept_Custom_TimeColumn_Type

    private readonly struct CustomInstant(DateTime utcDateTime)
    {
        public DateTime UtcDateTime { get; } = utcDateTime;
    }

    private class CustomTimeColumnEntity
    {
        public CustomInstant Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomTimeColumnContext : DbContext
    {
        public DbSet<CustomTimeColumnEntity> Metrics => Set<CustomTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_custom_time");
                entity.Property(x => x.Timestamp)
                      .HasConversion(v => v.UtcDateTime, v => new CustomInstant(v));
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void IsHypertable_Should_Accept_Custom_TimeColumn_Type()
    {
        using CustomTimeColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CustomTimeColumnEntity))!;

        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region WithChunkSkipping_Should_Throw_For_Non_Member_Expression

    private class NonMemberExpressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NonMemberExpressionContext : DbContext
    {
        public DbSet<NonMemberExpressionEntity> Metrics => Set<NonMemberExpressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NonMemberExpressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_non_member_expression");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.ToString()!);
            });
        }
    }

    [Fact]
    public void WithChunkSkipping_Should_Throw_For_Non_Member_Expression()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            using NonMemberExpressionContext context = new();
            _ = GetModel(context);
        });

        Assert.Contains("simple property access expression", exception.Message);
    }

    #endregion

    #region WithCompressionSegmentBy_Should_Throw_For_Converted_Non_Member_Expression

    private class ConvertedNonMemberExpressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ConvertedNonMemberExpressionContext : DbContext
    {
        public DbSet<ConvertedNonMemberExpressionEntity> Metrics => Set<ConvertedNonMemberExpressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConvertedNonMemberExpressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_converted_non_member_expression");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionSegmentBy(x => (object)(x.Value + 1));
            });
        }
    }

    [Fact]
    public void WithCompressionSegmentBy_Should_Throw_For_Converted_Non_Member_Expression()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            using ConvertedNonMemberExpressionContext context = new();
            _ = GetModel(context);
        });

        Assert.Contains("simple property access expression", exception.Message);
    }

    #endregion

    #region HasRangeDimension_Should_Add_Range_Dimension_To_AdditionalDimensions_Annotation

    private class HasRangeDimensionEntity
    {
        public DateTime Timestamp { get; set; }
        public string? Region { get; set; }
    }

    private class HasRangeDimensionContext : DbContext
    {
        public DbSet<HasRangeDimensionEntity> Metrics => Set<HasRangeDimensionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HasRangeDimensionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_has_range_dimension");
                entity.IsHypertable(x => x.Timestamp)
                      .HasRangeDimension(x => (object)x.Region!, "1 month");
            });
        }
    }

    [Fact]
    public void HasRangeDimension_Should_Add_Range_Dimension_To_AdditionalDimensions_Annotation()
    {
        using HasRangeDimensionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(HasRangeDimensionEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);
        Assert.Equal("Region", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Range, dimensions[0].Type);
        Assert.Equal("1 month", dimensions[0].Interval);
    }

    #endregion

    #region HasHashDimension_Should_Add_Hash_Dimension_To_AdditionalDimensions_Annotation

    private class HasHashDimensionEntity
    {
        public DateTime Timestamp { get; set; }
        public int WarehouseId { get; set; }
    }

    private class HasHashDimensionContext : DbContext
    {
        public DbSet<HasHashDimensionEntity> Metrics => Set<HasHashDimensionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HasHashDimensionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_has_hash_dimension");
                entity.IsHypertable(x => x.Timestamp)
                      .HasHashDimension(x => (object)x.WarehouseId, 8);
            });
        }
    }

    [Fact]
    public void HasHashDimension_Should_Add_Hash_Dimension_To_AdditionalDimensions_Annotation()
    {
        using HasHashDimensionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(HasHashDimensionEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);
        Assert.Equal("WarehouseId", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[0].Type);
        Assert.Equal(8, dimensions[0].NumberOfPartitions);
    }

    #endregion

    #region WithCompressionOrderBy_ParamsFuncSelector_Should_Set_Annotation_With_Direction_Suffixes

    private class ParamsFuncSelectorOrderByEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class ParamsFuncSelectorOrderByContext : DbContext
    {
        public DbSet<ParamsFuncSelectorOrderByEntity> Metrics => Set<ParamsFuncSelectorOrderByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParamsFuncSelectorOrderByEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("params_func_selector_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(
                          s => s.ByDescending(x => x.Timestamp),
                          s => s.ByAscending(x => x.DeviceId));
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_ParamsFuncSelector_Should_Set_Annotation_With_Direction_Suffixes()
    {
        // Arrange
        using ParamsFuncSelectorOrderByContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ParamsFuncSelectorOrderByEntity))!;

        // Act
        string? annotationValue = entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;

        // Assert
        Assert.NotNull(annotationValue);
        Assert.Contains("Timestamp DESC", annotationValue);
        Assert.Contains("DeviceId ASC", annotationValue);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region HasRangeDimension_And_HasHashDimension_Can_Be_Combined

    private class CombinedDimensionEntity
    {
        public DateTime Timestamp { get; set; }
        public string? Region { get; set; }
        public int WarehouseId { get; set; }
    }

    private class CombinedDimensionContext : DbContext
    {
        public DbSet<CombinedDimensionEntity> Metrics => Set<CombinedDimensionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CombinedDimensionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics_combined_dimensions");
                entity.IsHypertable(x => x.Timestamp)
                      .HasRangeDimension(x => (object)x.Region!, "30 days")
                      .HasHashDimension(x => (object)x.WarehouseId, 4);
            });
        }
    }

    [Fact]
    public void HasRangeDimension_And_HasHashDimension_Can_Be_Combined()
    {
        using CombinedDimensionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CombinedDimensionEntity))!;

        string? dimensionsJson = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions)?.Value as string;
        Assert.NotNull(dimensionsJson);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Equal(2, dimensions.Count);

        Assert.Equal("Region", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Range, dimensions[0].Type);
        Assert.Equal("30 days", dimensions[0].Interval);

        Assert.Equal("WarehouseId", dimensions[1].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[1].Type);
        Assert.Equal(4, dimensions[1].NumberOfPartitions);
    }

    #endregion
}
