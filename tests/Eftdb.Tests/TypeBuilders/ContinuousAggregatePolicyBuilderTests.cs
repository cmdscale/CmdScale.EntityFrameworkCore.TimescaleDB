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

    // ── String-context builder (ContinuousAggregateStringBuilder path) ─────

    #region StringBuilder_WithRefreshPolicy_Should_Set_HasRefreshPolicy_Annotation

    private class MetricSource13
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView13
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderRefreshPolicyContext13 : DbContext
    {
        public DbSet<MetricSource13> Metrics => Set<MetricSource13>();
        public DbSet<AggregateView13> Aggregates => Set<AggregateView13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource13>(entity =>
            {
                entity.ToTable("Metrics13");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView13>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_13", "MetricSource13", "1 hour", "Timestamp")
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour");
            });
        }
    }

    [Fact]
    public void StringBuilder_WithRefreshPolicy_Should_Set_HasRefreshPolicy_Annotation()
    {
        // Arrange
        using StringBuilderRefreshPolicyContext13 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView13))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
    }

    #endregion

    #region StringBuilder_WithRefreshPolicy_Should_Set_Offset_And_Schedule_Annotations

    private class MetricSource14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView14
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderOffsetsContext14 : DbContext
    {
        public DbSet<MetricSource14> Metrics => Set<MetricSource14>();
        public DbSet<AggregateView14> Aggregates => Set<AggregateView14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource14>(entity =>
            {
                entity.ToTable("Metrics14");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView14>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_14", "MetricSource14", "1 hour", "Timestamp")
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "30 minutes", scheduleInterval: "2 hours");
            });
        }
    }

    [Fact]
    public void StringBuilder_WithRefreshPolicy_Should_Set_Offset_And_Schedule_Annotations()
    {
        // Arrange
        using StringBuilderOffsetsContext14 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView14))!;

        // Assert
        Assert.Equal("7 days", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value);
        Assert.Equal("30 minutes", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value);
        Assert.Equal("2 hours", entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region StringBuilder_WithRefreshPolicy_Should_Not_Set_Null_Or_WhiteSpace_Offsets

    private class MetricSource15
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView15
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderNullOffsetsContext15 : DbContext
    {
        public DbSet<MetricSource15> Metrics => Set<MetricSource15>();
        public DbSet<AggregateView15> Aggregates => Set<AggregateView15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource15>(entity =>
            {
                entity.ToTable("Metrics15");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView15>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_15", "MetricSource15", "1 hour", "Timestamp")
                    .WithRefreshPolicy(startOffset: null, endOffset: " ", scheduleInterval: null);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithRefreshPolicy_Should_Not_Set_Null_Or_WhiteSpace_Offsets()
    {
        // Arrange
        using StringBuilderNullOffsetsContext15 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView15))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value);
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset));
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset));
        Assert.Null(entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region StringBuilder_WithInitialStart_Should_Set_Annotation

    private class MetricSource16
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView16
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderInitialStartContext16 : DbContext
    {
        public DbSet<MetricSource16> Metrics => Set<MetricSource16>();
        public DbSet<AggregateView16> Aggregates => Set<AggregateView16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource16>(entity =>
            {
                entity.ToTable("Metrics16");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView16>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_16", "MetricSource16", "1 hour", "Timestamp")
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour")
                    .WithInitialStart(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void StringBuilder_WithInitialStart_Should_Set_Annotation()
    {
        // Arrange
        using StringBuilderInitialStartContext16 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView16))!;

        // Assert
        object? value = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), (DateTime)value);
    }

    #endregion

    #region StringBuilder_WithIfNotExists_Should_Set_Annotation

    private class MetricSource17
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView17
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderIfNotExistsContext17 : DbContext
    {
        public DbSet<MetricSource17> Metrics => Set<MetricSource17>();
        public DbSet<AggregateView17> Aggregates => Set<AggregateView17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource17>(entity =>
            {
                entity.ToTable("Metrics17");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView17>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_17", "MetricSource17", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithIfNotExists(true);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithIfNotExists_Should_Set_Annotation()
    {
        // Arrange
        using StringBuilderIfNotExistsContext17 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView17))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)?.Value);
    }

    #endregion

    #region StringBuilder_WithIncludeTieredData_Should_Set_Annotation

    private class MetricSource18
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView18
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderIncludeTieredDataContext18 : DbContext
    {
        public DbSet<MetricSource18> Metrics => Set<MetricSource18>();
        public DbSet<AggregateView18> Aggregates => Set<AggregateView18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource18>(entity =>
            {
                entity.ToTable("Metrics18");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView18>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_18", "MetricSource18", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithIncludeTieredData(false);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithIncludeTieredData_Should_Set_Annotation()
    {
        // Arrange
        using StringBuilderIncludeTieredDataContext18 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView18))!;

        // Assert
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value);
    }

    #endregion

    #region StringBuilder_WithRefreshNewestFirst_Should_Set_Annotation

    private class MetricSource19
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView19
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderRefreshNewestFirstContext19 : DbContext
    {
        public DbSet<MetricSource19> Metrics => Set<MetricSource19>();
        public DbSet<AggregateView19> Aggregates => Set<AggregateView19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource19>(entity =>
            {
                entity.ToTable("Metrics19");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView19>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_19", "MetricSource19", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithRefreshNewestFirst_Should_Set_Annotation()
    {
        // Arrange
        using StringBuilderRefreshNewestFirstContext19 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView19))!;

        // Assert
        Assert.Equal(false, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value);
    }

    #endregion

    #region StringBuilder_WithBucketsPerBatch_Should_Set_Annotation_When_Valid

    private class MetricSource20
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView20
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderBucketsPerBatchContext20 : DbContext
    {
        public DbSet<MetricSource20> Metrics => Set<MetricSource20>();
        public DbSet<AggregateView20> Aggregates => Set<AggregateView20>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource20>(entity =>
            {
                entity.ToTable("Metrics20");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView20>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_20", "MetricSource20", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithBucketsPerBatch(3);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithBucketsPerBatch_Should_Set_Annotation_When_Valid()
    {
        // Arrange
        using StringBuilderBucketsPerBatchContext20 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView20))!;

        // Assert
        Assert.Equal(3, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value);
    }

    #endregion

    #region StringBuilder_WithMaxBatchesPerExecution_Should_Accept_Zero

    private class MetricSource21
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView21
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderMaxBatchesZeroContext21 : DbContext
    {
        public DbSet<MetricSource21> Metrics => Set<MetricSource21>();
        public DbSet<AggregateView21> Aggregates => Set<AggregateView21>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource21>(entity =>
            {
                entity.ToTable("Metrics21");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView21>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_21", "MetricSource21", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithMaxBatchesPerExecution(0);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithMaxBatchesPerExecution_Should_Accept_Zero()
    {
        // Arrange
        using StringBuilderMaxBatchesZeroContext21 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView21))!;

        // Assert
        Assert.Equal(0, entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value);
    }

    #endregion

    #region StringBuilder_MethodChaining_Should_Support_All_Policy_Options

    private class MetricSource22
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView22
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderFullChainContext22 : DbContext
    {
        public DbSet<MetricSource22> Metrics => Set<MetricSource22>();
        public DbSet<AggregateView22> Aggregates => Set<AggregateView22>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource22>(entity =>
            {
                entity.ToTable("Metrics22");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView22>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_22", "MetricSource22", "1 hour", "Timestamp")
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
    public void StringBuilder_MethodChaining_Should_Support_All_Policy_Options()
    {
        // Arrange
        using StringBuilderFullChainContext22 context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(AggregateView22))!;

        // Assert
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

    #region StringBuilder_WithBucketsPerBatch_Should_Throw_When_Zero

    private class MetricSource23
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView23
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderBucketsPerBatchInvalidContext23(int bucketsPerBatch) : DbContext
    {
        public DbSet<MetricSource23> Metrics => Set<MetricSource23>();
        public DbSet<AggregateView23> Aggregates => Set<AggregateView23>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource23>(entity =>
            {
                entity.ToTable("Metrics23");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView23>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_23", "MetricSource23", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithBucketsPerBatch(bucketsPerBatch);
            });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StringBuilder_WithBucketsPerBatch_Should_Throw_When_Zero(int bucketsPerBatch)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() =>
        {
            using StringBuilderBucketsPerBatchInvalidContext23 context = new(bucketsPerBatch);
            IModel model = GetModel(context);
        });
    }

    #endregion

    #region StringBuilder_WithMaxBatchesPerExecution_Should_Throw_When_Negative

    private class MetricSource24
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateView24
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderMaxBatchesNegativeContext24(int maxBatches) : DbContext
    {
        public DbSet<MetricSource24> Metrics => Set<MetricSource24>();
        public DbSet<AggregateView24> Aggregates => Set<AggregateView24>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricSource24>(entity =>
            {
                entity.ToTable("Metrics24");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AggregateView24>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate("sb_hourly_metrics_24", "MetricSource24", "1 hour", "Timestamp")
                    .WithRefreshPolicy()
                    .WithMaxBatchesPerExecution(maxBatches);
            });
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void StringBuilder_WithMaxBatchesPerExecution_Should_Throw_When_Negative(int maxBatches)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() =>
        {
            using StringBuilderMaxBatchesNegativeContext24 context = new(maxBatches);
            IModel model = GetModel(context);
        });
    }

    #endregion
}
