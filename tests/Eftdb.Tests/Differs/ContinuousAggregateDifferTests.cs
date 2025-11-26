using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class ContinuousAggregateDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_ContinuousAggregate

    private class MetricEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HypertableOnlyContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();

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
        }
    }

    private class BasicContinuousAggregateContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();
        public DbSet<MetricAggregate1> HourlyMetrics => Set<MetricAggregate1>();

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

            modelBuilder.Entity<MetricAggregate1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate1, MetricEntity1>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Detect_New_ContinuousAggregate()
    {
        using HypertableOnlyContext1 sourceContext = new();
        using BasicContinuousAggregateContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        CreateContinuousAggregateOperation? createOp = operations.OfType<CreateContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
        Assert.Equal("hourly_metrics", createOp.MaterializedViewName);
        Assert.Equal("1 hour", createOp.TimeBucketWidth);
        Assert.Contains("AvgValue:Avg:Value", createOp.AggregateFunctions);
    }

    #endregion

    #region Should_Detect_Multiple_New_ContinuousAggregates

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DailyMetricAggregate2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HypertableOnlyContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();

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
        }
    }

    private class MultipleContinuousAggregatesContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<MetricAggregate2> HourlyMetrics => Set<MetricAggregate2>();
        public DbSet<DailyMetricAggregate2> DailyMetrics => Set<DailyMetricAggregate2>();

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

            modelBuilder.Entity<MetricAggregate2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate2, MetricEntity2>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<DailyMetricAggregate2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<DailyMetricAggregate2, MetricEntity2>(
                        "daily_metrics",
                        "1 day",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_New_ContinuousAggregates()
    {
        using HypertableOnlyContext2 sourceContext = new();
        using MultipleContinuousAggregatesContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<CreateContinuousAggregateOperation> createOps = [.. operations.OfType<CreateContinuousAggregateOperation>()];
        Assert.Equal(2, createOps.Count);
        Assert.Contains(createOps, op => op.MaterializedViewName == "hourly_metrics");
        Assert.Contains(createOps, op => op.MaterializedViewName == "daily_metrics");
    }

    #endregion

    #region Should_Detect_ChunkInterval_Change

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<MetricAggregate3> HourlyMetrics => Set<MetricAggregate3>();

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

            modelBuilder.Entity<MetricAggregate3>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate3, MetricEntity3>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class ModifiedChunkIntervalContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<MetricAggregate3> HourlyMetrics => Set<MetricAggregate3>();

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

            modelBuilder.Entity<MetricAggregate3>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate3, MetricEntity3>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "30 days")
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Detect_ChunkInterval_Change()
    {
        using BasicContinuousAggregateContext3 sourceContext = new();
        using ModifiedChunkIntervalContext3 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterContinuousAggregateOperation? alterOp = operations.OfType<AlterContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Null(alterOp.OldChunkInterval);
        Assert.Equal("30 days", alterOp.ChunkInterval);
    }

    #endregion

    #region Should_Detect_CreateGroupIndexes_Change

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();
        public DbSet<MetricAggregate4> HourlyMetrics => Set<MetricAggregate4>();

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

            modelBuilder.Entity<MetricAggregate4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate4, MetricEntity4>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class GroupIndexesEnabledContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();
        public DbSet<MetricAggregate4> HourlyMetrics => Set<MetricAggregate4>();

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

            modelBuilder.Entity<MetricAggregate4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate4, MetricEntity4>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .CreateGroupIndexes(true);
            });
        }
    }

    [Fact]
    public void Should_Detect_CreateGroupIndexes_Change()
    {
        using BasicContinuousAggregateContext4 sourceContext = new();
        using GroupIndexesEnabledContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterContinuousAggregateOperation? alterOp = operations.OfType<AlterContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.False(alterOp.OldCreateGroupIndexes);
        Assert.True(alterOp.CreateGroupIndexes);
    }

    #endregion

    #region Should_Detect_MaterializedOnly_Change

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();
        public DbSet<MetricAggregate5> HourlyMetrics => Set<MetricAggregate5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate5>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate5, MetricEntity5>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class MaterializedOnlyEnabledContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();
        public DbSet<MetricAggregate5> HourlyMetrics => Set<MetricAggregate5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate5>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate5, MetricEntity5>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .MaterializedOnly(true);
            });
        }
    }

    [Fact]
    public void Should_Detect_MaterializedOnly_Change()
    {
        using BasicContinuousAggregateContext5 sourceContext = new();
        using MaterializedOnlyEnabledContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterContinuousAggregateOperation? alterOp = operations.OfType<AlterContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.False(alterOp.OldMaterializedOnly);
        Assert.True(alterOp.MaterializedOnly);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_TimeBucketWidth_Changes

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate6
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();
        public DbSet<MetricAggregate6> HourlyMetrics => Set<MetricAggregate6>();

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

            modelBuilder.Entity<MetricAggregate6>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate6, MetricEntity6>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class DifferentTimeBucketContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();
        public DbSet<MetricAggregate6> HourlyMetrics => Set<MetricAggregate6>();

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

            modelBuilder.Entity<MetricAggregate6>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate6, MetricEntity6>(
                        "hourly_metrics",
                        "1 day",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_TimeBucketWidth_Changes()
    {
        using BasicContinuousAggregateContext6 sourceContext = new();
        using DifferentTimeBucketContext6 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);

        DropContinuousAggregateOperation? dropOp = operations.OfType<DropContinuousAggregateOperation>().FirstOrDefault();
        CreateContinuousAggregateOperation? createOp = operations.OfType<CreateContinuousAggregateOperation>().FirstOrDefault();

        Assert.NotNull(dropOp);
        Assert.NotNull(createOp);
        Assert.Equal("hourly_metrics", dropOp.MaterializedViewName);
        Assert.Equal("hourly_metrics", createOp.MaterializedViewName);
        Assert.Equal("1 day", createOp.TimeBucketWidth);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_AggregateFunction_Changes

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate7
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricAggregateMax7
    {
        public DateTime TimeBucket { get; set; }
        public double MaxValue { get; set; }
    }

    private class BasicContinuousAggregateContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();
        public DbSet<MetricAggregate7> HourlyMetrics => Set<MetricAggregate7>();

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

            modelBuilder.Entity<MetricAggregate7>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate7, MetricEntity7>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class DifferentAggregateFunctionContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();
        public DbSet<MetricAggregateMax7> HourlyMetrics => Set<MetricAggregateMax7>();

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

            modelBuilder.Entity<MetricAggregateMax7>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMax7, MetricEntity7>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_AggregateFunction_Changes()
    {
        using BasicContinuousAggregateContext7 sourceContext = new();
        using DifferentAggregateFunctionContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_GroupByColumns_Change

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregate8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricAggregateWithCategory8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class BasicContinuousAggregateContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();
        public DbSet<MetricAggregate8> HourlyMetrics => Set<MetricAggregate8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate8>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate8, MetricEntity8>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithGroupByColumnsContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();
        public DbSet<MetricAggregateWithCategory8> HourlyMetrics => Set<MetricAggregateWithCategory8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithCategory8>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithCategory8, MetricEntity8>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_GroupByColumns_Change()
    {
        using BasicContinuousAggregateContext8 sourceContext = new();
        using WithGroupByColumnsContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_ParentName_Changes

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlternateMetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate9
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<MetricAggregate9> HourlyMetrics => Set<MetricAggregate9>();

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

            modelBuilder.Entity<MetricAggregate9>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate9, MetricEntity9>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class DifferentParentContext9 : DbContext
    {
        public DbSet<AlternateMetricEntity9> AlternateMetrics => Set<AlternateMetricEntity9>();
        public DbSet<MetricAggregate9> HourlyMetrics => Set<MetricAggregate9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlternateMetricEntity9>(entity =>
            {
                entity.ToTable("AlternateMetrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate9>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate9, AlternateMetricEntity9>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_ParentName_Changes()
    {
        using BasicContinuousAggregateContext9 sourceContext = new();
        using DifferentParentContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Not_Drop_And_Recreate_When_Only_Alterable_Properties_Change

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate10
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();
        public DbSet<MetricAggregate10> HourlyMetrics => Set<MetricAggregate10>();

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

            modelBuilder.Entity<MetricAggregate10>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate10, MetricEntity10>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class OnlyAlterableChangesContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();
        public DbSet<MetricAggregate10> HourlyMetrics => Set<MetricAggregate10>();

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

            modelBuilder.Entity<MetricAggregate10>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate10, MetricEntity10>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "30 days")
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .CreateGroupIndexes(true)
                    .MaterializedOnly(true);
            });
        }
    }

    [Fact]
    public void Should_Not_Drop_And_Recreate_When_Only_Alterable_Properties_Change()
    {
        using BasicContinuousAggregateContext10 sourceContext = new();
        using OnlyAlterableChangesContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
        Assert.Contains(operations, op => op is AlterContinuousAggregateOperation);
    }

    #endregion

    #region Should_Detect_Dropped_ContinuousAggregate

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate11
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<MetricAggregate11> HourlyMetrics => Set<MetricAggregate11>();

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

            modelBuilder.Entity<MetricAggregate11>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate11, MetricEntity11>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class HypertableOnlyContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();

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
        }
    }

    [Fact]
    public void Should_Detect_Dropped_ContinuousAggregate()
    {
        using BasicContinuousAggregateContext11 sourceContext = new();
        using HypertableOnlyContext11 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        DropContinuousAggregateOperation? dropOp = operations.OfType<DropContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
        Assert.Equal("hourly_metrics", dropOp.MaterializedViewName);
    }

    #endregion

    #region Should_Detect_Multiple_Dropped_ContinuousAggregates

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate12
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DailyMetricAggregate12
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultipleContinuousAggregatesContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();
        public DbSet<MetricAggregate12> HourlyMetrics => Set<MetricAggregate12>();
        public DbSet<DailyMetricAggregate12> DailyMetrics => Set<DailyMetricAggregate12>();

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

            modelBuilder.Entity<MetricAggregate12>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate12, MetricEntity12>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<DailyMetricAggregate12>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<DailyMetricAggregate12, MetricEntity12>(
                        "daily_metrics",
                        "1 day",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class HypertableOnlyContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();

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
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Dropped_ContinuousAggregates()
    {
        using MultipleContinuousAggregatesContext12 sourceContext = new();
        using HypertableOnlyContext12 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<DropContinuousAggregateOperation> dropOps = [.. operations.OfType<DropContinuousAggregateOperation>()];
        Assert.Equal(2, dropOps.Count);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_No_Changes

    private class MetricEntity13
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate13
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext13 : DbContext
    {
        public DbSet<MetricEntity13> Metrics => Set<MetricEntity13>();
        public DbSet<MetricAggregate13> HourlyMetrics => Set<MetricAggregate13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity13>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate13>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate13, MetricEntity13>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_No_Changes()
    {
        using BasicContinuousAggregateContext13 sourceContext = new();
        using BasicContinuousAggregateContext13 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Not_Drop_And_Recreate_When_Both_AggregateFunctions_Are_Null

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate14
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullAggregateFunctionsContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();
        public DbSet<MetricAggregate14> HourlyMetrics => Set<MetricAggregate14>();

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

            modelBuilder.Entity<MetricAggregate14>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate14, MetricEntity14>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Not_Drop_And_Recreate_When_Both_AggregateFunctions_Are_Null()
    {
        using NullAggregateFunctionsContext14 sourceContext = new();
        using NullAggregateFunctionsContext14 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_Source_AggregateFunctions_Null_And_Target_Has_Functions

    private class MetricEntity15
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate15
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullAggregateFunctionsContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();
        public DbSet<MetricAggregate15> HourlyMetrics => Set<MetricAggregate15>();

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

            modelBuilder.Entity<MetricAggregate15>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate15, MetricEntity15>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp);
            });
        }
    }

    private class BasicContinuousAggregateContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();
        public DbSet<MetricAggregate15> HourlyMetrics => Set<MetricAggregate15>();

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

            modelBuilder.Entity<MetricAggregate15>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate15, MetricEntity15>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_Source_AggregateFunctions_Null_And_Target_Has_Functions()
    {
        using NullAggregateFunctionsContext15 sourceContext = new();
        using BasicContinuousAggregateContext15 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_Source_Has_AggregateFunctions_And_Target_Null

    private class MetricEntity16
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate16
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();
        public DbSet<MetricAggregate16> HourlyMetrics => Set<MetricAggregate16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate16>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate16, MetricEntity16>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class NullAggregateFunctionsContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();
        public DbSet<MetricAggregate16> HourlyMetrics => Set<MetricAggregate16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate16>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate16, MetricEntity16>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_Source_Has_AggregateFunctions_And_Target_Null()
    {
        using BasicContinuousAggregateContext16 sourceContext = new();
        using NullAggregateFunctionsContext16 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Not_Drop_And_Recreate_When_Both_GroupByColumns_Are_Null

    private class MetricEntity17
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate17
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoGroupByColumnsContext17 : DbContext
    {
        public DbSet<MetricEntity17> Metrics => Set<MetricEntity17>();
        public DbSet<MetricAggregate17> HourlyMetrics => Set<MetricAggregate17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity17>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate17>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate17, MetricEntity17>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Drop_And_Recreate_When_Both_GroupByColumns_Are_Null()
    {
        using NoGroupByColumnsContext17 sourceContext = new();
        using NoGroupByColumnsContext17 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_Source_GroupByColumns_Null_And_Target_Has_Columns

    private class MetricEntity18
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregate18
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricAggregateWithCategory18
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class NoGroupByColumnsContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();
        public DbSet<MetricAggregate18> HourlyMetrics => Set<MetricAggregate18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate18>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate18, MetricEntity18>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithGroupByColumnsContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();
        public DbSet<MetricAggregateWithCategory18> HourlyMetrics => Set<MetricAggregateWithCategory18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithCategory18>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithCategory18, MetricEntity18>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_Source_GroupByColumns_Null_And_Target_Has_Columns()
    {
        using NoGroupByColumnsContext18 sourceContext = new();
        using WithGroupByColumnsContext18 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_Source_Has_GroupByColumns_And_Target_Null

    private class MetricEntity19
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregate19
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricAggregateWithCategory19
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class WithGroupByColumnsContext19 : DbContext
    {
        public DbSet<MetricEntity19> Metrics => Set<MetricEntity19>();
        public DbSet<MetricAggregateWithCategory19> HourlyMetrics => Set<MetricAggregateWithCategory19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity19>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithCategory19>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithCategory19, MetricEntity19>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    private class NoGroupByColumnsContext19 : DbContext
    {
        public DbSet<MetricEntity19> Metrics => Set<MetricEntity19>();
        public DbSet<MetricAggregate19> HourlyMetrics => Set<MetricAggregate19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity19>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate19>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate19, MetricEntity19>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_Source_Has_GroupByColumns_And_Target_Null()
    {
        using WithGroupByColumnsContext19 sourceContext = new();
        using NoGroupByColumnsContext19 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Handle_Null_Source_Model

    private class MetricEntity20
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate20
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext20 : DbContext
    {
        public DbSet<MetricEntity20> Metrics => Set<MetricEntity20>();
        public DbSet<MetricAggregate20> HourlyMetrics => Set<MetricAggregate20>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity20>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate20>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate20, MetricEntity20>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        using BasicContinuousAggregateContext20 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        CreateContinuousAggregateOperation? createOp = operations.OfType<CreateContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity21
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate21
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext21 : DbContext
    {
        public DbSet<MetricEntity21> Metrics => Set<MetricEntity21>();
        public DbSet<MetricAggregate21> HourlyMetrics => Set<MetricAggregate21>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity21>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate21>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate21, MetricEntity21>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        using BasicContinuousAggregateContext21 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        DropContinuousAggregateOperation? dropOp = operations.OfType<DropContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
    }

    #endregion

    #region Should_Handle_Both_Null_Models

    [Fact]
    public void Should_Handle_Both_Null_Models()
    {
        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, null);

        Assert.Empty(operations);
    }

    #endregion
}
