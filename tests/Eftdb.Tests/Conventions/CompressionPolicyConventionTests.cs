using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

public class CompressionPolicyConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Process_Minimal_After_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days")]
    private class MinimalAfterEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalAfterContext : DbContext
    {
        public DbSet<MinimalAfterEntity> Entities => Set<MinimalAfterEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MinimalAfterEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_Minimal_After_Attribute()
    {
        // Arrange
        using MinimalAfterContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalAfterEntity))!;

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore));
    }

    #endregion

    #region Should_Process_CreatedBefore_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy(createdBefore: "30 days")]
    private class CreatedBeforeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreatedBeforeContext : DbContext
    {
        public DbSet<CreatedBeforeEntity> Entities => Set<CreatedBeforeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("CreatedBeforeEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_CreatedBefore_Attribute()
    {
        // Arrange
        using CreatedBeforeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CreatedBeforeEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore)?.Value);
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.After));
    }

    #endregion

    #region Should_Process_ScheduleInterval_From_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", ScheduleInterval = "12 hours")]
    private class ScheduleIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalContext : DbContext
    {
        public DbSet<ScheduleIntervalEntity> Entities => Set<ScheduleIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ScheduleIntervalEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_ScheduleInterval_From_Attribute()
    {
        // Arrange
        using ScheduleIntervalContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScheduleIntervalEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("12 hours", entityType.FindAnnotation(CompressionPolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region Should_Process_InitialStart_From_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", InitialStart = "2025-10-01T03:00:00Z")]
    private class InitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext : DbContext
    {
        public DbSet<InitialStartEntity> Entities => Set<InitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("InitialStartEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_InitialStart_From_Attribute()
    {
        // Arrange
        using InitialStartContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(InitialStartEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        object? initialStartValue = entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);
        DateTime parsed = ((DateTime)initialStartValue).ToUniversalTime();
        Assert.Equal(2025, parsed.Year);
        Assert.Equal(10, parsed.Month);
        Assert.Equal(1, parsed.Day);
        Assert.Equal(3, parsed.Hour);
    }

    #endregion

    #region Should_Throw_When_InitialStart_Has_Invalid_Format

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", InitialStart = "not-a-date")]
    private class InvalidInitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InvalidInitialStartContext : DbContext
    {
        public DbSet<InvalidInitialStartEntity> Entities => Set<InvalidInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvalidInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("InvalidInitialStartEntity");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_InitialStart_Has_Invalid_Format()
    {
        // Arrange & Act
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using InvalidInitialStartContext context = new();
            _ = GetModel(context);
        });

        // Assert
        Assert.Contains("InitialStart", ex.Message);
        Assert.Contains("not a valid DateTime format", ex.Message);
    }

    #endregion

    #region Should_Process_Timezone_From_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", Timezone = "Europe/Berlin")]
    private class TimezoneEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimezoneContext : DbContext
    {
        public DbSet<TimezoneEntity> Entities => Set<TimezoneEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimezoneEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("TimezoneEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_Timezone_From_Attribute()
    {
        // Arrange
        using TimezoneContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(TimezoneEntity))!;

        // Assert
        Assert.Equal("Europe/Berlin", entityType.FindAnnotation(CompressionPolicyAnnotations.Timezone)?.Value);
    }

    #endregion

    #region Should_Process_IfNotExists_From_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", IfNotExists = true)]
    private class IfNotExistsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IfNotExistsContext : DbContext
    {
        public DbSet<IfNotExistsEntity> Entities => Set<IfNotExistsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IfNotExistsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("IfNotExistsEntity");
            });
        }
    }

    [Fact]
    public void Should_Process_IfNotExists_From_Attribute()
    {
        // Arrange
        using IfNotExistsContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IfNotExistsEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.IfNotExists)?.Value);
    }

    #endregion

    #region Should_Not_Annotate_IfNotExists_When_False

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days")]
    private class IfNotExistsFalseEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IfNotExistsFalseContext : DbContext
    {
        public DbSet<IfNotExistsFalseEntity> Entities => Set<IfNotExistsFalseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IfNotExistsFalseEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("IfNotExistsFalseEntity");
            });
        }
    }

    [Fact]
    public void Should_Not_Annotate_IfNotExists_When_False()
    {
        // Arrange
        using IfNotExistsFalseContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IfNotExistsFalseEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.IfNotExists));
    }

    #endregion

    #region Should_Throw_When_Both_After_And_CreatedBefore_Specified

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", CreatedBefore = "30 days")]
    private class BothSpecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BothSpecifiedContext : DbContext
    {
        public DbSet<BothSpecifiedEntity> Entities => Set<BothSpecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BothSpecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("BothSpecifiedEntity");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Both_After_And_CreatedBefore_Specified()
    {
        // Arrange & Act
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using BothSpecifiedContext context = new();
            _ = GetModel(context);
        });

        // Assert
        Assert.Contains("mutually exclusive", ex.Message);
    }

    #endregion

    #region Should_Throw_When_Neither_After_Nor_CreatedBefore_Specified

    [Hypertable("Timestamp")]
    [CompressionPolicy]
    private class NoneSpecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoneSpecifiedContext : DbContext
    {
        public DbSet<NoneSpecifiedEntity> Entities => Set<NoneSpecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoneSpecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("NoneSpecifiedEntity");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Neither_After_Nor_CreatedBefore_Specified()
    {
        // Arrange & Act
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using NoneSpecifiedContext context = new();
            _ = GetModel(context);
        });

        // Assert
        Assert.Contains("Exactly one of 'After' or 'CreatedBefore' must be specified", ex.Message);
    }

    #endregion

    #region Should_Not_Process_Entity_Without_Attribute

    [Hypertable("Timestamp")]
    private class PlainEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class PlainContext : DbContext
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
                entity.ToTable("PlainEntity");
            });
        }
    }

    [Fact]
    public void Should_Not_Process_Entity_Without_Attribute()
    {
        // Arrange
        using PlainContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(PlainEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.After));
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore));
    }

    #endregion

    #region Attribute_Should_Produce_Same_Annotations_As_FluentAPI

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days", ScheduleInterval = "12 hours", Timezone = "UTC")]
    private class EquivalenceAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Timestamp")]
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
                entity.ToTable("EquivalenceAttributeEntity");
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
                entity.ToTable("EquivalenceAttributeEntity");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days", scheduleInterval: "12 hours", timezone: "UTC");
            });
        }
    }

    [Fact]
    public void Attribute_Should_Produce_Same_Annotations_As_FluentAPI()
    {
        // Arrange
        using AttributeBasedContext attrCtx = new();
        using FluentApiBasedContext fluentCtx = new();

        // Act
        IModel attrModel = GetModel(attrCtx);
        IModel fluentModel = GetModel(fluentCtx);
        IEntityType attrEntity = attrModel.FindEntityType(typeof(EquivalenceAttributeEntity))!;
        IEntityType fluentEntity = fluentModel.FindEntityType(typeof(EquivalenceFluentEntity))!;

        // Assert
        Assert.Equal(
            attrEntity.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value,
            fluentEntity.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal(
            attrEntity.FindAnnotation(CompressionPolicyAnnotations.After)?.Value,
            fluentEntity.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
        Assert.Equal(
            attrEntity.FindAnnotation(CompressionPolicyAnnotations.ScheduleInterval)?.Value,
            fluentEntity.FindAnnotation(CompressionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal(
            attrEntity.FindAnnotation(CompressionPolicyAnnotations.Timezone)?.Value,
            fluentEntity.FindAnnotation(CompressionPolicyAnnotations.Timezone)?.Value);
    }

    #endregion
}
