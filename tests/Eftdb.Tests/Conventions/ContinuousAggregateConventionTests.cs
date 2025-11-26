using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify ContinuousAggregateConvention processes [ContinuousAggregate], [TimeBucket], and [Aggregate] attributes correctly
/// and applies the same annotations as the Fluent API.
/// </summary>
public class ContinuousAggregateConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Process_Minimal_ContinuousAggregate_Attributes

    [Hypertable("Timestamp")]
    private class MinimalSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [TimeBucket("1 hour", "Timestamp")]
    private class MinimalContinuousAggregateEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class MinimalAttributeContext : DbContext
    {
        public DbSet<MinimalSourceMetricEntity> Metrics => Set<MinimalSourceMetricEntity>();
        public DbSet<MinimalContinuousAggregateEntity> HourlyMetrics => Set<MinimalContinuousAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<MinimalContinuousAggregateEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_Minimal_ContinuousAggregate_Attributes()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalContinuousAggregateEntity))!;

        Assert.NotNull(entityType);
        Assert.Equal("hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region Should_Configure_Entity_As_View_Not_Table

    [Hypertable("Timestamp")]
    private class ViewSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [TimeBucket("1 hour", "Timestamp")]
    private class ViewContinuousAggregateEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class ViewAttributeContext : DbContext
    {
        public DbSet<ViewSourceMetricEntity> Metrics => Set<ViewSourceMetricEntity>();
        public DbSet<ViewContinuousAggregateEntity> HourlyMetrics => Set<ViewContinuousAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<ViewContinuousAggregateEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Configure_Entity_As_View_Not_Table()
    {
        using ViewAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ViewContinuousAggregateEntity))!;

        Assert.Equal("hourly_metrics", entityType.GetViewName());
        Assert.Null(entityType.GetTableName());
    }

    #endregion

    #region Should_Process_AggregateAttribute_On_Properties

    [Hypertable("Timestamp")]
    private class AggregateSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [TimeBucket("1 hour", "Timestamp")]
    private class AggregateContinuousAggregateEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class AggregateAttributeContext : DbContext
    {
        public DbSet<AggregateSourceMetricEntity> Metrics => Set<AggregateSourceMetricEntity>();
        public DbSet<AggregateContinuousAggregateEntity> HourlyMetrics => Set<AggregateContinuousAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AggregateSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<AggregateContinuousAggregateEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_AggregateAttribute_On_Properties()
    {
        using AggregateAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateContinuousAggregateEntity))!;

        object? aggregateFunctionsValue = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value;
        Assert.NotNull(aggregateFunctionsValue);

        List<string>? aggregateFunctions = aggregateFunctionsValue as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Single(aggregateFunctions);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
    }

    #endregion

    #region Should_Process_Multiple_AggregateAttributes

    [Hypertable("Timestamp")]
    private class MultipleAggregatesSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [TimeBucket("1 hour", "Timestamp")]
    private class MultipleAggregatesEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }

        [Aggregate(EAggregateFunction.Min, "Value")]
        public double MinValue { get; set; }

        [Aggregate(EAggregateFunction.Max, "Value")]
        public double MaxValue { get; set; }
    }

    private class MultipleAggregatesContext : DbContext
    {
        public DbSet<MultipleAggregatesSourceMetricEntity> Metrics => Set<MultipleAggregatesSourceMetricEntity>();
        public DbSet<MultipleAggregatesEntity> HourlyMetrics => Set<MultipleAggregatesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleAggregatesSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<MultipleAggregatesEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_Multiple_AggregateAttributes()
    {
        using MultipleAggregatesContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MultipleAggregatesEntity))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(3, aggregateFunctions.Count);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
        Assert.Contains("MinValue:Min:Value", aggregateFunctions);
        Assert.Contains("MaxValue:Max:Value", aggregateFunctions);
    }

    #endregion

    #region Should_Process_ChunkInterval_Option

    [Hypertable("Timestamp")]
    private class ChunkIntervalSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics", ChunkInterval = "30 days")]
    [TimeBucket("1 hour", "Timestamp")]
    private class ChunkIntervalEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class ChunkIntervalAttributeContext : DbContext
    {
        public DbSet<ChunkIntervalSourceMetricEntity> Metrics => Set<ChunkIntervalSourceMetricEntity>();
        public DbSet<ChunkIntervalEntity> HourlyMetrics => Set<ChunkIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<ChunkIntervalEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_ChunkInterval_Option()
    {
        using ChunkIntervalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ChunkIntervalEntity))!;

        Assert.Equal("30 days", entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value);
    }

    #endregion

    #region Should_Process_WithNoData_Option

    [Hypertable("Timestamp")]
    private class WithNoDataSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics", WithNoData = true)]
    [TimeBucket("1 hour", "Timestamp")]
    private class WithNoDataEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class WithNoDataAttributeContext : DbContext
    {
        public DbSet<WithNoDataSourceMetricEntity> Metrics => Set<WithNoDataSourceMetricEntity>();
        public DbSet<WithNoDataEntity> HourlyMetrics => Set<WithNoDataEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WithNoDataSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<WithNoDataEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_WithNoData_Option()
    {
        using WithNoDataAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(WithNoDataEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value);
    }

    #endregion

    #region Should_Process_CreateGroupIndexes_Option

    [Hypertable("Timestamp")]
    private class CreateGroupIndexesSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics", CreateGroupIndexes = false)]
    [TimeBucket("1 hour", "Timestamp")]
    private class CreateGroupIndexesEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class CreateGroupIndexesAttributeContext : DbContext
    {
        public DbSet<CreateGroupIndexesSourceMetricEntity> Metrics => Set<CreateGroupIndexesSourceMetricEntity>();
        public DbSet<CreateGroupIndexesEntity> HourlyMetrics => Set<CreateGroupIndexesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateGroupIndexesSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<CreateGroupIndexesEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_CreateGroupIndexes_Option()
    {
        using CreateGroupIndexesAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CreateGroupIndexesEntity))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value);
    }

    #endregion

    #region Should_Process_MaterializedOnly_Option

    [Hypertable("Timestamp")]
    private class MaterializedOnlySourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics", MaterializedOnly = true)]
    [TimeBucket("1 hour", "Timestamp")]
    private class MaterializedOnlyEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyAttributeContext : DbContext
    {
        public DbSet<MaterializedOnlySourceMetricEntity> Metrics => Set<MaterializedOnlySourceMetricEntity>();
        public DbSet<MaterializedOnlyEntity> HourlyMetrics => Set<MaterializedOnlyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnlySourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<MaterializedOnlyEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_MaterializedOnly_Option()
    {
        using MaterializedOnlyAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaterializedOnlyEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value);
    }

    #endregion

    #region Should_Process_WhereClause_Option

    [Hypertable("Timestamp")]
    private class WhereClauseSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics", Where = "DeviceId > 100")]
    [TimeBucket("1 hour", "Timestamp")]
    private class WhereClauseEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class WhereClauseAttributeContext : DbContext
    {
        public DbSet<WhereClauseSourceMetricEntity> Metrics => Set<WhereClauseSourceMetricEntity>();
        public DbSet<WhereClauseEntity> HourlyMetrics => Set<WhereClauseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WhereClauseSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<WhereClauseEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_WhereClause_Option()
    {
        using WhereClauseAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(WhereClauseEntity))!;

        Assert.Equal("DeviceId > 100", entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value);
    }

    #endregion

    #region Should_Process_TimeBucketAttribute_GroupBy

    [Hypertable("Timestamp")]
    private class TimeBucketGroupBySourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "Metrics")]
    [TimeBucket("1 hour", "Timestamp", GroupBy = false)]
    private class TimeBucketGroupByEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class TimeBucketGroupByContext : DbContext
    {
        public DbSet<TimeBucketGroupBySourceMetricEntity> Metrics => Set<TimeBucketGroupBySourceMetricEntity>();
        public DbSet<TimeBucketGroupByEntity> HourlyMetrics => Set<TimeBucketGroupByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeBucketGroupBySourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<TimeBucketGroupByEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_TimeBucketAttribute_GroupBy()
    {
        using TimeBucketGroupByContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(TimeBucketGroupByEntity))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy)?.Value);
    }

    #endregion

    #region Should_Process_Fully_Configured_ContinuousAggregate

    [Hypertable("Timestamp")]
    private class FullyConfiguredSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(
        MaterializedViewName = "hourly_metrics",
        ParentName = "Metrics",
        ChunkInterval = "30 days",
        WithNoData = true,
        CreateGroupIndexes = false,
        MaterializedOnly = true,
        Where = "DeviceId > 100")]
    [TimeBucket("1 hour", "Timestamp")]
    private class FullyConfiguredEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }

        [Aggregate(EAggregateFunction.Max, "Value")]
        public double MaxValue { get; set; }
    }

    private class FullyConfiguredAttributeContext : DbContext
    {
        public DbSet<FullyConfiguredSourceMetricEntity> Metrics => Set<FullyConfiguredSourceMetricEntity>();
        public DbSet<FullyConfiguredEntity> HourlyMetrics => Set<FullyConfiguredEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<FullyConfiguredEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Process_Fully_Configured_ContinuousAggregate()
    {
        using FullyConfiguredAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal("hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value);
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value);
        Assert.Equal("DeviceId > 100", entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value);

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(2, aggregateFunctions.Count);
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

        Assert.Null(entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName));
        Assert.Null(entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName));
        Assert.Null(entityType.GetViewName());
    }

    #endregion

    #region Attribute_Should_Produce_Same_Annotations_As_FluentAPI

    [Hypertable("Timestamp")]
    private class EquivalenceSourceMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = "EquivalenceSourceMetricEntity", ChunkInterval = "30 days")]
    [TimeBucket("1 hour", "Timestamp")]
    private class EquivalenceAttributeEntity
    {
        public DateTime TimeBucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, "Value")]
        public double AvgValue { get; set; }
    }

    private class EquivalenceFluentEntity
    {
        public DateTime Timestamp { get; set; }
        public double AvgValue { get; set; }
    }

    private class AttributeBasedContext : DbContext
    {
        public DbSet<EquivalenceSourceMetricEntity> Metrics => Set<EquivalenceSourceMetricEntity>();
        public DbSet<EquivalenceAttributeEntity> HourlyMetrics => Set<EquivalenceAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<EquivalenceAttributeEntity>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    private class FluentApiBasedContext : DbContext
    {
        public DbSet<EquivalenceSourceMetricEntity> Metrics => Set<EquivalenceSourceMetricEntity>();
        public DbSet<EquivalenceFluentEntity> HourlyMetrics => Set<EquivalenceFluentEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceSourceMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });

            modelBuilder.Entity<EquivalenceFluentEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<EquivalenceFluentEntity, EquivalenceSourceMetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "30 days")
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
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
            attributeEntity.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value,
            fluentEntity.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value
        );

        List<string>? attributeAggregates = attributeEntity.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        List<string>? fluentAggregates = fluentEntity.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;

        Assert.NotNull(attributeAggregates);
        Assert.NotNull(fluentAggregates);
        Assert.Equal(attributeAggregates.Count, fluentAggregates.Count);
        Assert.All(attributeAggregates, agg => Assert.Contains(agg, fluentAggregates));
    }

    #endregion
}
