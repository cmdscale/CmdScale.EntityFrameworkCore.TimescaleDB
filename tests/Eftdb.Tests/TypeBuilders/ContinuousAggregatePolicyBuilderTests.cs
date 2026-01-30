using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify ContinuousAggregatePolicyBuilder and ContinuousAggregateBuilderPolicyExtensions
/// correctly apply annotations and validate inputs.
/// </summary>
public class ContinuousAggregatePolicyBuilderTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region WithRefreshPolicy_Should_Set_HasRefreshPolicy_Annotation

    private class MetricSource1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RefreshPolicyContext1 : DbContext
    {
        public DbSet<MetricSource1> Metrics => Set<MetricSource1>();
        public DbSet<AggregateView1> Aggregates => Set<AggregateView1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource1>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView1, MetricSource1>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void WithRefreshPolicy_Should_Set_HasRefreshPolicy_Annotation()
    {
        using RefreshPolicyContext1 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView1))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
    }

    #endregion

    #region WithRefreshPolicy_Should_Set_Offset_And_Schedule_Annotations

    private class MetricSource2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OffsetsContext2 : DbContext
    {
        public DbSet<MetricSource2> Metrics => Set<MetricSource2>();
        public DbSet<AggregateView2> Aggregates => Set<AggregateView2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource2>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView2, MetricSource2>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "30 minutes", scheduleInterval: "2 hours");
            });
        }
    }

    [Fact]
    public void WithRefreshPolicy_Should_Set_Offset_And_Schedule_Annotations()
    {
        using OffsetsContext2 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView2))!;

        Assert.Equal("7 days", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value);
        Assert.Equal("30 minutes", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value);
        Assert.Equal("2 hours", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region WithRefreshPolicy_Should_Not_Set_Null_Or_Empty_Strings

    private class MetricSource3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullOffsetsContext3 : DbContext
    {
        public DbSet<MetricSource3> Metrics => Set<MetricSource3>();
        public DbSet<AggregateView3> Aggregates => Set<AggregateView3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource3>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView3>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView3, MetricSource3>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: null, endOffset: null, scheduleInterval: null);
            });
        }
    }

    [Fact]
    public void WithRefreshPolicy_Should_Not_Set_Null_Or_Empty_Strings()
    {
        using NullOffsetsContext3 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView3))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset));
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset));
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region WithInitialStart_Should_Set_Annotation

    private class MetricSource4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class InitialStartContext4 : DbContext
    {
        public DbSet<MetricSource4> Metrics => Set<MetricSource4>();
        public DbSet<AggregateView4> Aggregates => Set<AggregateView4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView4, MetricSource4>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithInitialStart(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void WithInitialStart_Should_Set_Annotation()
    {
        using InitialStartContext4 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView4))!;

        object? value = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), (DateTime)value);
    }

    #endregion

    #region WithBucketsPerBatch_Should_Throw_When_LessThan_One

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void WithBucketsPerBatch_Should_Throw_When_LessThan_One(int bucketsPerBatch)
    {
        // Build a real context and builder to call WithBucketsPerBatch on
        MetricSource5 dummySource = new();
        Assert.Throws<ArgumentException>(() =>
        {
            using BucketsPerBatchInvalidContext5 context = new(bucketsPerBatch);
            IModel model = GetModel(context);
        });
    }

    private class MetricSource5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketsPerBatchInvalidContext5(int bucketsPerBatch) : DbContext
    {
        public DbSet<MetricSource5> Metrics => Set<MetricSource5>();
        public DbSet<AggregateView5> Aggregates => Set<AggregateView5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView5>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView5, MetricSource5>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithBucketsPerBatch(bucketsPerBatch);
            });
        }
    }

    #endregion

    #region WithBucketsPerBatch_Should_Set_Annotation_When_Valid

    private class MetricSource6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView6
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketsPerBatchValidContext6 : DbContext
    {
        public DbSet<MetricSource6> Metrics => Set<MetricSource6>();
        public DbSet<AggregateView6> Aggregates => Set<AggregateView6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView6>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView6, MetricSource6>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithBucketsPerBatch(5);
            });
        }
    }

    [Fact]
    public void WithBucketsPerBatch_Should_Set_Annotation_When_Valid()
    {
        using BucketsPerBatchValidContext6 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView6))!;

        Assert.Equal(5, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value);
    }

    #endregion

    #region WithMaxBatchesPerExecution_Should_Throw_When_Negative

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void WithMaxBatchesPerExecution_Should_Throw_When_Negative(int maxBatches)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            using MaxBatchesInvalidContext7 context = new(maxBatches);
            IModel model = GetModel(context);
        });
    }

    private class MetricSource7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView7
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaxBatchesInvalidContext7(int maxBatches) : DbContext
    {
        public DbSet<MetricSource7> Metrics => Set<MetricSource7>();
        public DbSet<AggregateView7> Aggregates => Set<AggregateView7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView7>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView7, MetricSource7>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithMaxBatchesPerExecution(maxBatches);
            });
        }
    }

    #endregion

    #region WithMaxBatchesPerExecution_Should_Accept_Zero

    private class MetricSource8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaxBatchesZeroContext8 : DbContext
    {
        public DbSet<MetricSource8> Metrics => Set<MetricSource8>();
        public DbSet<AggregateView8> Aggregates => Set<AggregateView8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView8>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView8, MetricSource8>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithMaxBatchesPerExecution(0);
            });
        }
    }

    [Fact]
    public void WithMaxBatchesPerExecution_Should_Accept_Zero()
    {
        using MaxBatchesZeroContext8 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView8))!;

        Assert.Equal(0, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value);
    }

    #endregion

    #region WithRefreshNewestFirst_Should_Set_Annotation

    private class MetricSource9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView9
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RefreshNewestFirstContext9 : DbContext
    {
        public DbSet<MetricSource9> Metrics => Set<MetricSource9>();
        public DbSet<AggregateView9> Aggregates => Set<AggregateView9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView9>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView9, MetricSource9>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public void WithRefreshNewestFirst_Should_Set_Annotation()
    {
        using RefreshNewestFirstContext9 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView9))!;

        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value);
    }

    #endregion

    #region WithIncludeTieredData_Should_Set_Annotation

    private class MetricSource10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView10
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IncludeTieredDataContext10 : DbContext
    {
        public DbSet<MetricSource10> Metrics => Set<MetricSource10>();
        public DbSet<AggregateView10> Aggregates => Set<AggregateView10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource10>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView10>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView10, MetricSource10>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithIncludeTieredData(true);
            });
        }
    }

    [Fact]
    public void WithIncludeTieredData_Should_Set_Annotation()
    {
        using IncludeTieredDataContext10 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView10))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value);
    }

    #endregion

    #region WithIfNotExists_Should_Set_Annotation

    private class MetricSource11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView11
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IfNotExistsContext11 : DbContext
    {
        public DbSet<MetricSource11> Metrics => Set<MetricSource11>();
        public DbSet<AggregateView11> Aggregates => Set<AggregateView11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView11>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView11, MetricSource11>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithIfNotExists(true);
            });
        }
    }

    [Fact]
    public void WithIfNotExists_Should_Set_Annotation()
    {
        using IfNotExistsContext11 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView11))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)?.Value);
    }

    #endregion

    #region MethodChaining_Should_Support_All_Policy_Options

    private class MetricSource12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView12
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class FullChainContext12 : DbContext
    {
        public DbSet<MetricSource12> Metrics => Set<MetricSource12>();
        public DbSet<AggregateView12> Aggregates => Set<AggregateView12>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource12>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateView12>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateView12, MetricSource12>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "30 minutes")
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    .WithIfNotExists(true)
                    .WithIncludeTieredData(false)
                    .WithBucketsPerBatch(3)
                    .WithMaxBatchesPerExecution(10)
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public void MethodChaining_Should_Support_All_Policy_Options()
    {
        using FullChainContext12 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView12))!;

        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value);
        Assert.Equal("1 hour", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value);
        Assert.Equal("30 minutes", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)?.Value);
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value);
        Assert.Equal(10, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value);
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value);
    }

    #endregion
}
