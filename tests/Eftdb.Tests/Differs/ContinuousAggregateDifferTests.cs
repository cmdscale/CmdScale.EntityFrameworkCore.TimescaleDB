using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
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

    #region Should_Drop_And_Recreate_When_AggregateFunctions_Count_Differs

    private class MetricEntity22
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate22
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricAggregateMultiple22
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public double MaxValue { get; set; }
    }

    private class SingleAggregateFunctionContext22 : DbContext
    {
        public DbSet<MetricEntity22> Metrics => Set<MetricEntity22>();
        public DbSet<MetricAggregate22> HourlyMetrics => Set<MetricAggregate22>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity22>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate22>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate22, MetricEntity22>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class MultipleAggregateFunctionsContext22 : DbContext
    {
        public DbSet<MetricEntity22> Metrics => Set<MetricEntity22>();
        public DbSet<MetricAggregateMultiple22> HourlyMetrics => Set<MetricAggregateMultiple22>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity22>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateMultiple22>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMultiple22, MetricEntity22>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_AggregateFunctions_Count_Differs()
    {
        using SingleAggregateFunctionContext22 sourceContext = new();
        using MultipleAggregateFunctionsContext22 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_GroupByColumns_Count_Differs

    private class MetricEntity23
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
    }

    private class MetricAggregateSingleGroupBy23
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregateMultipleGroupBy23
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
    }

    private class SingleGroupByColumnContext23 : DbContext
    {
        public DbSet<MetricEntity23> Metrics => Set<MetricEntity23>();
        public DbSet<MetricAggregateSingleGroupBy23> HourlyMetrics => Set<MetricAggregateSingleGroupBy23>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity23>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateSingleGroupBy23>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateSingleGroupBy23, MetricEntity23>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    private class MultipleGroupByColumnsContext23 : DbContext
    {
        public DbSet<MetricEntity23> Metrics => Set<MetricEntity23>();
        public DbSet<MetricAggregateMultipleGroupBy23> HourlyMetrics => Set<MetricAggregateMultipleGroupBy23>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity23>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateMultipleGroupBy23>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMultipleGroupBy23, MetricEntity23>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category)
                    .AddGroupByColumn(x => x.Region);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_GroupByColumns_Count_Differs()
    {
        using SingleGroupByColumnContext23 sourceContext = new();
        using MultipleGroupByColumnsContext23 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_ViewDefinition_Changes

    private class MetricEntity24
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate24
    {
        public DateTime Bucket { get; set; }
    }

    private class RawDefinitionContextA24 : DbContext
    {
        public DbSet<MetricEntity24> Metrics => Set<MetricEntity24>();
        public DbSet<MetricAggregate24> HourlyMetrics => Set<MetricAggregate24>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity24>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate24>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MetricEntity24));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"Metrics\" GROUP BY bucket;");
            });
        }
    }

    private class RawDefinitionContextB24 : DbContext
    {
        public DbSet<MetricEntity24> Metrics => Set<MetricEntity24>();
        public DbSet<MetricAggregate24> HourlyMetrics => Set<MetricAggregate24>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity24>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate24>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MetricEntity24));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 day', \"Timestamp\") AS bucket FROM \"Metrics\" GROUP BY bucket;");
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_ViewDefinition_Changes()
    {
        // Arrange
        using RawDefinitionContextA24 sourceContext = new();
        using RawDefinitionContextB24 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        DropContinuousAggregateOperation? dropOp = operations.OfType<DropContinuousAggregateOperation>().FirstOrDefault();
        CreateContinuousAggregateOperation? createOp = operations.OfType<CreateContinuousAggregateOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
        Assert.NotNull(createOp);
        Assert.Equal("hourly_metrics", dropOp.MaterializedViewName);
        Assert.Equal("hourly_metrics", createOp.MaterializedViewName);
        Assert.Contains("1 day", createOp.ViewDefinition);

        int dropIndex = operations.ToList().FindIndex(o => o is DropContinuousAggregateOperation);
        int createIndex = operations.ToList().FindIndex(o => o is CreateContinuousAggregateOperation);
        Assert.True(dropIndex < createIndex, "Drop operation must precede the create operation.");
    }

    #endregion

    #region Should_Emit_No_Operation_When_ViewDefinition_Identical

    private class MetricEntity25
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate25
    {
        public DateTime Bucket { get; set; }
    }

    private class IdenticalRawDefinitionContextA25 : DbContext
    {
        public DbSet<MetricEntity25> Metrics => Set<MetricEntity25>();
        public DbSet<MetricAggregate25> HourlyMetrics => Set<MetricAggregate25>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity25>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate25>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MetricEntity25));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"Metrics\" GROUP BY bucket;");
            });
        }
    }

    private class IdenticalRawDefinitionContextB25 : DbContext
    {
        public DbSet<MetricEntity25> Metrics => Set<MetricEntity25>();
        public DbSet<MetricAggregate25> HourlyMetrics => Set<MetricAggregate25>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity25>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate25>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MetricEntity25));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"Metrics\" GROUP BY bucket;");
            });
        }
    }

    [Fact]
    public void Should_Emit_No_Operation_When_ViewDefinition_Identical()
    {
        // Arrange
        using IdenticalRawDefinitionContextA25 sourceContext = new();
        using IdenticalRawDefinitionContextB25 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Not_Drop_And_Recreate_When_Same_GroupByColumns_And_Same_Count

    private class MetricEntity26
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregateWithCategory26
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class WithGroupByColumnsContext26 : DbContext
    {
        public DbSet<MetricEntity26> Metrics => Set<MetricEntity26>();
        public DbSet<MetricAggregateWithCategory26> HourlyMetrics => Set<MetricAggregateWithCategory26>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity26>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithCategory26>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithCategory26, MetricEntity26>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    [Fact]
    public void Should_Not_Drop_And_Recreate_When_Same_GroupByColumns_And_Same_Count()
    {
        // Arrange
        using WithGroupByColumnsContext26 sourceContext = new();
        using WithGroupByColumnsContext26 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_GroupByColumns_Same_Count_Different_Values

    private class MetricEntity27
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
    }

    private class MetricAggregateWithCategory27
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
    }

    private class MetricAggregateWithRegion27
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Region { get; set; }
    }

    private class GroupByCategoryContext27 : DbContext
    {
        public DbSet<MetricEntity27> Metrics => Set<MetricEntity27>();
        public DbSet<MetricAggregateWithCategory27> HourlyMetrics => Set<MetricAggregateWithCategory27>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity27>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithCategory27>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithCategory27, MetricEntity27>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    private class GroupByRegionContext27 : DbContext
    {
        public DbSet<MetricEntity27> Metrics => Set<MetricEntity27>();
        public DbSet<MetricAggregateWithRegion27> HourlyMetrics => Set<MetricAggregateWithRegion27>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity27>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateWithRegion27>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateWithRegion27, MetricEntity27>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Region);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_GroupByColumns_Same_Count_Different_Values()
    {
        // Arrange
        using GroupByCategoryContext27 sourceContext = new();
        using GroupByRegionContext27 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Accept_Explicit_Non_Null_FeatureDiffContext

    private class MetricEntity28
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate28
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BasicContinuousAggregateContext28 : DbContext
    {
        public DbSet<MetricEntity28> Metrics => Set<MetricEntity28>();
        public DbSet<MetricAggregate28> HourlyMetrics => Set<MetricAggregate28>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity28>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate28>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate28, MetricEntity28>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Accept_Explicit_Non_Null_FeatureDiffContext()
    {
        // Arrange
        using BasicContinuousAggregateContext28 sourceContext = new();
        using BasicContinuousAggregateContext28 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();
        FeatureDiffContext context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_WhereClause_Changes

    private class MetricEntity29
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate29
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoWhereClauseContext29 : DbContext
    {
        public DbSet<MetricEntity29> Metrics => Set<MetricEntity29>();
        public DbSet<MetricAggregate29> HourlyMetrics => Set<MetricAggregate29>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity29>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate29>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate29, MetricEntity29>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithWhereClauseContext29 : DbContext
    {
        public DbSet<MetricEntity29> Metrics => Set<MetricEntity29>();
        public DbSet<MetricAggregate29> HourlyMetrics => Set<MetricAggregate29>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity29>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate29>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate29, MetricEntity29>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .Where("value > 0");
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_WhereClause_Changes()
    {
        // Arrange
        using NoWhereClauseContext29 sourceContext = new();
        using WithWhereClauseContext29 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_WithNoData_Changes

    private class MetricEntity30
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate30
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithDataContext30 : DbContext
    {
        public DbSet<MetricEntity30> Metrics => Set<MetricEntity30>();
        public DbSet<MetricAggregate30> HourlyMetrics => Set<MetricAggregate30>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity30>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate30>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate30, MetricEntity30>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithNoDataContext30 : DbContext
    {
        public DbSet<MetricEntity30> Metrics => Set<MetricEntity30>();
        public DbSet<MetricAggregate30> HourlyMetrics => Set<MetricAggregate30>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity30>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate30>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate30, MetricEntity30>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithNoData();
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_WithNoData_Changes()
    {
        // Arrange
        using WithDataContext30 sourceContext = new();
        using WithNoDataContext30 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_TimeBucketGroupBy_Changes

    private class MetricEntity31
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate31
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class TimeBucketGroupByEnabledContext31 : DbContext
    {
        public DbSet<MetricEntity31> Metrics => Set<MetricEntity31>();
        public DbSet<MetricAggregate31> HourlyMetrics => Set<MetricAggregate31>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity31>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate31>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate31, MetricEntity31>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp,
                        timeBucketGroupBy: true)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class TimeBucketGroupByDisabledContext31 : DbContext
    {
        public DbSet<MetricEntity31> Metrics => Set<MetricEntity31>();
        public DbSet<MetricAggregate31> HourlyMetrics => Set<MetricAggregate31>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity31>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate31>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate31, MetricEntity31>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp,
                        timeBucketGroupBy: false)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_TimeBucketGroupBy_Changes()
    {
        // Arrange
        using TimeBucketGroupByEnabledContext31 sourceContext = new();
        using TimeBucketGroupByDisabledContext31 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_AggregateFunctions_Same_Count_Different_Values

    private class MetricEntity32
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregateAvg32
    {
        public DateTime TimeBucket { get; set; }
        public double AggValue { get; set; }
    }

    private class MetricAggregateMax32
    {
        public DateTime TimeBucket { get; set; }
        public double AggValue { get; set; }
    }

    private class AvgAggregateFunctionContext32 : DbContext
    {
        public DbSet<MetricEntity32> Metrics => Set<MetricEntity32>();
        public DbSet<MetricAggregateAvg32> HourlyMetrics => Set<MetricAggregateAvg32>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity32>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateAvg32>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateAvg32, MetricEntity32>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AggValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class MaxAggregateFunctionContext32 : DbContext
    {
        public DbSet<MetricEntity32> Metrics => Set<MetricEntity32>();
        public DbSet<MetricAggregateMax32> HourlyMetrics => Set<MetricAggregateMax32>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity32>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateMax32>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMax32, MetricEntity32>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AggValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_AggregateFunctions_Same_Count_Different_Values()
    {
        // Arrange
        using AvgAggregateFunctionContext32 sourceContext = new();
        using MaxAggregateFunctionContext32 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Not_Recreate_When_GroupByColumns_Same_Values_Different_List_Order

    private class MetricEntity33
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
    }

    private class MetricAggregateMultiGroupBy33
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
    }

    private class GroupByCategoryFirstContext33 : DbContext
    {
        public DbSet<MetricEntity33> Metrics => Set<MetricEntity33>();
        public DbSet<MetricAggregateMultiGroupBy33> HourlyMetrics => Set<MetricAggregateMultiGroupBy33>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity33>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateMultiGroupBy33>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMultiGroupBy33, MetricEntity33>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category)
                    .AddGroupByColumn(x => x.Region);
            });
        }
    }

    private class GroupByRegionFirstContext33 : DbContext
    {
        public DbSet<MetricEntity33> Metrics => Set<MetricEntity33>();
        public DbSet<MetricAggregateMultiGroupBy33> HourlyMetrics => Set<MetricAggregateMultiGroupBy33>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity33>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregateMultiGroupBy33>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregateMultiGroupBy33, MetricEntity33>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Region)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    [Fact]
    public void Should_Not_Recreate_When_GroupByColumns_Same_Values_Different_List_Order()
    {
        // Arrange
        using GroupByCategoryFirstContext33 sourceContext = new();
        using GroupByRegionFirstContext33 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_TimeBucketSourceColumn_Changes

    private class MetricEntity34
    {
        public DateTime Timestamp { get; set; }
        public DateTime CreatedAt { get; set; }
        public double Value { get; set; }
    }

    private class MetricAggregate34
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class TimestampSourceColumnContext34 : DbContext
    {
        public DbSet<MetricEntity34> Metrics => Set<MetricEntity34>();
        public DbSet<MetricAggregate34> HourlyMetrics => Set<MetricAggregate34>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity34>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate34>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate34, MetricEntity34>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class CreatedAtSourceColumnContext34 : DbContext
    {
        public DbSet<MetricEntity34> Metrics => Set<MetricEntity34>();
        public DbSet<MetricAggregate34> HourlyMetrics => Set<MetricAggregate34>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity34>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MetricAggregate34>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MetricAggregate34, MetricEntity34>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.CreatedAt)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_TimeBucketSourceColumn_Changes()
    {
        // Arrange
        using TimestampSourceColumnContext34 sourceContext = new();
        using CreatedAtSourceColumnContext34 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_AggregateFunction_Count_Differs

    private class MetricEntity35
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SingleAggregate35
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DoubleAggregate35
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public double MaxValue { get; set; }
    }

    private class SingleAggregateContext35 : DbContext
    {
        public DbSet<MetricEntity35> Metrics => Set<MetricEntity35>();
        public DbSet<SingleAggregate35> HourlyMetrics => Set<SingleAggregate35>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity35>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SingleAggregate35>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SingleAggregate35, MetricEntity35>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class DoubleAggregateContext35 : DbContext
    {
        public DbSet<MetricEntity35> Metrics => Set<MetricEntity35>();
        public DbSet<DoubleAggregate35> HourlyMetrics => Set<DoubleAggregate35>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity35>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DoubleAggregate35>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<DoubleAggregate35, MetricEntity35>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_AggregateFunction_Count_Differs()
    {
        // Arrange
        using SingleAggregateContext35 sourceContext = new();
        using DoubleAggregateContext35 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_And_Recreate_When_GroupByColumn_Count_Differs

    private class MetricEntity36
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }

    private class SingleGroupByAggregate36
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    private class DoubleGroupByAggregate36
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }

    private class SingleGroupByContext36 : DbContext
    {
        public DbSet<MetricEntity36> Metrics => Set<MetricEntity36>();
        public DbSet<SingleGroupByAggregate36> HourlyMetrics => Set<SingleGroupByAggregate36>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity36>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SingleGroupByAggregate36>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SingleGroupByAggregate36, MetricEntity36>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category);
            });
        }
    }

    private class DoubleGroupByContext36 : DbContext
    {
        public DbSet<MetricEntity36> Metrics => Set<MetricEntity36>();
        public DbSet<DoubleGroupByAggregate36> HourlyMetrics => Set<DoubleGroupByAggregate36>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity36>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DoubleGroupByAggregate36>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<DoubleGroupByAggregate36, MetricEntity36>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.Category)
                    .AddGroupByColumn(x => x.Region);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_GroupByColumn_Count_Differs()
    {
        // Arrange
        using SingleGroupByContext36 sourceContext = new();
        using DoubleGroupByContext36 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    // ── Hierarchical continuous aggregates ──

    #region Should_Add_Child_Aggregate_To_Unchanged_Parent

    private class HierAddRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HierAddHourly
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierAddDaily
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierAddParentOnlyContext : DbContext
    {
        public DbSet<HierAddRaw> Raw => Set<HierAddRaw>();
        public DbSet<HierAddHourly> Hourly => Set<HierAddHourly>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierAddRaw>(entity =>
            {
                entity.ToTable("hier_add_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierAddHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierAddHourly, HierAddRaw>(
                        "hier_add_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class HierAddParentAndChildContext : DbContext
    {
        public DbSet<HierAddRaw> Raw => Set<HierAddRaw>();
        public DbSet<HierAddHourly> Hourly => Set<HierAddHourly>();
        public DbSet<HierAddDaily> Daily => Set<HierAddDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierAddRaw>(entity =>
            {
                entity.ToTable("hier_add_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierAddHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierAddHourly, HierAddRaw>(
                        "hier_add_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<HierAddDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierAddDaily, HierAddHourly>(
                        "hier_add_daily",
                        "1 day",
                        x => x.TimeBucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Add_Child_Aggregate_To_Unchanged_Parent()
    {
        using HierAddParentOnlyContext sourceContext = new();
        using HierAddParentAndChildContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        CreateContinuousAggregateOperation createOp = Assert.Single(operations.OfType<CreateContinuousAggregateOperation>());
        Assert.Equal("hier_add_daily", createOp.MaterializedViewName);
        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
    }

    #endregion

    #region Should_Drop_Child_Before_Parent_When_Both_Removed

    private class HierRemoveRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HierRemoveHourly
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierRemoveDaily
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierRemoveParentAndChildContext : DbContext
    {
        public DbSet<HierRemoveRaw> Raw => Set<HierRemoveRaw>();
        public DbSet<HierRemoveHourly> Hourly => Set<HierRemoveHourly>();
        public DbSet<HierRemoveDaily> Daily => Set<HierRemoveDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierRemoveRaw>(entity =>
            {
                entity.ToTable("hier_remove_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierRemoveHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierRemoveHourly, HierRemoveRaw>(
                        "hier_remove_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<HierRemoveDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierRemoveDaily, HierRemoveHourly>(
                        "hier_remove_daily",
                        "1 day",
                        x => x.TimeBucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    private class HierRemoveHypertableOnlyContext : DbContext
    {
        public DbSet<HierRemoveRaw> Raw => Set<HierRemoveRaw>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierRemoveRaw>(entity =>
            {
                entity.ToTable("hier_remove_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Drop_Child_Before_Parent_When_Both_Removed()
    {
        using HierRemoveParentAndChildContext sourceContext = new();
        using HierRemoveHypertableOnlyContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<DropContinuousAggregateOperation> drops = [.. operations.OfType<DropContinuousAggregateOperation>()];
        Assert.Equal(2, drops.Count);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);

        int childIndex = drops.FindIndex(op => op.MaterializedViewName == "hier_remove_daily");
        int parentIndex = drops.FindIndex(op => op.MaterializedViewName == "hier_remove_hourly");
        Assert.True(childIndex < parentIndex);
    }

    #endregion

    #region Should_Cascade_Drop_And_Recreate_When_Parent_Structurally_Changes

    private class HierCascadeRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HierCascadeHourly
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierCascadeDaily
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierCascadeInitialContext : DbContext
    {
        public DbSet<HierCascadeRaw> Raw => Set<HierCascadeRaw>();
        public DbSet<HierCascadeHourly> Hourly => Set<HierCascadeHourly>();
        public DbSet<HierCascadeDaily> Daily => Set<HierCascadeDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierCascadeRaw>(entity =>
            {
                entity.ToTable("hier_cascade_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierCascadeHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierCascadeHourly, HierCascadeRaw>(
                        "hier_cascade_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<HierCascadeDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierCascadeDaily, HierCascadeHourly>(
                        "hier_cascade_daily",
                        "1 day",
                        x => x.TimeBucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    private class HierCascadeChangedParentContext : DbContext
    {
        public DbSet<HierCascadeRaw> Raw => Set<HierCascadeRaw>();
        public DbSet<HierCascadeHourly> Hourly => Set<HierCascadeHourly>();
        public DbSet<HierCascadeDaily> Daily => Set<HierCascadeDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierCascadeRaw>(entity =>
            {
                entity.ToTable("hier_cascade_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierCascadeHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierCascadeHourly, HierCascadeRaw>(
                        "hier_cascade_hourly",
                        "2 hours",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<HierCascadeDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierCascadeDaily, HierCascadeHourly>(
                        "hier_cascade_daily",
                        "1 day",
                        x => x.TimeBucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Cascade_Drop_And_Recreate_When_Parent_Structurally_Changes()
    {
        using HierCascadeInitialContext sourceContext = new();
        using HierCascadeChangedParentContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.DoesNotContain(operations, op => op is AlterContinuousAggregateOperation);

        List<DropContinuousAggregateOperation> drops = [.. operations.OfType<DropContinuousAggregateOperation>()];
        List<CreateContinuousAggregateOperation> creates = [.. operations.OfType<CreateContinuousAggregateOperation>()];
        Assert.Equal(2, drops.Count);
        Assert.Equal(2, creates.Count);

        int dropChildIndex = drops.FindIndex(op => op.MaterializedViewName == "hier_cascade_daily");
        int dropParentIndex = drops.FindIndex(op => op.MaterializedViewName == "hier_cascade_hourly");
        Assert.True(dropChildIndex < dropParentIndex);

        int createParentIndex = creates.FindIndex(op => op.MaterializedViewName == "hier_cascade_hourly");
        int createChildIndex = creates.FindIndex(op => op.MaterializedViewName == "hier_cascade_daily");
        Assert.True(createParentIndex < createChildIndex);
    }

    #endregion

    #region Should_Create_Both_Hierarchical_Aggregates_ParentFirst_From_Empty

    private class HierCreateRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChildAggregateFirstAlphabetically
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ParentAggregateSecondAlphabetically
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierCreateHypertableOnlyContext : DbContext
    {
        public DbSet<HierCreateRaw> Raw => Set<HierCreateRaw>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierCreateRaw>(entity =>
            {
                entity.ToTable("hier_create_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class HierCreateFullContext : DbContext
    {
        public DbSet<HierCreateRaw> Raw => Set<HierCreateRaw>();
        public DbSet<ChildAggregateFirstAlphabetically> Daily => Set<ChildAggregateFirstAlphabetically>();
        public DbSet<ParentAggregateSecondAlphabetically> Hourly => Set<ParentAggregateSecondAlphabetically>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierCreateRaw>(entity =>
            {
                entity.ToTable("hier_create_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ParentAggregateSecondAlphabetically>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<ParentAggregateSecondAlphabetically, HierCreateRaw>(
                        "hier_create_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<ChildAggregateFirstAlphabetically>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<ChildAggregateFirstAlphabetically, ParentAggregateSecondAlphabetically>(
                        "hier_create_daily",
                        "1 day",
                        x => x.TimeBucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Create_Both_Hierarchical_Aggregates_ParentFirst_From_Empty()
    {
        using HierCreateHypertableOnlyContext sourceContext = new();
        using HierCreateFullContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<CreateContinuousAggregateOperation> creates = [.. operations.OfType<CreateContinuousAggregateOperation>()];
        Assert.Equal(2, creates.Count);

        int parentIndex = creates.FindIndex(op => op.MaterializedViewName == "hier_create_hourly");
        int childIndex = creates.FindIndex(op => op.MaterializedViewName == "hier_create_daily");
        Assert.True(parentIndex < childIndex);
    }

    #endregion

    // ── Time-bucket column name ──

    #region Should_Drop_And_Recreate_When_TimeBucketColumnName_Changes

    private class BucketRenameRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BucketRenameHourly
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketRenameDefaultContext : DbContext
    {
        public DbSet<BucketRenameRaw> Metrics => Set<BucketRenameRaw>();
        public DbSet<BucketRenameHourly> HourlyMetrics => Set<BucketRenameHourly>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketRenameRaw>(entity =>
            {
                entity.ToTable("bucket_rename_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketRenameHourly>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<BucketRenameHourly, BucketRenameRaw>(
                        "bucket_rename_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class BucketRenameCustomContext : DbContext
    {
        public DbSet<BucketRenameRaw> Metrics => Set<BucketRenameRaw>();
        public DbSet<BucketRenameHourly> HourlyMetrics => Set<BucketRenameHourly>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketRenameRaw>(entity =>
            {
                entity.ToTable("bucket_rename_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketRenameHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<BucketRenameHourly, BucketRenameRaw>(
                        "bucket_rename_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .WithTimeBucketProperty(x => x.Bucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Drop_And_Recreate_When_TimeBucketColumnName_Changes()
    {
        using BucketRenameDefaultContext sourceContext = new();
        using BucketRenameCustomContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        DropContinuousAggregateOperation? dropOp = operations.OfType<DropContinuousAggregateOperation>().FirstOrDefault();
        CreateContinuousAggregateOperation? createOp = operations.OfType<CreateContinuousAggregateOperation>().FirstOrDefault();

        Assert.NotNull(dropOp);
        Assert.NotNull(createOp);
        Assert.Equal("bucket_rename_hourly", dropOp.MaterializedViewName);
        Assert.Equal("bucket_rename_hourly", createOp.MaterializedViewName);
        Assert.Equal("hour_start", createOp.TimeBucketColumnName);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_TimeBucketColumnName_Identical

    private class BucketStableRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BucketStableHourly
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketStableContext : DbContext
    {
        public DbSet<BucketStableRaw> Metrics => Set<BucketStableRaw>();
        public DbSet<BucketStableHourly> HourlyMetrics => Set<BucketStableHourly>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketStableRaw>(entity =>
            {
                entity.ToTable("bucket_stable_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketStableHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<BucketStableHourly, BucketStableRaw>(
                        "bucket_stable_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .WithTimeBucketProperty(x => x.Bucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_TimeBucketColumnName_Identical()
    {
        using BucketStableContext sourceContext = new();
        using BucketStableContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Cascade_Drop_And_Recreate_When_Parent_BucketColumnName_Changes

    private class BucketCascadeRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BucketCascadeHourly
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketCascadeDaily
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketCascadeInitialContext : DbContext
    {
        public DbSet<BucketCascadeRaw> Raw => Set<BucketCascadeRaw>();
        public DbSet<BucketCascadeHourly> Hourly => Set<BucketCascadeHourly>();
        public DbSet<BucketCascadeDaily> Daily => Set<BucketCascadeDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketCascadeRaw>(entity =>
            {
                entity.ToTable("bucket_cascade_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketCascadeHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<BucketCascadeHourly, BucketCascadeRaw>(
                        "bucket_cascade_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<BucketCascadeDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<BucketCascadeDaily, BucketCascadeHourly>(
                        "bucket_cascade_daily",
                        "1 day",
                        x => x.Bucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    private class BucketCascadeRenamedParentContext : DbContext
    {
        public DbSet<BucketCascadeRaw> Raw => Set<BucketCascadeRaw>();
        public DbSet<BucketCascadeHourly> Hourly => Set<BucketCascadeHourly>();
        public DbSet<BucketCascadeDaily> Daily => Set<BucketCascadeDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketCascadeRaw>(entity =>
            {
                entity.ToTable("bucket_cascade_raw");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketCascadeHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<BucketCascadeHourly, BucketCascadeRaw>(
                        "bucket_cascade_hourly",
                        "1 hour",
                        x => x.Timestamp)
                    .WithTimeBucketProperty(x => x.Bucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<BucketCascadeDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<BucketCascadeDaily, BucketCascadeHourly>(
                        "bucket_cascade_daily",
                        "1 day",
                        x => x.Bucket)
                    .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Cascade_Drop_And_Recreate_When_Parent_BucketColumnName_Changes()
    {
        using BucketCascadeInitialContext sourceContext = new();
        using BucketCascadeRenamedParentContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregateDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<DropContinuousAggregateOperation> drops = [.. operations.OfType<DropContinuousAggregateOperation>()];
        List<CreateContinuousAggregateOperation> creates = [.. operations.OfType<CreateContinuousAggregateOperation>()];
        Assert.Equal(2, drops.Count);
        Assert.Equal(2, creates.Count);

        int dropChildIndex = drops.FindIndex(op => op.MaterializedViewName == "bucket_cascade_daily");
        int dropParentIndex = drops.FindIndex(op => op.MaterializedViewName == "bucket_cascade_hourly");
        Assert.True(dropChildIndex < dropParentIndex);

        int createParentIndex = creates.FindIndex(op => op.MaterializedViewName == "bucket_cascade_hourly");
        int createChildIndex = creates.FindIndex(op => op.MaterializedViewName == "bucket_cascade_daily");
        Assert.True(createParentIndex < createChildIndex);

        CreateContinuousAggregateOperation parentCreate = creates[createParentIndex];
        Assert.Equal("hour_start", parentCreate.TimeBucketColumnName);
    }

    #endregion
}
