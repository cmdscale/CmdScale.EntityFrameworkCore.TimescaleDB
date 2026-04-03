using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify RetentionPolicyConvention processes [RetentionPolicy] attribute correctly
/// and applies the same annotations as the Fluent API.
/// </summary>
public class RetentionPolicyConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Process_Minimal_RetentionPolicy_Attribute

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days")]
    private class MinimalRetentionPolicyEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalAttributeContext : DbContext
    {
        public DbSet<MinimalRetentionPolicyEntity> Entities => Set<MinimalRetentionPolicyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalRetentionPolicyEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MinimalRetentionPolicy");
            });
        }
    }

    [Fact]
    public void Should_Process_Minimal_RetentionPolicy_Attribute()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalRetentionPolicyEntity))!;

        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_DropCreatedBefore

    [Hypertable("Timestamp")]
    [RetentionPolicy(dropCreatedBefore: "30 days")]
    private class DropCreatedBeforeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeAttributeContext : DbContext
    {
        public DbSet<DropCreatedBeforeEntity> Entities => Set<DropCreatedBeforeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropCreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("DropCreatedBefore");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_DropCreatedBefore()
    {
        using DropCreatedBeforeAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DropCreatedBeforeEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter));
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_ScheduleInterval

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", ScheduleInterval = "1 day")]
    private class ScheduleIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalAttributeContext : DbContext
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
                entity.ToTable("ScheduleInterval");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_ScheduleInterval()
    {
        using ScheduleIntervalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScheduleIntervalEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_InitialStart

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", InitialStart = "2025-01-01T00:00:00Z")]
    private class InitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartAttributeContext : DbContext
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
                entity.ToTable("InitialStart");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_InitialStart()
    {
        using InitialStartAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(InitialStartEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);

        object? initialStartValue = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);

        DateTime initialStart = (DateTime)initialStartValue;
        DateTime utcStart = initialStart.ToUniversalTime();
        Assert.Equal(2025, utcStart.Year);
        Assert.Equal(1, utcStart.Month);
        Assert.Equal(1, utcStart.Day);
        Assert.Equal(0, utcStart.Hour);
        Assert.Equal(0, utcStart.Minute);
        Assert.Equal(0, utcStart.Second);
    }

    #endregion

    #region Should_Throw_When_InitialStart_Has_Invalid_Format

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", InitialStart = "invalid-date-format")]
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
                entity.ToTable("InvalidInitialStart");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_InitialStart_Has_Invalid_Format()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using InvalidInitialStartContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("InitialStart", exception.Message);
        Assert.Contains("not a valid DateTime format", exception.Message);
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_MaxRetries

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", MaxRetries = 5)]
    private class MaxRetriesEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesAttributeContext : DbContext
    {
        public DbSet<MaxRetriesEntity> Entities => Set<MaxRetriesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MaxRetries");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_MaxRetries()
    {
        using MaxRetriesAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRetriesEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal(5, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
    }

    #endregion

    #region Should_Not_Set_MaxRetries_When_Using_Default_Value

    [Fact]
    public void Should_Not_Set_MaxRetries_When_Using_Default_Value()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalRetentionPolicyEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries));
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_MaxRuntime

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", MaxRuntime = "1 hour")]
    private class MaxRuntimeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRuntimeAttributeContext : DbContext
    {
        public DbSet<MaxRuntimeEntity> Entities => Set<MaxRuntimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MaxRuntime");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_MaxRuntime()
    {
        using MaxRuntimeAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRuntimeEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_RetryPeriod

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", RetryPeriod = "30 minutes")]
    private class RetryPeriodEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetryPeriodAttributeContext : DbContext
    {
        public DbSet<RetryPeriodEntity> Entities => Set<RetryPeriodEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetryPeriodEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("RetryPeriod");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_RetryPeriod()
    {
        using RetryPeriodAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RetryPeriodEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("30 minutes", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region Should_Process_Fully_Configured_RetentionPolicy

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", ScheduleInterval = "1 day", InitialStart = "2025-01-01T00:00:00Z", MaxRuntime = "1 hour", MaxRetries = 3, RetryPeriod = "30 minutes")]
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
    public void Should_Process_Fully_Configured_RetentionPolicy()
    {
        using FullyConfiguredAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("30 minutes", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);

        object? initialStartValue = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);
    }

    #endregion

    #region Should_Not_Process_Entity_Without_Attribute

    [Hypertable("Timestamp")]
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

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore));
    }

    #endregion

    #region Attribute_Should_Produce_Same_Annotations_As_FluentAPI

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", ScheduleInterval = "1 day", MaxRuntime = "1 hour")]
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
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "1 day",
                    maxRuntime: "1 hour"
                );
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
            attributeEntity.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value,
            fluentEntity.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value,
            fluentEntity.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value,
            fluentEntity.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value,
            fluentEntity.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value
        );
    }

    #endregion

    #region Should_Process_RetentionPolicy_With_MaxRetries_Zero

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", MaxRetries = 0)]
    private class MaxRetriesZeroEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesZeroAttributeContext : DbContext
    {
        public DbSet<MaxRetriesZeroEntity> Entities => Set<MaxRetriesZeroEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesZeroEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MaxRetriesZero");
            });
        }
    }

    [Fact]
    public void Should_Process_RetentionPolicy_With_MaxRetries_Zero()
    {
        using MaxRetriesZeroAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRetriesZeroEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal(0, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
    }

    #endregion

    #region Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified_Via_Attribute

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days", DropCreatedBefore = "30 days")]
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
                entity.ToTable("BothSpecified");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified_Via_Attribute()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using BothSpecifiedContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("mutually exclusive", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified_Via_Attribute

    [Hypertable("Timestamp")]
    [RetentionPolicy]
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
                entity.ToTable("NoneSpecified");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified_Via_Attribute()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using NoneSpecifiedContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("RetentionPolicy: Exactly one of 'DropAfter' or 'DropCreatedBefore' must be specified.", exception.Message);
    }

    #endregion
}
