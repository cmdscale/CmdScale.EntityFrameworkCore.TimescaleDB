using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify ContinuousAggregatePolicyConvention processes [ContinuousAggregatePolicyAttribute] correctly
/// and applies the same annotations as the Fluent API.
/// </summary>
/// <remarks>
/// NOTE: The IncludeTieredData property is a nullable bool which cannot be used in C# attributes.
/// Therefore, tests for IncludeTieredData can only be performed using Fluent API, not attributes.
/// </remarks>
public class ContinuousAggregatePolicyConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Apply_HasRefreshPolicy_Annotation

    private class MetricEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour")]
    private class AggregateEntity1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MinimalAttributeContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();
        public DbSet<AggregateEntity1> Aggregates => Set<AggregateEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity1>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity1>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_HasRefreshPolicy_Annotation()
    {
        // Arrange
        using MinimalAttributeContext1 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity1))!;

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
    }

    #endregion

    #region Should_Apply_StartOffset_Annotation

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "7 days", EndOffset = "1 hour", ScheduleInterval = "1 hour")]
    private class AggregateEntity2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StartOffsetContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<AggregateEntity2> Aggregates => Set<AggregateEntity2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity2>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity2>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_StartOffset_Annotation()
    {
        // Arrange
        using StartOffsetContext2 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity2))!;

        // Assert
        Assert.Equal("7 days", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value);
    }

    #endregion

    #region Should_Apply_EndOffset_Annotation

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "30 minutes", ScheduleInterval = "1 hour")]
    private class AggregateEntity3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class EndOffsetContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<AggregateEntity3> Aggregates => Set<AggregateEntity3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity3>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity3>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_EndOffset_Annotation()
    {
        // Arrange
        using EndOffsetContext3 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity3))!;

        // Assert
        Assert.Equal("30 minutes", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value);
    }

    #endregion

    #region Should_Apply_ScheduleInterval_Annotation

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "30 minutes")]
    private class AggregateEntity4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ScheduleIntervalContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();
        public DbSet<AggregateEntity4> Aggregates => Set<AggregateEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity4>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_ScheduleInterval_Annotation()
    {
        // Arrange
        using ScheduleIntervalContext4 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity4))!;

        // Assert
        Assert.Equal("30 minutes", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region Should_Apply_InitialStart_Annotation

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", InitialStart = "2025-12-15T03:00:00Z")]
    private class AggregateEntity6
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class InitialStartContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();
        public DbSet<AggregateEntity6> Aggregates => Set<AggregateEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity6>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_InitialStart_Annotation()
    {
        // Arrange
        using InitialStartContext6 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity6))!;

        // Assert
        object? initialStartValue = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);

        DateTime initialStart = (DateTime)initialStartValue;
        DateTime utcStart = initialStart.ToUniversalTime();
        Assert.Equal(2025, utcStart.Year);
        Assert.Equal(12, utcStart.Month);
        Assert.Equal(15, utcStart.Day);
        Assert.Equal(3, utcStart.Hour);
    }

    #endregion

    #region Should_Apply_IfNotExists_Annotation

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", IfNotExists = true)]
    private class AggregateEntity7
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IfNotExistsContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();
        public DbSet<AggregateEntity7> Aggregates => Set<AggregateEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity7>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_IfNotExists_Annotation()
    {
        // Arrange
        using IfNotExistsContext7 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity7))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)?.Value);
    }

    #endregion

    #region Should_Apply_BucketsPerBatch_Annotation

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", BucketsPerBatch = 5)]
    private class AggregateEntity9
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketsPerBatchContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<AggregateEntity9> Aggregates => Set<AggregateEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity9>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_BucketsPerBatch_Annotation()
    {
        // Arrange
        using BucketsPerBatchContext9 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity9))!;

        // Assert
        Assert.Equal(5, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value);
    }

    #endregion

    #region Should_Apply_MaxBatchesPerExecution_Annotation

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", MaxBatchesPerExecution = 10)]
    private class AggregateEntity10
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaxBatchesPerExecutionContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();
        public DbSet<AggregateEntity10> Aggregates => Set<AggregateEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity10>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity10>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_MaxBatchesPerExecution_Annotation()
    {
        // Arrange
        using MaxBatchesPerExecutionContext10 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity10))!;

        // Assert
        Assert.Equal(10, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value);
    }

    #endregion

    #region Should_Apply_RefreshNewestFirst_Annotation

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", RefreshNewestFirst = false)]
    private class AggregateEntity11
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RefreshNewestFirstContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<AggregateEntity11> Aggregates => Set<AggregateEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity11>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Apply_RefreshNewestFirst_Annotation()
    {
        // Arrange
        using RefreshNewestFirstContext11 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity11))!;

        // Assert
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value);
    }

    #endregion

    #region Should_Not_Apply_Default_Values

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour")]
    private class AggregateEntity12
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DefaultValuesContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();
        public DbSet<AggregateEntity12> Aggregates => Set<AggregateEntity12>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity12>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity12>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Not_Apply_Default_Values()
    {
        // Arrange
        using DefaultValuesContext12 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity12))!;

        // Assert
        // Default values should not have annotations
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)); // Default is false
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)); // Default is 1
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)); // Default is 0
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)); // Default is true
    }

    #endregion

    #region Should_Match_FluentApi_Configuration

    private class MetricEntity13a
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(
        StartOffset = "7 days",
        EndOffset = "1 hour",
        ScheduleInterval = "1 hour",
        RefreshNewestFirst = true)]
    private class AttributeBasedAggregate13
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricEntity13b
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FluentBasedAggregate13
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AttributeBasedContext13 : DbContext
    {
        public DbSet<MetricEntity13a> Metrics => Set<MetricEntity13a>();
        public DbSet<AttributeBasedAggregate13> Aggregates => Set<AttributeBasedAggregate13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity13a>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AttributeBasedAggregate13>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    private class FluentBasedContext13 : DbContext
    {
        public DbSet<MetricEntity13b> Metrics => Set<MetricEntity13b>();
        public DbSet<FluentBasedAggregate13> Aggregates => Set<FluentBasedAggregate13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity13b>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FluentBasedAggregate13>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<FluentBasedAggregate13, MetricEntity13b>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithRefreshNewestFirst(true);
            });
        }
    }

    [Fact]
    public void Should_Match_FluentApi_Configuration()
    {
        // Arrange
        using AttributeBasedContext13 attributeContext = new();
        using FluentBasedContext13 fluentContext = new();

        // Act
        IModel attributeModel = GetModel(attributeContext);
        IModel fluentModel = GetModel(fluentContext);

        IEntityType attributeEntity = attributeModel.FindEntityType(typeof(AttributeBasedAggregate13))!;
        IEntityType fluentEntity = fluentModel.FindEntityType(typeof(FluentBasedAggregate13))!;

        // Assert
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value
        );
    }

    #endregion

    #region Should_Throw_When_InitialStart_Has_Invalid_Format

    private class MetricEntity15
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour", InitialStart = "not-a-date")]
    private class AggregateEntity15
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class InvalidInitialStartContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();
        public DbSet<AggregateEntity15> Aggregates => Set<AggregateEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity15>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Throw_When_InitialStart_Has_Invalid_Format()
    {
        using InvalidInitialStartContext15 context = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context)
        );

        Assert.Contains("not-a-date", exception.Message);
        Assert.Contains("InitialStart", exception.Message);
    }

    #endregion

    #region Should_Require_ContinuousAggregate_Attribute

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    // Note: Missing [ContinuousAggregate] attribute
    [ContinuousAggregatePolicy(StartOffset = "1 month", EndOffset = "1 hour", ScheduleInterval = "1 hour")]
    private class AggregateEntity14
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RequiresContinuousAggregateContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();
        public DbSet<AggregateEntity14> Aggregates => Set<AggregateEntity14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity14>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Require_ContinuousAggregate_Attribute()
    {
        // Arrange
        using RequiresContinuousAggregateContext14 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateEntity14))!;

        // Assert
        // The policy annotation should be applied even without ContinuousAggregate attribute
        // This is because the convention processes the attribute independently
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);

        // However, the entity won't be recognized as a continuous aggregate without the ContinuousAggregate attribute
        Assert.Null(entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName));
    }

    #endregion
}
