using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify HypertableConvention processes [Hypertable] attribute correctly
/// and applies the same annotations as the Fluent API.
/// </summary>
public class HypertableConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Process_Minimal_Hypertable_Attribute

    [Hypertable("Timestamp")]
    private class MinimalHypertableEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalAttributeContext : DbContext
    {
        public DbSet<MinimalHypertableEntity> Entities => Set<MinimalHypertableEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalHypertableEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MinimalHypertable");
            });
        }
    }

    [Fact]
    public void Should_Process_Minimal_Hypertable_Attribute()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalHypertableEntity))!;

        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
    }

    #endregion

    #region Should_Process_Hypertable_With_ChunkTimeInterval

    [Hypertable("Timestamp", ChunkTimeInterval = "1 day")]
    private class ChunkIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChunkIntervalAttributeContext : DbContext
    {
        public DbSet<ChunkIntervalEntity> Entities => Set<ChunkIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ChunkInterval");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_ChunkTimeInterval()
    {
        using ChunkIntervalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ChunkIntervalEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
    }

    #endregion

    #region Should_Process_Hypertable_With_Compression_Enabled

    [Hypertable("Timestamp", EnableCompression = true)]
    private class CompressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionAttributeContext : DbContext
    {
        public DbSet<CompressionEntity> Entities => Set<CompressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Compression");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_Compression_Enabled()
    {
        using CompressionAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressionEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Process_Hypertable_With_CompressionSegmentBy

    [Hypertable("Timestamp", CompressionSegmentBy = ["TenantId", "DeviceId"])]
    private class SegmentByEntity
    {
        public DateTime Timestamp { get; set; }
        public int TenantId { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class SegmentByContext : DbContext
    {
        public DbSet<SegmentByEntity> Entities => Set<SegmentByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SegmentByEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("SegmentBy");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_CompressionSegmentBy()
    {
        using SegmentByContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SegmentByEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        // Should implicitly enable compression
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        // Should join array with comma space
        Assert.Equal("TenantId, DeviceId", entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value);
    }

    #endregion

    #region Should_Process_Hypertable_With_CompressionOrderBy

    [Hypertable("Timestamp", CompressionOrderBy = ["Timestamp DESC", "Value ASC NULLS LAST"])]
    private class OrderByEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderByContext : DbContext
    {
        public DbSet<OrderByEntity> Entities => Set<OrderByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderByEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("OrderBy");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_CompressionOrderBy()
    {
        using OrderByContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(OrderByEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        // Should implicitly enable compression
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        // Should preserve raw SQL strings joined by comma space
        Assert.Equal("Timestamp DESC, Value ASC NULLS LAST", entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value);
    }

    #endregion

    #region Should_Not_Apply_Compression_Settings_When_Arrays_Empty

    [Hypertable("Timestamp", CompressionSegmentBy = [], CompressionOrderBy = [])]
    private class EmptyCompressionSettingsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EmptyCompressionSettingsContext : DbContext
    {
        public DbSet<EmptyCompressionSettingsEntity> Entities => Set<EmptyCompressionSettingsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EmptyCompressionSettingsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("EmptyCompressionSettings");
            });
        }
    }

    [Fact]
    public void Should_Not_Apply_Compression_Settings_When_Arrays_Empty()
    {
        using EmptyCompressionSettingsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EmptyCompressionSettingsEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);

        // Should NOT enable compression because arrays are empty
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.EnableCompression));

        // Should NOT set the segment/order annotations
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.CompressionSegmentBy));
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.CompressionOrderBy));
    }

    #endregion

    #region Should_Process_Hypertable_With_ChunkSkipColumns

    [Hypertable("Timestamp", ChunkSkipColumns = ["Value", "DeviceId"])]
    private class ChunkSkippingEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class ChunkSkippingAttributeContext : DbContext
    {
        public DbSet<ChunkSkippingEntity> Entities => Set<ChunkSkippingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkSkippingEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ChunkSkipping");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_ChunkSkipColumns()
    {
        using ChunkSkippingAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ChunkSkippingEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        Assert.Equal("Value,DeviceId", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
    }

    #endregion

    #region Should_Auto_Enable_Compression_When_ChunkSkipColumns_Present

    [Hypertable("Timestamp", ChunkSkipColumns = ["Value", "DeviceId"])]
    private class AutoCompressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class AutoCompressionContext : DbContext
    {
        public DbSet<AutoCompressionEntity> Entities => Set<AutoCompressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AutoCompressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("AutoCompression");
            });
        }
    }

    [Fact]
    public void Should_Auto_Enable_Compression_When_ChunkSkipColumns_Present()
    {
        using AutoCompressionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AutoCompressionEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Process_Fully_Configured_Hypertable

    [Hypertable("Timestamp", ChunkTimeInterval = "1 day", EnableCompression = true, ChunkSkipColumns = ["Value"])]
    private class FullyConfiguredEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredAttributeContext : DbContext
    {
        public DbSet<FullyConfiguredEntity> Entities => Set<FullyConfiguredEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("FullyConfigured");
            });
        }
    }

    [Fact]
    public void Should_Process_Fully_Configured_Hypertable()
    {
        using FullyConfiguredAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        Assert.Equal("Value", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
    }

    #endregion

    #region Should_Not_Process_Entity_Without_Attribute

    private class PlainEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoAttributeContext : DbContext
    {
        public DbSet<PlainEntity> Entities => Set<PlainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Plain");
            });
        }
    }

    [Fact]
    public void Should_Not_Process_Entity_Without_Attribute()
    {
        using NoAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(PlainEntity))!;

        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.IsHypertable));
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn));
    }

    #endregion

    #region Should_Not_Apply_ChunkTimeInterval_When_Empty

    [Hypertable("Timestamp", ChunkTimeInterval = "")]
    private class EmptyChunkIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EmptyChunkIntervalContext : DbContext
    {
        public DbSet<EmptyChunkIntervalEntity> Entities => Set<EmptyChunkIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EmptyChunkIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("EmptyChunkInterval");
            });
        }
    }

    [Fact]
    public void Should_Not_Apply_ChunkTimeInterval_When_Empty()
    {
        using EmptyChunkIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EmptyChunkIntervalEntity))!;

        // ChunkTimeInterval annotation should be null when the attribute property is empty
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval));
        // But IsHypertable should still be set
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
    }

    #endregion

    #region Should_Not_Enable_Compression_For_Empty_ChunkSkipColumns_Array

    [Hypertable("Timestamp", ChunkSkipColumns = new string[0])]
    private class EmptyChunkSkipColumnsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EmptyChunkSkipColumnsContext : DbContext
    {
        public DbSet<EmptyChunkSkipColumnsEntity> Entities => Set<EmptyChunkSkipColumnsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EmptyChunkSkipColumnsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("EmptyChunkSkipColumns");
            });
        }
    }

    [Fact]
    public void Should_Not_Enable_Compression_For_Empty_ChunkSkipColumns_Array()
    {
        using EmptyChunkSkipColumnsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EmptyChunkSkipColumnsEntity))!;

        // Empty ChunkSkipColumns should NOT enable compression or set ChunkSkipColumns annotation
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.EnableCompression));
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns));
        // But IsHypertable should still be set
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Verify_EnableCompression_False_Explicitly

    [Hypertable("Timestamp", EnableCompression = false)]
    private class NoCompressionEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoCompressionContext : DbContext
    {
        public DbSet<NoCompressionEntity> Entities => Set<NoCompressionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoCompressionEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("NoCompression");
            });
        }
    }

    [Fact]
    public void Should_Verify_EnableCompression_False_Explicitly()
    {
        using NoCompressionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoCompressionEntity))!;

        // EnableCompression should be null (not set) when false
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.EnableCompression));
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Apply_Default_ChunkTimeInterval_When_Not_Set

    [Hypertable("Timestamp")]
    private class DefaultChunkIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultChunkIntervalContext : DbContext
    {
        public DbSet<DefaultChunkIntervalEntity> Entities => Set<DefaultChunkIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultChunkIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("DefaultChunkInterval");
            });
        }
    }

    [Fact]
    public void Should_Apply_Default_ChunkTimeInterval_When_Not_Set()
    {
        using DefaultChunkIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DefaultChunkIntervalEntity))!;

        // When ChunkTimeInterval is not explicitly set, it uses the DefaultValues.ChunkTimeInterval
        // and should still be applied as an annotation
        Assert.Equal(DefaultValues.ChunkTimeInterval, entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value);
    }

    #endregion

    #region Should_Handle_Single_ChunkSkipColumn

    [Hypertable("Timestamp", ChunkSkipColumns = ["Value"])]
    private class SingleChunkSkipColumnEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SingleChunkSkipColumnContext : DbContext
    {
        public DbSet<SingleChunkSkipColumnEntity> Entities => Set<SingleChunkSkipColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleChunkSkipColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("SingleChunkSkipColumn");
            });
        }
    }

    [Fact]
    public void Should_Handle_Single_ChunkSkipColumn()
    {
        using SingleChunkSkipColumnContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SingleChunkSkipColumnEntity))!;

        // Single column should be stored without extra commas
        Assert.Equal("Value", entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Attribute_Should_Produce_Same_Annotations_As_FluentAPI

    [Hypertable("Timestamp", ChunkTimeInterval = "1 hour", EnableCompression = true)]
    private class EquivalenceAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EquivalenceFluentEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttributeBasedContext : DbContext
    {
        public DbSet<EquivalenceAttributeEntity> Entities => Set<EquivalenceAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Equivalence");
            });
        }
    }

    private class FluentApiBasedContext : DbContext
    {
        public DbSet<EquivalenceFluentEntity> Entities => Set<EquivalenceFluentEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceFluentEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Equivalence");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 hour")
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public void Attribute_Should_Produce_Same_Annotations_As_FluentAPI()
    {
        using AttributeBasedContext attributeContext = new();
        using FluentApiBasedContext fluentContext = new();

        IModel attributeModel = GetModel(attributeContext);
        IModel fluentModel = GetModel(fluentContext);

        IEntityType attributeEntity = attributeModel.FindEntityType(typeof(EquivalenceAttributeEntity))!;
        IEntityType fluentEntity = fluentModel.FindEntityType(typeof(EquivalenceFluentEntity))!;

        Assert.Equal(
            attributeEntity.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value
        );
    }

    #endregion

    #region Should_Process_Hypertable_With_MigrateData_True

    [Hypertable("Timestamp", MigrateData = true)]
    private class MigrateDataTrueEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataTrueContext : DbContext
    {
        public DbSet<MigrateDataTrueEntity> Entities => Set<MigrateDataTrueEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataTrueEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MigrateDataTrue");
            });
        }
    }

    [Fact]
    public void Should_Process_Hypertable_With_MigrateData_True()
    {
        using MigrateDataTrueContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataTrueEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
    }

    #endregion

    #region Should_Not_Apply_MigrateData_When_False

    [Hypertable("Timestamp", MigrateData = false)]
    private class MigrateDataFalseEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataFalseContext : DbContext
    {
        public DbSet<MigrateDataFalseEntity> Entities => Set<MigrateDataFalseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataFalseEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MigrateDataFalse");
            });
        }
    }

    [Fact]
    public void Should_Not_Apply_MigrateData_When_False()
    {
        using MigrateDataFalseContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataFalseEntity))!;

        // MigrateData annotation should be null when the attribute property is false
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.MigrateData));
        // But IsHypertable should still be set
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Not_Apply_MigrateData_By_Default

    [Hypertable("Timestamp")]
    private class MigrateDataDefaultEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataDefaultContext : DbContext
    {
        public DbSet<MigrateDataDefaultEntity> Entities => Set<MigrateDataDefaultEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataDefaultEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MigrateDataDefault");
            });
        }
    }

    [Fact]
    public void Should_Not_Apply_MigrateData_By_Default()
    {
        using MigrateDataDefaultContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MigrateDataDefaultEntity))!;

        // When MigrateData is not explicitly set in attribute, annotation should be null
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.MigrateData));
        // But IsHypertable should still be set
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region MigrateData_Attribute_Should_Produce_Same_Annotation_As_FluentAPI

    [Hypertable("Timestamp", MigrateData = true)]
    private class MigrateDataAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataFluentEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataAttributeContext : DbContext
    {
        public DbSet<MigrateDataAttributeEntity> Entities => Set<MigrateDataAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MigrateDataEquivalence");
            });
        }
    }

    private class MigrateDataFluentContext : DbContext
    {
        public DbSet<MigrateDataFluentEntity> Entities => Set<MigrateDataFluentEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataFluentEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MigrateDataEquivalence");
                entity.IsHypertable(x => x.Timestamp)
                      .WithMigrateData(true);
            });
        }
    }

    [Fact]
    public void MigrateData_Attribute_Should_Produce_Same_Annotation_As_FluentAPI()
    {
        using MigrateDataAttributeContext attributeContext = new();
        using MigrateDataFluentContext fluentContext = new();

        IModel attributeModel = GetModel(attributeContext);
        IModel fluentModel = GetModel(fluentContext);

        IEntityType attributeEntity = attributeModel.FindEntityType(typeof(MigrateDataAttributeEntity))!;
        IEntityType fluentEntity = fluentModel.FindEntityType(typeof(MigrateDataFluentEntity))!;

        Assert.Equal(
            attributeEntity.FindAnnotation(HypertableAnnotations.MigrateData)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.MigrateData)?.Value
        );
        Assert.Equal(true, attributeEntity.FindAnnotation(HypertableAnnotations.MigrateData)?.Value);
    }

    #endregion
}
