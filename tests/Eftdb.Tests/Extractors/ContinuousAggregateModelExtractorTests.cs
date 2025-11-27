using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that verify ContinuousAggregateModelExtractor correctly extracts continuous aggregate configurations
/// from EF Core models and converts them to CreateContinuousAggregateOperation objects.
/// </summary>
public class ContinuousAggregateModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Extract_Minimal_ContinuousAggregate

    private class MinimalSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MinimalContinuousAggregateContext : DbContext
    {
        public DbSet<MinimalSourceMetric> Metrics => Set<MinimalSourceMetric>();
        public DbSet<MinimalHourlyMetric> HourlyMetrics => Set<MinimalHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MinimalHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MinimalHourlyMetric, MinimalSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_Minimal_ContinuousAggregate()
    {
        using MinimalContinuousAggregateContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        CreateContinuousAggregateOperation operation = operations[0];
        Assert.Equal("hourly_metrics", operation.MaterializedViewName);
        Assert.Equal("Metrics", operation.ParentName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("1 hour", operation.TimeBucketWidth);
        Assert.Equal("Timestamp", operation.TimeBucketSourceColumn);
        Assert.True(operation.TimeBucketGroupBy);
        Assert.Null(operation.ChunkInterval);
        Assert.False(operation.WithNoData);
        Assert.False(operation.CreateGroupIndexes);
        Assert.False(operation.MaterializedOnly);
        Assert.Null(operation.WhereClause);
        Assert.Empty(operation.AggregateFunctions);
        Assert.Empty(operation.GroupByColumns);
    }

    #endregion

    #region Should_Return_Empty_When_No_ContinuousAggregates

    private class NoAggregateSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoContinuousAggregateContext : DbContext
    {
        public DbSet<NoAggregateSourceMetric> Metrics => Set<NoAggregateSourceMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoAggregateSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_ContinuousAggregates()
    {
        using NoContinuousAggregateContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_RelationalModel_Is_Null

    [Fact]
    public void Should_Return_Empty_When_RelationalModel_Is_Null()
    {
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(null)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Resolve_ParentName_To_TableName

    private class ParentNameSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ParentNameHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class ParentNameContext : DbContext
    {
        public DbSet<ParentNameSourceMetric> Metrics => Set<ParentNameSourceMetric>();
        public DbSet<ParentNameHourlyMetric> HourlyMetrics => Set<ParentNameHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParentNameSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ParentNameHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ParentNameHourlyMetric, ParentNameSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Resolve_ParentName_To_TableName()
    {
        using ParentNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("Metrics", operations[0].ParentName);
    }

    #endregion

    #region Should_Resolve_TimeBucketSourceColumn_With_Snake_Case_Convention

    private class SnakeCaseSourceMetric
    {
        public DateTime TimestampUtc { get; set; }
        public double Value { get; set; }
    }

    private class SnakeCaseHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class SnakeCaseContext : DbContext
    {
        public DbSet<SnakeCaseSourceMetric> Metrics => Set<SnakeCaseSourceMetric>();
        public DbSet<SnakeCaseHourlyMetric> HourlyMetrics => Set<SnakeCaseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SnakeCaseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.TimestampUtc);
            });

            modelBuilder.Entity<SnakeCaseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SnakeCaseHourlyMetric, SnakeCaseSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.TimestampUtc
                );
            });
        }
    }

    [Fact]
    public void Should_Resolve_TimeBucketSourceColumn_With_Snake_Case_Convention()
    {
        using SnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("timestamp_utc", operations[0].TimeBucketSourceColumn);
    }

    #endregion

    #region Should_Extract_ChunkInterval

    private class ChunkIntervalSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChunkIntervalHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class ChunkIntervalContext : DbContext
    {
        public DbSet<ChunkIntervalSourceMetric> Metrics => Set<ChunkIntervalSourceMetric>();
        public DbSet<ChunkIntervalHourlyMetric> HourlyMetrics => Set<ChunkIntervalHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ChunkIntervalHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ChunkIntervalHourlyMetric, ChunkIntervalSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "30 days"
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_ChunkInterval()
    {
        using ChunkIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("30 days", operations[0].ChunkInterval);
    }

    #endregion

    #region Should_Extract_WithNoData_True

    private class WithNoDataSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class WithNoDataHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class WithNoDataContext : DbContext
    {
        public DbSet<WithNoDataSourceMetric> Metrics => Set<WithNoDataSourceMetric>();
        public DbSet<WithNoDataHourlyMetric> HourlyMetrics => Set<WithNoDataHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WithNoDataSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<WithNoDataHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<WithNoDataHourlyMetric, WithNoDataSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).WithNoData();
            });
        }
    }

    [Fact]
    public void Should_Extract_WithNoData_True()
    {
        using WithNoDataContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.True(operations[0].WithNoData);
    }

    #endregion

    #region Should_Extract_CreateGroupIndexes_True

    private class CreateGroupIndexesSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateGroupIndexesHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class CreateGroupIndexesContext : DbContext
    {
        public DbSet<CreateGroupIndexesSourceMetric> Metrics => Set<CreateGroupIndexesSourceMetric>();
        public DbSet<CreateGroupIndexesHourlyMetric> HourlyMetrics => Set<CreateGroupIndexesHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateGroupIndexesSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CreateGroupIndexesHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CreateGroupIndexesHourlyMetric, CreateGroupIndexesSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).CreateGroupIndexes();
            });
        }
    }

    [Fact]
    public void Should_Extract_CreateGroupIndexes_True()
    {
        using CreateGroupIndexesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.True(operations[0].CreateGroupIndexes);
    }

    #endregion

    #region Should_Extract_MaterializedOnly_True

    private class MaterializedOnlySourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaterializedOnlyHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MaterializedOnlyContext : DbContext
    {
        public DbSet<MaterializedOnlySourceMetric> Metrics => Set<MaterializedOnlySourceMetric>();
        public DbSet<MaterializedOnlyHourlyMetric> HourlyMetrics => Set<MaterializedOnlyHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnlySourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MaterializedOnlyHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MaterializedOnlyHourlyMetric, MaterializedOnlySourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).MaterializedOnly();
            });
        }
    }

    [Fact]
    public void Should_Extract_MaterializedOnly_True()
    {
        using MaterializedOnlyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.True(operations[0].MaterializedOnly);
    }

    #endregion

    #region Should_Extract_TimeBucketGroupBy_False

    private class TimeBucketGroupByFalseSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimeBucketGroupByFalseHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class TimeBucketGroupByFalseContext : DbContext
    {
        public DbSet<TimeBucketGroupByFalseSourceMetric> Metrics => Set<TimeBucketGroupByFalseSourceMetric>();
        public DbSet<TimeBucketGroupByFalseHourlyMetric> HourlyMetrics => Set<TimeBucketGroupByFalseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeBucketGroupByFalseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<TimeBucketGroupByFalseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<TimeBucketGroupByFalseHourlyMetric, TimeBucketGroupByFalseSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    timeBucketGroupBy: false
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_TimeBucketGroupBy_False()
    {
        using TimeBucketGroupByFalseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.False(operations[0].TimeBucketGroupBy);
    }

    #endregion

    #region Should_Extract_WhereClause

    private class WhereClauseSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class WhereClauseHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class WhereClauseContext : DbContext
    {
        public DbSet<WhereClauseSourceMetric> Metrics => Set<WhereClauseSourceMetric>();
        public DbSet<WhereClauseHourlyMetric> HourlyMetrics => Set<WhereClauseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WhereClauseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<WhereClauseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<WhereClauseHourlyMetric, WhereClauseSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).Where("Value > 0");
            });
        }
    }

    [Fact]
    public void Should_Extract_WhereClause()
    {
        using WhereClauseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("Value > 0", operations[0].WhereClause);
    }

    #endregion

    #region Should_Extract_Single_AggregateFunction

    private class SingleAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SingleAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class SingleAggregateFunctionContext : DbContext
    {
        public DbSet<SingleAggregateFunctionSourceMetric> Metrics => Set<SingleAggregateFunctionSourceMetric>();
        public DbSet<SingleAggregateFunctionHourlyMetric> HourlyMetrics => Set<SingleAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SingleAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SingleAggregateFunctionHourlyMetric, SingleAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Value,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_Single_AggregateFunction()
    {
        using SingleAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("AvgValue:Avg:Value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_Multiple_AggregateFunctions

    private class MultipleAggregateFunctionsSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleAggregateFunctionsHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }

    private class MultipleAggregateFunctionsContext : DbContext
    {
        public DbSet<MultipleAggregateFunctionsSourceMetric> Metrics => Set<MultipleAggregateFunctionsSourceMetric>();
        public DbSet<MultipleAggregateFunctionsHourlyMetric> HourlyMetrics => Set<MultipleAggregateFunctionsHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleAggregateFunctionsSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleAggregateFunctionsHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultipleAggregateFunctionsHourlyMetric, MultipleAggregateFunctionsSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .AddAggregateFunction(x => x.MinValue, x => x.Value, EAggregateFunction.Min)
                 .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_AggregateFunctions()
    {
        using MultipleAggregateFunctionsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(3, operations[0].AggregateFunctions.Count);
        Assert.Contains("AvgValue:Avg:Value", operations[0].AggregateFunctions);
        Assert.Contains("MinValue:Min:Value", operations[0].AggregateFunctions);
        Assert.Contains("MaxValue:Max:Value", operations[0].AggregateFunctions);
    }

    #endregion

    #region Should_Resolve_AggregateFunction_Column_Names_With_Naming_Convention

    private class AggregateFunctionSnakeCaseSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double SensorValue { get; set; }
    }

    private class AggregateFunctionSnakeCaseHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AggregateFunctionSnakeCaseContext : DbContext
    {
        public DbSet<AggregateFunctionSnakeCaseSourceMetric> Metrics => Set<AggregateFunctionSnakeCaseSourceMetric>();
        public DbSet<AggregateFunctionSnakeCaseHourlyMetric> HourlyMetrics => Set<AggregateFunctionSnakeCaseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AggregateFunctionSnakeCaseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateFunctionSnakeCaseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateFunctionSnakeCaseHourlyMetric, AggregateFunctionSnakeCaseSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.SensorValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Resolve_AggregateFunction_Column_Names_With_Naming_Convention()
    {
        using AggregateFunctionSnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("avg_value:Avg:sensor_value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_Single_GroupByColumn_From_Expression

    private class SingleGroupByColumnSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class SingleGroupByColumnHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class SingleGroupByColumnContext : DbContext
    {
        public DbSet<SingleGroupByColumnSourceMetric> Metrics => Set<SingleGroupByColumnSourceMetric>();
        public DbSet<SingleGroupByColumnHourlyMetric> HourlyMetrics => Set<SingleGroupByColumnHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleGroupByColumnSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SingleGroupByColumnHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SingleGroupByColumnHourlyMetric, SingleGroupByColumnSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Extract_Single_GroupByColumn_From_Expression()
    {
        using SingleGroupByColumnContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].GroupByColumns);
        Assert.Equal("DeviceId", operations[0].GroupByColumns[0]);
    }

    #endregion

    #region Should_Extract_Multiple_GroupByColumns

    private class MultipleGroupByColumnsSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class MultipleGroupByColumnsHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    private class MultipleGroupByColumnsContext : DbContext
    {
        public DbSet<MultipleGroupByColumnsSourceMetric> Metrics => Set<MultipleGroupByColumnsSourceMetric>();
        public DbSet<MultipleGroupByColumnsHourlyMetric> HourlyMetrics => Set<MultipleGroupByColumnsHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleGroupByColumnsSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleGroupByColumnsHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultipleGroupByColumnsHourlyMetric, MultipleGroupByColumnsSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.DeviceId)
                 .AddGroupByColumn(x => x.Location);
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_GroupByColumns()
    {
        using MultipleGroupByColumnsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(2, operations[0].GroupByColumns.Count);
        Assert.Contains("DeviceId", operations[0].GroupByColumns);
        Assert.Contains("Location", operations[0].GroupByColumns);
    }

    #endregion

    #region Should_Extract_RawSQL_GroupByColumn

    private class RawSQLGroupBySourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RawSQLGroupByHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class RawSQLGroupByContext : DbContext
    {
        public DbSet<RawSQLGroupBySourceMetric> Metrics => Set<RawSQLGroupBySourceMetric>();
        public DbSet<RawSQLGroupByHourlyMetric> HourlyMetrics => Set<RawSQLGroupByHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RawSQLGroupBySourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RawSQLGroupByHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<RawSQLGroupByHourlyMetric, RawSQLGroupBySourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn("1, 2");
            });
        }
    }

    [Fact]
    public void Should_Extract_RawSQL_GroupByColumn()
    {
        using RawSQLGroupByContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].GroupByColumns);
        Assert.Equal("1, 2", operations[0].GroupByColumns[0]);
    }

    #endregion

    #region Should_Resolve_GroupByColumn_Names_With_Naming_Convention

    private class GroupBySnakeCaseSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class GroupBySnakeCaseHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class GroupBySnakeCaseContext : DbContext
    {
        public DbSet<GroupBySnakeCaseSourceMetric> Metrics => Set<GroupBySnakeCaseSourceMetric>();
        public DbSet<GroupBySnakeCaseHourlyMetric> HourlyMetrics => Set<GroupBySnakeCaseHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GroupBySnakeCaseSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<GroupBySnakeCaseHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<GroupBySnakeCaseHourlyMetric, GroupBySnakeCaseSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Resolve_GroupByColumn_Names_With_Naming_Convention()
    {
        using GroupBySnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].GroupByColumns);
        Assert.Equal("device_id", operations[0].GroupByColumns[0]);
    }

    #endregion

    #region Should_Extract_Multiple_ContinuousAggregates

    private class MultipleAggregatesSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleAggregatesHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MultipleAggregatesSourceEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultipleAggregatesDailyEvent
    {
        public DateTime Bucket { get; set; }
        public int EventCount { get; set; }
    }

    private class MultipleAggregatesContext : DbContext
    {
        public DbSet<MultipleAggregatesSourceMetric> Metrics => Set<MultipleAggregatesSourceMetric>();
        public DbSet<MultipleAggregatesHourlyMetric> HourlyMetrics => Set<MultipleAggregatesHourlyMetric>();
        public DbSet<MultipleAggregatesSourceEvent> Events => Set<MultipleAggregatesSourceEvent>();
        public DbSet<MultipleAggregatesDailyEvent> DailyEvents => Set<MultipleAggregatesDailyEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleAggregatesSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleAggregatesHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultipleAggregatesHourlyMetric, MultipleAggregatesSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
            });

            modelBuilder.Entity<MultipleAggregatesSourceEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleAggregatesDailyEvent>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultipleAggregatesDailyEvent, MultipleAggregatesSourceEvent>(
                    "daily_events",
                    "1 day",
                    x => x.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_ContinuousAggregates()
    {
        using MultipleAggregatesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, op => op.MaterializedViewName == "hourly_metrics");
        Assert.Contains(operations, op => op.MaterializedViewName == "daily_events");
    }

    #endregion

    #region Should_Extract_Fully_Configured_ContinuousAggregate

    private class FullyConfiguredSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class FullyConfiguredHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
    }

    private class FullyConfiguredContext : DbContext
    {
        public DbSet<FullyConfiguredSourceMetric> Metrics => Set<FullyConfiguredSourceMetric>();
        public DbSet<FullyConfiguredHourlyMetric> HourlyMetrics => Set<FullyConfiguredHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FullyConfiguredHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<FullyConfiguredHourlyMetric, FullyConfiguredSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp,
                    chunkInterval: "30 days"
                ).WithNoData()
                 .CreateGroupIndexes()
                 .MaterializedOnly()
                 .Where("Value > 0")
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .AddAggregateFunction(x => x.MinValue, x => x.Value, EAggregateFunction.Min)
                 .AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Extract_Fully_Configured_ContinuousAggregate()
    {
        using FullyConfiguredContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        CreateContinuousAggregateOperation operation = operations[0];
        Assert.Equal("hourly_metrics", operation.MaterializedViewName);
        Assert.Equal("Metrics", operation.ParentName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("1 hour", operation.TimeBucketWidth);
        Assert.Equal("Timestamp", operation.TimeBucketSourceColumn);
        Assert.True(operation.TimeBucketGroupBy);
        Assert.Equal("30 days", operation.ChunkInterval);
        Assert.True(operation.WithNoData);
        Assert.True(operation.CreateGroupIndexes);
        Assert.True(operation.MaterializedOnly);
        Assert.Equal("Value > 0", operation.WhereClause);
        Assert.Equal(2, operation.AggregateFunctions.Count);
        Assert.Single(operation.GroupByColumns);
    }

    #endregion

    #region Should_Extract_Sum_AggregateFunction

    private class SumAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SumAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double TotalValue { get; set; }
    }

    private class SumAggregateFunctionContext : DbContext
    {
        public DbSet<SumAggregateFunctionSourceMetric> Metrics => Set<SumAggregateFunctionSourceMetric>();
        public DbSet<SumAggregateFunctionHourlyMetric> HourlyMetrics => Set<SumAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SumAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SumAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SumAggregateFunctionHourlyMetric, SumAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.TotalValue, x => x.Value, EAggregateFunction.Sum);
            });
        }
    }

    [Fact]
    public void Should_Extract_Sum_AggregateFunction()
    {
        using SumAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("TotalValue:Sum:Value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_Count_AggregateFunction

    private class CountAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CountAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public long RecordCount { get; set; }
    }

    private class CountAggregateFunctionContext : DbContext
    {
        public DbSet<CountAggregateFunctionSourceMetric> Metrics => Set<CountAggregateFunctionSourceMetric>();
        public DbSet<CountAggregateFunctionHourlyMetric> HourlyMetrics => Set<CountAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CountAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CountAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CountAggregateFunctionHourlyMetric, CountAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.RecordCount, x => x.Value, EAggregateFunction.Count);
            });
        }
    }

    [Fact]
    public void Should_Extract_Count_AggregateFunction()
    {
        using CountAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("RecordCount:Count:Value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_First_AggregateFunction

    private class FirstAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FirstAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double FirstValue { get; set; }
    }

    private class FirstAggregateFunctionContext : DbContext
    {
        public DbSet<FirstAggregateFunctionSourceMetric> Metrics => Set<FirstAggregateFunctionSourceMetric>();
        public DbSet<FirstAggregateFunctionHourlyMetric> HourlyMetrics => Set<FirstAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FirstAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<FirstAggregateFunctionHourlyMetric, FirstAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.FirstValue, x => x.Value, EAggregateFunction.First);
            });
        }
    }

    [Fact]
    public void Should_Extract_First_AggregateFunction()
    {
        using FirstAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("FirstValue:First:Value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_Last_AggregateFunction

    private class LastAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LastAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double LastValue { get; set; }
    }

    private class LastAggregateFunctionContext : DbContext
    {
        public DbSet<LastAggregateFunctionSourceMetric> Metrics => Set<LastAggregateFunctionSourceMetric>();
        public DbSet<LastAggregateFunctionHourlyMetric> HourlyMetrics => Set<LastAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LastAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<LastAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<LastAggregateFunctionHourlyMetric, LastAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.LastValue, x => x.Value, EAggregateFunction.Last);
            });
        }
    }

    [Fact]
    public void Should_Extract_Last_AggregateFunction()
    {
        using LastAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("LastValue:Last:Value", operations[0].AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_Custom_Schema

    private class CustomSchemaSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomSchemaHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class CustomSchemaContext : DbContext
    {
        public DbSet<CustomSchemaSourceMetric> Metrics => Set<CustomSchemaSourceMetric>();
        public DbSet<CustomSchemaHourlyMetric> HourlyMetrics => Set<CustomSchemaHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomSchemaSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics", "custom_schema");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CustomSchemaHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CustomSchemaHourlyMetric, CustomSchemaSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Extract_Custom_Schema()
    {
        using CustomSchemaContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("custom_schema", operations[0].Schema);
    }

    #endregion

    #region Should_Extract_GroupByColumn_With_Explicit_Column_Name

    private class ExplicitGroupByColumnSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class ExplicitGroupByColumnHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class ExplicitGroupByColumnContext : DbContext
    {
        public DbSet<ExplicitGroupByColumnSourceMetric> Metrics => Set<ExplicitGroupByColumnSourceMetric>();
        public DbSet<ExplicitGroupByColumnHourlyMetric> HourlyMetrics => Set<ExplicitGroupByColumnHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExplicitGroupByColumnSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.Property(x => x.DeviceId).HasColumnName("device_identifier");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ExplicitGroupByColumnHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ExplicitGroupByColumnHourlyMetric, ExplicitGroupByColumnSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Extract_GroupByColumn_With_Explicit_Column_Name()
    {
        using ExplicitGroupByColumnContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].GroupByColumns);
        Assert.Equal("device_identifier", operations[0].GroupByColumns[0]);
    }

    #endregion

    #region Should_Extract_AggregateFunction_With_Explicit_Source_Column_Name

    private class ExplicitSourceColumnSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ExplicitSourceColumnHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ExplicitSourceColumnContext : DbContext
    {
        public DbSet<ExplicitSourceColumnSourceMetric> Metrics => Set<ExplicitSourceColumnSourceMetric>();
        public DbSet<ExplicitSourceColumnHourlyMetric> HourlyMetrics => Set<ExplicitSourceColumnHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExplicitSourceColumnSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.Property(x => x.Value).HasColumnName("sensor_value");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ExplicitSourceColumnHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ExplicitSourceColumnHourlyMetric, ExplicitSourceColumnSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Extract_AggregateFunction_With_Explicit_Source_Column_Name()
    {
        using ExplicitSourceColumnContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Single(operations);
        Assert.Single(operations[0].AggregateFunctions);
        Assert.Equal("AvgValue:Avg:sensor_value", operations[0].AggregateFunctions[0]);
    }

    #endregion
}
