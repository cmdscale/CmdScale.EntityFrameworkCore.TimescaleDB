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
}
