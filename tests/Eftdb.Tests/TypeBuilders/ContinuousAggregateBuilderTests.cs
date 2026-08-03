using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify ContinuousAggregateBuilder Fluent API methods correctly apply annotations.
/// </summary>
public class ContinuousAggregateBuilderTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region IsContinuousAggregate_Should_Set_MaterializedViewName

    private class IsContinuousAggregate_Should_Set_MaterializedViewName_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_MaterializedViewName_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_MaterializedViewName_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_MaterializedViewName_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_MaterializedViewName_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_MaterializedViewName_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_MaterializedViewName()
    {
        using IsContinuousAggregate_Should_Set_MaterializedViewName_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_MaterializedViewName_HourlyMetricAggregate))!;

        Assert.Equal("hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type

    private class IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type()
    {
        using IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_HourlyMetricAggregate))!;

        Assert.Equal("IsContinuousAggregate_Should_Set_ParentName_From_SourceEntity_Type_MetricEntity", entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Configure_Entity_As_View

    private class IsContinuousAggregate_Should_Configure_Entity_As_View_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Configure_Entity_As_View_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Configure_Entity_As_View_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Configure_Entity_As_View_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Configure_Entity_As_View_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate, IsContinuousAggregate_Should_Configure_Entity_As_View_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Configure_Entity_As_View()
    {
        using IsContinuousAggregate_Should_Configure_Entity_As_View_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Configure_Entity_As_View_HourlyMetricAggregate))!;

        Assert.Equal("hourly_metrics", entityType.GetViewName());
        Assert.Null(entityType.GetTableName());
    }

    #endregion

    #region IsContinuousAggregate_Should_Set_TimeBucketWidth

    private class IsContinuousAggregate_Should_Set_TimeBucketWidth_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketWidth_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketWidth_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_TimeBucketWidth_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketWidth_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_TimeBucketWidth_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_TimeBucketWidth()
    {
        using IsContinuousAggregate_Should_Set_TimeBucketWidth_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_TimeBucketWidth_HourlyMetricAggregate))!;

        Assert.Equal("1 hour", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression

    private class IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression()
    {
        using IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_TimeBucketSourceColumn_From_Expression_HourlyMetricAggregate))!;

        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True

    private class IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True()
    {
        using IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_TimeBucketGroupBy_Default_True_HourlyMetricAggregate))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False

    private class IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate, IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    timeBucketGroupBy: false)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False()
    {
        using IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Support_TimeBucketGroupBy_False_HourlyMetricAggregate))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided

    private class IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate, IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "30 days")
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided()
    {
        using IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Set_ChunkInterval_When_Provided_HourlyMetricAggregate))!;

        Assert.Equal("30 days", entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null

    private class IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate, IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null()
    {
        using IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Not_Set_ChunkInterval_When_Null_HourlyMetricAggregate))!;

        Assert.Null(entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval));
    }

    #endregion

    #region AddAggregateFunction_Should_Add_Single_Aggregate

    private class AddAggregateFunction_Should_Add_Single_Aggregate_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddAggregateFunction_Should_Add_Single_Aggregate_Context : DbContext
    {
        public DbSet<AddAggregateFunction_Should_Add_Single_Aggregate_MetricEntity> Metrics => Set<AddAggregateFunction_Should_Add_Single_Aggregate_MetricEntity>();
        public DbSet<AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate> HourlyMetrics => Set<AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddAggregateFunction_Should_Add_Single_Aggregate_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate, AddAggregateFunction_Should_Add_Single_Aggregate_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void AddAggregateFunction_Should_Add_Single_Aggregate()
    {
        using AddAggregateFunction_Should_Add_Single_Aggregate_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddAggregateFunction_Should_Add_Single_Aggregate_HourlyMetricAggregate))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Single(aggregateFunctions);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
    }

    #endregion

    #region AddAggregateFunction_Should_Support_Multiple_Aggregates

    private class AddAggregateFunction_Should_Support_Multiple_Aggregates_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double SumValue { get; set; }
    }

    private class AddAggregateFunction_Should_Support_Multiple_Aggregates_Context : DbContext
    {
        public DbSet<AddAggregateFunction_Should_Support_Multiple_Aggregates_MetricEntity> Metrics => Set<AddAggregateFunction_Should_Support_Multiple_Aggregates_MetricEntity>();
        public DbSet<AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity> HourlyMetrics => Set<AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddAggregateFunction_Should_Support_Multiple_Aggregates_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity, AddAggregateFunction_Should_Support_Multiple_Aggregates_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.MinValue, x => x.Value, EAggregateFunction.Min)
                .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max)
                .AddAggregateFunction(x => x.SumValue, x => x.Value, EAggregateFunction.Sum);
            });
        }
    }

    [Fact]
    public void AddAggregateFunction_Should_Support_Multiple_Aggregates()
    {
        using AddAggregateFunction_Should_Support_Multiple_Aggregates_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddAggregateFunction_Should_Support_Multiple_Aggregates_MultipleAggregatesEntity))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(4, aggregateFunctions.Count);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
        Assert.Contains("MinValue:Min:Value", aggregateFunctions);
        Assert.Contains("MaxValue:Max:Value", aggregateFunctions);
        Assert.Contains("SumValue:Sum:Value", aggregateFunctions);
    }

    #endregion

    #region AddAggregateFunction_Should_Prevent_Duplicate_Property

    private class AddAggregateFunction_Should_Prevent_Duplicate_Property_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddAggregateFunction_Should_Prevent_Duplicate_Property_Context : DbContext
    {
        public DbSet<AddAggregateFunction_Should_Prevent_Duplicate_Property_MetricEntity> Metrics => Set<AddAggregateFunction_Should_Prevent_Duplicate_Property_MetricEntity>();
        public DbSet<AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate> HourlyMetrics => Set<AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddAggregateFunction_Should_Prevent_Duplicate_Property_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate, AddAggregateFunction_Should_Prevent_Duplicate_Property_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void AddAggregateFunction_Should_Prevent_Duplicate_Property()
    {
        using AddAggregateFunction_Should_Prevent_Duplicate_Property_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddAggregateFunction_Should_Prevent_Duplicate_Property_HourlyMetricAggregate))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Single(aggregateFunctions);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
    }

    #endregion

    #region AddAggregateFunction_Should_Support_All_Aggregate_Types

    private class AddAggregateFunction_Should_Support_All_Aggregate_Types_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double SumValue { get; set; }
        public int CountValue { get; set; }
        public double FirstValue { get; set; }
        public double LastValue { get; set; }
    }

    private class AddAggregateFunction_Should_Support_All_Aggregate_Types_Context : DbContext
    {
        public DbSet<AddAggregateFunction_Should_Support_All_Aggregate_Types_MetricEntity> Metrics => Set<AddAggregateFunction_Should_Support_All_Aggregate_Types_MetricEntity>();
        public DbSet<AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity> HourlyMetrics => Set<AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddAggregateFunction_Should_Support_All_Aggregate_Types_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity, AddAggregateFunction_Should_Support_All_Aggregate_Types_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.MinValue, x => x.Value, EAggregateFunction.Min)
                .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max)
                .AddAggregateFunction(x => x.SumValue, x => x.Value, EAggregateFunction.Sum)
                .AddAggregateFunction(x => x.CountValue, x => x.Value, EAggregateFunction.Count)
                .AddAggregateFunction(x => x.FirstValue, x => x.Value, EAggregateFunction.First)
                .AddAggregateFunction(x => x.LastValue, x => x.Value, EAggregateFunction.Last);
            });
        }
    }

    [Fact]
    public void AddAggregateFunction_Should_Support_All_Aggregate_Types()
    {
        using AddAggregateFunction_Should_Support_All_Aggregate_Types_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddAggregateFunction_Should_Support_All_Aggregate_Types_AllAggregatesEntity))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(7, aggregateFunctions.Count);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
        Assert.Contains("MinValue:Min:Value", aggregateFunctions);
        Assert.Contains("MaxValue:Max:Value", aggregateFunctions);
        Assert.Contains("SumValue:Sum:Value", aggregateFunctions);
        Assert.Contains("CountValue:Count:Value", aggregateFunctions);
        Assert.Contains("FirstValue:First:Value", aggregateFunctions);
        Assert.Contains("LastValue:Last:Value", aggregateFunctions);
    }

    #endregion

    #region AddAggregateFunction_Should_Support_Mismatched_Property_And_SourceColumn_Types

    private class CountMismatch_TradeEntity
    {
        public DateTime Timestamp { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class CountMismatch_TradeAggregate
    {
        public DateTime TimeBucket { get; set; }
        public int TradeCount { get; set; }
        public long TickerCount { get; set; }
    }

    private class CountMismatch_Context : DbContext
    {
        public DbSet<CountMismatch_TradeEntity> Trades => Set<CountMismatch_TradeEntity>();
        public DbSet<CountMismatch_TradeAggregate> HourlyTrades => Set<CountMismatch_TradeAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CountMismatch_TradeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Trades");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CountMismatch_TradeAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CountMismatch_TradeAggregate, CountMismatch_TradeEntity>(
                    "hourly_trades",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.TradeCount, x => x.Timestamp, EAggregateFunction.Count)
                .AddAggregateFunction(x => x.TickerCount, x => x.Ticker, EAggregateFunction.Count);
            });
        }
    }

    [Fact]
    public void AddAggregateFunction_Should_Support_Mismatched_Property_And_SourceColumn_Types()
    {
        using CountMismatch_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CountMismatch_TradeAggregate))!;

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(2, aggregateFunctions.Count);
        Assert.Contains("TradeCount:Count:Timestamp", aggregateFunctions);
        Assert.Contains("TickerCount:Count:Ticker", aggregateFunctions);
    }

    #endregion

    #region AddGroupByColumn_Should_Add_Single_Column_From_Expression

    private class AddGroupByColumn_Should_Add_Single_Column_From_Expression_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public int DeviceId { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddGroupByColumn_Should_Add_Single_Column_From_Expression_Context : DbContext
    {
        public DbSet<AddGroupByColumn_Should_Add_Single_Column_From_Expression_MetricEntity> Metrics => Set<AddGroupByColumn_Should_Add_Single_Column_From_Expression_MetricEntity>();
        public DbSet<AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate> HourlyMetrics => Set<AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddGroupByColumn_Should_Add_Single_Column_From_Expression_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate, AddGroupByColumn_Should_Add_Single_Column_From_Expression_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void AddGroupByColumn_Should_Add_Single_Column_From_Expression()
    {
        using AddGroupByColumn_Should_Add_Single_Column_From_Expression_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddGroupByColumn_Should_Add_Single_Column_From_Expression_GroupedMetricAggregate))!;

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
        Assert.Contains("DeviceId", groupByColumns);
    }

    #endregion

    #region AddGroupByColumn_Should_Add_Single_Column_From_RawSQL

    private class AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_Context : DbContext
    {
        public DbSet<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_MetricEntity> Metrics => Set<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_MetricEntity>();
        public DbSet<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate> HourlyMetrics => Set<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate, AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn("device_id % 10");
            });
        }
    }

    [Fact]
    public void AddGroupByColumn_Should_Add_Single_Column_From_RawSQL()
    {
        using AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddGroupByColumn_Should_Add_Single_Column_From_RawSQL_HourlyMetricAggregate))!;

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
        Assert.Contains("device_id % 10", groupByColumns);
    }

    #endregion

    #region AddGroupByColumn_Should_Support_Multiple_Columns

    private class AddGroupByColumn_Should_Support_Multiple_Columns_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
        public string? Location { get; set; }
    }

    private class AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity
    {
        public DateTime TimeBucket { get; set; }
        public int DeviceId { get; set; }
        public string? Location { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddGroupByColumn_Should_Support_Multiple_Columns_Context : DbContext
    {
        public DbSet<AddGroupByColumn_Should_Support_Multiple_Columns_MetricEntity> Metrics => Set<AddGroupByColumn_Should_Support_Multiple_Columns_MetricEntity>();
        public DbSet<AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity> HourlyMetrics => Set<AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddGroupByColumn_Should_Support_Multiple_Columns_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity, AddGroupByColumn_Should_Support_Multiple_Columns_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn(x => x.DeviceId)
                .AddGroupByColumn(x => x.Location);
            });
        }
    }

    [Fact]
    public void AddGroupByColumn_Should_Support_Multiple_Columns()
    {
        using AddGroupByColumn_Should_Support_Multiple_Columns_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddGroupByColumn_Should_Support_Multiple_Columns_MultiGroupByEntity))!;

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Equal(2, groupByColumns.Count);
        Assert.Contains("DeviceId", groupByColumns);
        Assert.Contains("Location", groupByColumns);
    }

    #endregion

    #region AddGroupByColumn_Should_Prevent_Duplicate_Columns

    private class AddGroupByColumn_Should_Prevent_Duplicate_Columns_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public int DeviceId { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddGroupByColumn_Should_Prevent_Duplicate_Columns_Context : DbContext
    {
        public DbSet<AddGroupByColumn_Should_Prevent_Duplicate_Columns_MetricEntity> Metrics => Set<AddGroupByColumn_Should_Prevent_Duplicate_Columns_MetricEntity>();
        public DbSet<AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate> HourlyMetrics => Set<AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddGroupByColumn_Should_Prevent_Duplicate_Columns_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate, AddGroupByColumn_Should_Prevent_Duplicate_Columns_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn(x => x.DeviceId)
                .AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void AddGroupByColumn_Should_Prevent_Duplicate_Columns()
    {
        using AddGroupByColumn_Should_Prevent_Duplicate_Columns_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddGroupByColumn_Should_Prevent_Duplicate_Columns_GroupedMetricAggregate))!;

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
        Assert.Contains("DeviceId", groupByColumns);
    }

    #endregion

    #region AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns

    private class AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_Context : DbContext
    {
        public DbSet<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_MetricEntity> Metrics => Set<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_MetricEntity>();
        public DbSet<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate> HourlyMetrics => Set<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate, AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn("device_id % 10")
                .AddGroupByColumn("device_id % 10");
            });
        }
    }

    [Fact]
    public void AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns()
    {
        using AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AddGroupByColumn_Should_Prevent_Duplicate_RawSQL_Columns_HourlyMetricAggregate))!;

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
        Assert.Contains("device_id % 10", groupByColumns);
    }

    #endregion

    #region WithNoData_Should_Set_WithNoData_True_By_Default

    private class WithNoData_Should_Set_WithNoData_True_By_Default_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithNoData_Should_Set_WithNoData_True_By_Default_Context : DbContext
    {
        public DbSet<WithNoData_Should_Set_WithNoData_True_By_Default_MetricEntity> Metrics => Set<WithNoData_Should_Set_WithNoData_True_By_Default_MetricEntity>();
        public DbSet<WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate> HourlyMetrics => Set<WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WithNoData_Should_Set_WithNoData_True_By_Default_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate, WithNoData_Should_Set_WithNoData_True_By_Default_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .WithNoData();
            });
        }
    }

    [Fact]
    public void WithNoData_Should_Set_WithNoData_True_By_Default()
    {
        using WithNoData_Should_Set_WithNoData_True_By_Default_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(WithNoData_Should_Set_WithNoData_True_By_Default_HourlyMetricAggregate))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value);
    }

    #endregion

    #region WithNoData_Should_Support_Explicit_False

    private class WithNoData_Should_Support_Explicit_False_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithNoData_Should_Support_Explicit_False_Context : DbContext
    {
        public DbSet<WithNoData_Should_Support_Explicit_False_MetricEntity> Metrics => Set<WithNoData_Should_Support_Explicit_False_MetricEntity>();
        public DbSet<WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate> HourlyMetrics => Set<WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WithNoData_Should_Support_Explicit_False_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate, WithNoData_Should_Support_Explicit_False_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .WithNoData(false);
            });
        }
    }

    [Fact]
    public void WithNoData_Should_Support_Explicit_False()
    {
        using WithNoData_Should_Support_Explicit_False_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(WithNoData_Should_Support_Explicit_False_HourlyMetricAggregate))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value);
    }

    #endregion

    #region CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default

    private class CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_Context : DbContext
    {
        public DbSet<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_MetricEntity> Metrics => Set<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_MetricEntity>();
        public DbSet<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate> HourlyMetrics => Set<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate, CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .CreateGroupIndexes();
            });
        }
    }

    [Fact]
    public void CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default()
    {
        using CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CreateGroupIndexes_Should_Set_CreateGroupIndexes_True_By_Default_HourlyMetricAggregate))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value);
    }

    #endregion

    #region CreateGroupIndexes_Should_Support_Explicit_False

    private class CreateGroupIndexes_Should_Support_Explicit_False_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CreateGroupIndexes_Should_Support_Explicit_False_Context : DbContext
    {
        public DbSet<CreateGroupIndexes_Should_Support_Explicit_False_MetricEntity> Metrics => Set<CreateGroupIndexes_Should_Support_Explicit_False_MetricEntity>();
        public DbSet<CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate> HourlyMetrics => Set<CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateGroupIndexes_Should_Support_Explicit_False_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate, CreateGroupIndexes_Should_Support_Explicit_False_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .CreateGroupIndexes(false);
            });
        }
    }

    [Fact]
    public void CreateGroupIndexes_Should_Support_Explicit_False()
    {
        using CreateGroupIndexes_Should_Support_Explicit_False_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CreateGroupIndexes_Should_Support_Explicit_False_HourlyMetricAggregate))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value);
    }

    #endregion

    #region MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default

    private class MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_Context : DbContext
    {
        public DbSet<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_MetricEntity> Metrics => Set<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_MetricEntity>();
        public DbSet<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate> HourlyMetrics => Set<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate, MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .MaterializedOnly();
            });
        }
    }

    [Fact]
    public void MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default()
    {
        using MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaterializedOnly_Should_Set_MaterializedOnly_True_By_Default_HourlyMetricAggregate))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value);
    }

    #endregion

    #region MaterializedOnly_Should_Support_Explicit_False

    private class MaterializedOnly_Should_Support_Explicit_False_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnly_Should_Support_Explicit_False_Context : DbContext
    {
        public DbSet<MaterializedOnly_Should_Support_Explicit_False_MetricEntity> Metrics => Set<MaterializedOnly_Should_Support_Explicit_False_MetricEntity>();
        public DbSet<MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate> HourlyMetrics => Set<MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnly_Should_Support_Explicit_False_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate, MaterializedOnly_Should_Support_Explicit_False_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .MaterializedOnly(false);
            });
        }
    }

    [Fact]
    public void MaterializedOnly_Should_Support_Explicit_False()
    {
        using MaterializedOnly_Should_Support_Explicit_False_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaterializedOnly_Should_Support_Explicit_False_HourlyMetricAggregate))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value);
    }

    #endregion

    #region Where_Should_Set_WhereClause

    private class Where_Should_Set_WhereClause_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class Where_Should_Set_WhereClause_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class Where_Should_Set_WhereClause_Context : DbContext
    {
        public DbSet<Where_Should_Set_WhereClause_MetricEntity> Metrics => Set<Where_Should_Set_WhereClause_MetricEntity>();
        public DbSet<Where_Should_Set_WhereClause_HourlyMetricAggregate> HourlyMetrics => Set<Where_Should_Set_WhereClause_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Where_Should_Set_WhereClause_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<Where_Should_Set_WhereClause_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<Where_Should_Set_WhereClause_HourlyMetricAggregate, Where_Should_Set_WhereClause_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .Where("device_id > 100");
            });
        }
    }

    [Fact]
    public void Where_Should_Set_WhereClause()
    {
        using Where_Should_Set_WhereClause_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(Where_Should_Set_WhereClause_HourlyMetricAggregate))!;

        Assert.Equal("device_id > 100", entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value);
    }

    #endregion

    #region FluentAPI_Should_Support_Full_Method_Chaining

    private class FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate
    {
        public DateTime TimeBucket { get; set; }
        public int DeviceId { get; set; }
        public double AvgValue { get; set; }
        public double MaxValue { get; set; }
    }

    private class FluentAPI_Should_Support_Full_Method_Chaining_Context : DbContext
    {
        public DbSet<FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity> Metrics => Set<FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity>();
        public DbSet<FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate> HourlyMetrics => Set<FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate, FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "30 days")
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max)
                .AddGroupByColumn(x => x.DeviceId)
                .WithNoData()
                .CreateGroupIndexes(false)
                .MaterializedOnly()
                .Where("device_id > 100");
            });
        }
    }

    [Fact]
    public void FluentAPI_Should_Support_Full_Method_Chaining()
    {
        using FluentAPI_Should_Support_Full_Method_Chaining_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FluentAPI_Should_Support_Full_Method_Chaining_FullyConfiguredAggregate))!;

        Assert.Equal("hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("FluentAPI_Should_Support_Full_Method_Chaining_MetricEntity", entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value);
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value);
        Assert.Equal("device_id > 100", entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value);

        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;
        Assert.NotNull(aggregateFunctions);
        Assert.Equal(2, aggregateFunctions.Count);

        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn

    private class IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_MetricEntity
    {
        public DateTimeOffset Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate
    {
        public DateTimeOffset TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("dto_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate, IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_MetricEntity>(
                    "dto_hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn()
    {
        using IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_DateTimeOffset_TimeColumn_HourlyMetricAggregate))!;

        Assert.Equal("dto_hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn

    private class IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_MetricEntity
    {
        public DateOnly Day { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate
    {
        public DateOnly TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate> DailyMetrics => Set<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("dateonly_metrics");
                entity.IsHypertable(x => x.Day);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate, IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_MetricEntity>(
                    "dateonly_daily_metrics",
                    "1 day",
                    x => x.Day)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn()
    {
        using IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_DateOnly_TimeColumn_DailyMetricAggregate))!;

        Assert.Equal("dateonly_daily_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Day", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_Long_TimeColumn

    private class IsContinuousAggregate_Should_Accept_Long_TimeColumn_MetricEntity
    {
        public long EpochMicros { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate
    {
        public long TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Long_TimeColumn_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_Long_TimeColumn_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_Long_TimeColumn_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate> Buckets => Set<IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Long_TimeColumn_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("long_metrics");
                entity.IsHypertable(x => x.EpochMicros);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate, IsContinuousAggregate_Should_Accept_Long_TimeColumn_MetricEntity>(
                    "long_bucketed_metrics",
                    "3600000000",
                    x => x.EpochMicros)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_Long_TimeColumn()
    {
        using IsContinuousAggregate_Should_Accept_Long_TimeColumn_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_Long_TimeColumn_BucketedMetricAggregate))!;

        Assert.Equal("long_bucketed_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("EpochMicros", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_Int_TimeColumn

    private class IsContinuousAggregate_Should_Accept_Int_TimeColumn_MetricEntity
    {
        public int Ticks { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate
    {
        public int TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Int_TimeColumn_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_Int_TimeColumn_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_Int_TimeColumn_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate> Buckets => Set<IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Int_TimeColumn_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("int_metrics");
                entity.IsHypertable(x => x.Ticks);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate, IsContinuousAggregate_Should_Accept_Int_TimeColumn_MetricEntity>(
                    "int_bucketed_metrics",
                    "3600",
                    x => x.Ticks)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_Int_TimeColumn()
    {
        using IsContinuousAggregate_Should_Accept_Int_TimeColumn_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_Int_TimeColumn_BucketedMetricAggregate))!;

        Assert.Equal("int_bucketed_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Ticks", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_Short_TimeColumn

    private class IsContinuousAggregate_Should_Accept_Short_TimeColumn_MetricEntity
    {
        public short SlotIndex { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate
    {
        public short TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Short_TimeColumn_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_Short_TimeColumn_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_Short_TimeColumn_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate> Buckets => Set<IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Short_TimeColumn_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("short_metrics");
                entity.IsHypertable(x => x.SlotIndex);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate, IsContinuousAggregate_Should_Accept_Short_TimeColumn_MetricEntity>(
                    "short_bucketed_metrics",
                    "10",
                    x => x.SlotIndex)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_Short_TimeColumn()
    {
        using IsContinuousAggregate_Should_Accept_Short_TimeColumn_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_Short_TimeColumn_BucketedMetricAggregate))!;

        Assert.Equal("short_bucketed_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("SlotIndex", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type

    private readonly struct IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_CustomInstant(DateTime utcDateTime)
    {
        public DateTime UtcDateTime { get; } = utcDateTime;
    }

    private class IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_MetricEntity
    {
        public IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_CustomInstant Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_Context : DbContext
    {
        public DbSet<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_MetricEntity> Metrics => Set<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_MetricEntity>();
        public DbSet<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate> HourlyMetrics => Set<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("custom_cagg_metrics");
                entity.Property(x => x.Timestamp)
                      .HasConversion(v => v.UtcDateTime, v => new IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_CustomInstant(v));
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate, IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_MetricEntity, IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_CustomInstant>(
                    "custom_cagg_hourly_metrics",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type()
    {
        using IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(IsContinuousAggregate_Should_Accept_Custom_TimeColumn_Type_HourlyMetricAggregate))!;

        Assert.Equal("custom_cagg_hourly_metrics", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value);
    }

    #endregion

    #region GetPropertyName_Should_Handle_UnaryExpression_Cast

    private class CastExpressionMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public int Value { get; set; }
    }

    private class CastExpressionAggEntity
    {
        public DateTime TimeBucket { get; set; }
        public long LongValue { get; set; }
    }

    private class CastExpressionContext : DbContext
    {
        public DbSet<CastExpressionMetricEntity> Metrics => Set<CastExpressionMetricEntity>();
        public DbSet<CastExpressionAggEntity> Aggregates => Set<CastExpressionAggEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CastExpressionMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cast_expr_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CastExpressionAggEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CastExpressionAggEntity, CastExpressionMetricEntity>(
                    "cast_expr_hourly", "1 hour", x => x.Timestamp)
                .AddAggregateFunction(x => x.LongValue, x => (object)x.Value, EAggregateFunction.Sum);
            });
        }
    }

    [Fact]
    public void GetPropertyName_Should_Handle_UnaryExpression_Cast()
    {
        // Arrange
        using CastExpressionContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CastExpressionAggEntity))!;

        // Act
        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;

        // Assert
        Assert.NotNull(aggregateFunctions);
        Assert.Single(aggregateFunctions);
        Assert.Contains("LongValue:Sum:Value", aggregateFunctions);
    }

    #endregion

    #region GetPropertyName_Should_Throw_For_Non_Member_Expression

    private class ThrowNonMemberMetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ThrowNonMemberAggEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ThrowNonMemberContext : DbContext
    {
        public DbSet<ThrowNonMemberMetricEntity> Metrics => Set<ThrowNonMemberMetricEntity>();
        public DbSet<ThrowNonMemberAggEntity> Aggregates => Set<ThrowNonMemberAggEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ThrowNonMemberMetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("throw_non_member_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ThrowNonMemberAggEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ThrowNonMemberAggEntity, ThrowNonMemberMetricEntity>(
                    "throw_non_member_hourly", "1 hour", x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => (double)(object)42, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void GetPropertyName_Should_Throw_For_Non_Member_Expression()
    {
        // Arrange & Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            using ThrowNonMemberContext context = new();
            _ = GetModel(context);
        });

        // Assert
        Assert.Contains("simple property access expression", exception.Message);
    }

    #endregion

    #region StringBuilder_WithChunkInterval_Should_Set_ChunkInterval_Annotation

    private class StringBuilder_WithChunkInterval_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilder_WithChunkInterval_AggEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilder_WithChunkInterval_Context : DbContext
    {
        public DbSet<StringBuilder_WithChunkInterval_MetricEntity> Metrics => Set<StringBuilder_WithChunkInterval_MetricEntity>();
        public DbSet<StringBuilder_WithChunkInterval_AggEntity> HourlyMetrics => Set<StringBuilder_WithChunkInterval_AggEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilder_WithChunkInterval_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_chunk_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<StringBuilder_WithChunkInterval_AggEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_chunk_hourly", "StringBuilder_WithChunkInterval_MetricEntity", "1 hour", "Timestamp")
                    .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                    .WithChunkInterval("30 days");
            });
        }
    }

    [Fact]
    public void StringBuilder_WithChunkInterval_Should_Set_ChunkInterval_Annotation()
    {
        // Arrange
        using StringBuilder_WithChunkInterval_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilder_WithChunkInterval_AggEntity))!;

        // Act
        object? annotationValue = entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value;

        // Assert
        Assert.Equal("30 days", annotationValue);
    }

    #endregion

    #region StringBuilder_WithNoData_Should_Set_WithNoData_Annotation

    private class StringBuilder_WithNoData_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilder_WithNoData_AggEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilder_WithNoData_Context : DbContext
    {
        public DbSet<StringBuilder_WithNoData_MetricEntity> Metrics => Set<StringBuilder_WithNoData_MetricEntity>();
        public DbSet<StringBuilder_WithNoData_AggEntity> HourlyMetrics => Set<StringBuilder_WithNoData_AggEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilder_WithNoData_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_nodata_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<StringBuilder_WithNoData_AggEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_nodata_hourly", "StringBuilder_WithNoData_MetricEntity", "1 hour", "Timestamp")
                    .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                    .WithNoData(true);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithNoData_Should_Set_WithNoData_Annotation()
    {
        // Arrange
        using StringBuilder_WithNoData_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilder_WithNoData_AggEntity))!;

        // Act
        object? annotationValue = entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value;

        // Assert
        Assert.Equal(true, annotationValue);
    }

    #endregion

    #region Should_Handle_Cast_Lambda_In_AddAggregateFunction

    private class CastLambdaAggFunction_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CastLambdaAggFunction_HourlyAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CastLambdaAggFunction_Context : DbContext
    {
        public DbSet<CastLambdaAggFunction_MetricEntity> Metrics => Set<CastLambdaAggFunction_MetricEntity>();
        public DbSet<CastLambdaAggFunction_HourlyAggregate> HourlyMetrics => Set<CastLambdaAggFunction_HourlyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CastLambdaAggFunction_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cast_lambda_agg_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CastLambdaAggFunction_HourlyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CastLambdaAggFunction_HourlyAggregate, CastLambdaAggFunction_MetricEntity>(
                    "cast_lambda_agg_hourly", "1 hour", x => x.Timestamp)
                .AddAggregateFunction(x => (object)x.AvgValue, x => (object)x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Handle_Cast_Lambda_In_AddAggregateFunction()
    {
        // Arrange
        using CastLambdaAggFunction_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CastLambdaAggFunction_HourlyAggregate))!;

        // Act
        List<string>? aggregateFunctions = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>;

        // Assert
        Assert.NotNull(aggregateFunctions);
        Assert.Single(aggregateFunctions);
        Assert.Contains("AvgValue:Avg:Value", aggregateFunctions);
    }

    #endregion

    #region Should_Handle_Cast_Lambda_In_AddGroupByColumn

    private class CastLambdaGroupBy_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class CastLambdaGroupBy_GroupedAggregate
    {
        public DateTime TimeBucket { get; set; }
        public int DeviceId { get; set; }
        public double AvgValue { get; set; }
    }

    private class CastLambdaGroupBy_Context : DbContext
    {
        public DbSet<CastLambdaGroupBy_MetricEntity> Metrics => Set<CastLambdaGroupBy_MetricEntity>();
        public DbSet<CastLambdaGroupBy_GroupedAggregate> HourlyMetrics => Set<CastLambdaGroupBy_GroupedAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CastLambdaGroupBy_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cast_lambda_gb_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CastLambdaGroupBy_GroupedAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CastLambdaGroupBy_GroupedAggregate, CastLambdaGroupBy_MetricEntity>(
                    "cast_lambda_gb_hourly", "1 hour", x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                .AddGroupByColumn(x => (object)x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Handle_Cast_Lambda_In_AddGroupByColumn()
    {
        // Arrange
        using CastLambdaGroupBy_Context context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CastLambdaGroupBy_GroupedAggregate))!;

        // Act
        List<string>? groupByColumns = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>;

        // Assert
        Assert.NotNull(groupByColumns);
        Assert.Single(groupByColumns);
        Assert.Contains("DeviceId", groupByColumns);
    }

    #endregion

    #region GenericBuilder_And_StringBuilder_Should_Produce_Identical_Annotations

    private class BuilderParity_MetricEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Region { get; set; } = null!;
    }

    private class BuilderParity_GenericAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BuilderParity_StringAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BuilderParity_Context : DbContext
    {
        public DbSet<BuilderParity_MetricEntity> Metrics => Set<BuilderParity_MetricEntity>();
        public DbSet<BuilderParity_GenericAggregate> GenericAggregates => Set<BuilderParity_GenericAggregate>();
        public DbSet<BuilderParity_StringAggregate> StringAggregates => Set<BuilderParity_StringAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BuilderParity_MetricEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("builder_parity_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BuilderParity_GenericAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<BuilderParity_GenericAggregate, BuilderParity_MetricEntity>(
                        "builder_parity_generic", "1 hour", x => x.Timestamp)
                    .WithNoData()
                    .CreateGroupIndexes(false)
                    .MaterializedOnly()
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Region)
                    .Where("value > 0");
            });

            modelBuilder.Entity<BuilderParity_StringAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("builder_parity_string", "BuilderParity_MetricEntity", "1 hour", "Timestamp")
                    .WithNoData()
                    .CreateGroupIndexes(false)
                    .MaterializedOnly()
                    .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                    .AddGroupByColumn("Region")
                    .Where("value > 0");
            });
        }
    }

    [Fact]
    public void GenericBuilder_And_StringBuilder_Should_Produce_Identical_Annotations()
    {
        using BuilderParity_Context context = new();
        IModel model = GetModel(context);
        IEntityType genericEntity = model.FindEntityType(typeof(BuilderParity_GenericAggregate))!;
        IEntityType stringEntity = model.FindEntityType(typeof(BuilderParity_StringAggregate))!;

        string[] sharedAnnotations =
        [
            ContinuousAggregateAnnotations.WithNoData,
            ContinuousAggregateAnnotations.CreateGroupIndexes,
            ContinuousAggregateAnnotations.MaterializedOnly,
            ContinuousAggregateAnnotations.WhereClause,
        ];

        foreach (string annotation in sharedAnnotations)
        {
            Assert.Equal(genericEntity.FindAnnotation(annotation)?.Value, stringEntity.FindAnnotation(annotation)?.Value);
        }

        Assert.Equal(
            genericEntity.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>,
            stringEntity.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value as List<string>);
        Assert.Equal(
            genericEntity.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>,
            stringEntity.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value as List<string>);
    }

    #endregion

    #region GetPropertyName_Should_Throw_When_Body_Is_Binary_Expression

    private class BinaryExpressionEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void GetPropertyName_Should_Throw_When_Body_Is_Binary_Expression()
    {
        // Arrange
        Expression<Func<BinaryExpressionEntity, int>> expression = e => e.Id + 1;

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>((Action)(() =>
        {
            _ = ExpressionHelper.GetPropertyName(expression);
        }));

        Assert.Contains("simple property access expression", ex.Message);
    }

    #endregion
}
