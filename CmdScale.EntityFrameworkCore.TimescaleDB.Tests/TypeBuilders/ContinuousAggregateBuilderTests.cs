using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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
}
