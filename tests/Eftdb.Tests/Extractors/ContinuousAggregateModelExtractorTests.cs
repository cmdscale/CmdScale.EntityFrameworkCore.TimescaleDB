using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.ComponentModel.DataAnnotations.Schema;

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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
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

        Assert.Equal("Metrics", Assert.Single(operations).ParentName);
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

        Assert.Equal("timestamp_utc", Assert.Single(operations).TimeBucketSourceColumn);
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

        Assert.Equal("30 days", Assert.Single(operations).ChunkInterval);
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

        Assert.True(Assert.Single(operations).WithNoData);
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

        Assert.True(Assert.Single(operations).CreateGroupIndexes);
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

        Assert.True(Assert.Single(operations).MaterializedOnly);
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

        Assert.False(Assert.Single(operations).TimeBucketGroupBy);
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

        Assert.Equal("Value > 0", Assert.Single(operations).WhereClause);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("AvgValue:Avg:Value", operation.AggregateFunctions[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Equal(3, operation.AggregateFunctions.Count);
        Assert.Contains("AvgValue:Avg:Value", operation.AggregateFunctions);
        Assert.Contains("MinValue:Min:Value", operation.AggregateFunctions);
        Assert.Contains("MaxValue:Max:Value", operation.AggregateFunctions);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("avg_value:Avg:sensor_value", operation.AggregateFunctions[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.GroupByColumns);
        Assert.Equal("DeviceId", operation.GroupByColumns[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Equal(2, operation.GroupByColumns.Count);
        Assert.Contains("DeviceId", operation.GroupByColumns);
        Assert.Contains("Location", operation.GroupByColumns);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.GroupByColumns);
        Assert.Equal("1, 2", operation.GroupByColumns[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.GroupByColumns);
        Assert.Equal("device_id", operation.GroupByColumns[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("TotalValue:Sum:Value", operation.AggregateFunctions[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("RecordCount:Count:Value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_CountStar_AggregateFunction

    private class CountStarSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_count_star_metrics", ParentName = nameof(CountStarSourceMetric))]
    [TimeBucket("1 hour", nameof(CountStarSourceMetric.Timestamp))]
    private class CountStarHourlyMetric
    {
        public DateTime Bucket { get; set; }

        [Aggregate(EAggregateFunction.Count, "*")]
        public long RecordCount { get; set; }
    }

    private class CountStarContext : DbContext
    {
        public DbSet<CountStarSourceMetric> Metrics => Set<CountStarSourceMetric>();
        public DbSet<CountStarHourlyMetric> HourlyMetrics => Set<CountStarHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CountStarSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("count_star_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CountStarHourlyMetric>(entity => entity.HasNoKey());
        }
    }

    [Fact]
    public void Should_Extract_CountStar_AggregateFunction()
    {
        using CountStarContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("RecordCount:Count:*", operation.AggregateFunctions[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("FirstValue:First:Value", operation.AggregateFunctions[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("LastValue:Last:Value", operation.AggregateFunctions[0]);
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

        Assert.Equal("custom_schema", Assert.Single(operations).Schema);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.GroupByColumns);
        Assert.Equal("device_identifier", operation.GroupByColumns[0]);
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

        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("AvgValue:Avg:sensor_value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Skip_When_MaterializedViewName_Is_Missing

    private class MissingViewNameSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class MissingViewNameHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MissingViewNameContext : DbContext
    {
        public DbSet<MissingViewNameSourceMetric> Metrics => Set<MissingViewNameSourceMetric>();
        public DbSet<MissingViewNameHourlyMetric> HourlyMetrics => Set<MissingViewNameHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingViewNameSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingViewNameHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MissingViewNameSourceMetric));
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, nameof(MissingViewNameSourceMetric.Timestamp));
            });
        }
    }

    [Fact]
    public void Should_Skip_When_MaterializedViewName_Is_Missing()
    {
        // Arrange
        using MissingViewNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_ParentName_Is_Missing

    private class MissingParentNameSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class MissingParentNameHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MissingParentNameContext : DbContext
    {
        public DbSet<MissingParentNameSourceMetric> Metrics => Set<MissingParentNameSourceMetric>();
        public DbSet<MissingParentNameHourlyMetric> HourlyMetrics => Set<MissingParentNameHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingParentNameSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingParentNameHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, nameof(MissingParentNameSourceMetric.Timestamp));
            });
        }
    }

    [Fact]
    public void Should_Skip_When_ParentName_Is_Missing()
    {
        // Arrange
        using MissingParentNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_ParentEntity_Not_Found

    private class ParentNotFoundSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class ParentNotFoundHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class ParentNotFoundContext : DbContext
    {
        public DbSet<ParentNotFoundSourceMetric> Metrics => Set<ParentNotFoundSourceMetric>();
        public DbSet<ParentNotFoundHourlyMetric> HourlyMetrics => Set<ParentNotFoundHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParentNotFoundSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ParentNotFoundHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.ParentName, "NonExistentEntity");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, nameof(ParentNotFoundSourceMetric.Timestamp));
            });
        }
    }

    [Fact]
    public void Should_Skip_When_ParentEntity_Not_Found()
    {
        // Arrange
        using ParentNotFoundContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_Parent_Has_No_Relational_Name

    private class NoRelationalNameSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class NoRelationalNameHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class NoRelationalNameContext : DbContext
    {
        public DbSet<NoRelationalNameSourceMetric> Metrics => Set<NoRelationalNameSourceMetric>();
        public DbSet<NoRelationalNameHourlyMetric> HourlyMetrics => Set<NoRelationalNameHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoRelationalNameSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable((string?)null);
            });

            modelBuilder.Entity<NoRelationalNameHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<NoRelationalNameHourlyMetric, NoRelationalNameSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Skip_When_Parent_Has_No_Relational_Name()
    {
        // Arrange
        using NoRelationalNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_TimeBucketWidth_Is_Missing

    private class MissingTimeBucketWidthSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class MissingTimeBucketWidthHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MissingTimeBucketWidthContext : DbContext
    {
        public DbSet<MissingTimeBucketWidthSourceMetric> Metrics => Set<MissingTimeBucketWidthSourceMetric>();
        public DbSet<MissingTimeBucketWidthHourlyMetric> HourlyMetrics => Set<MissingTimeBucketWidthHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingTimeBucketWidthSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingTimeBucketWidthHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MissingTimeBucketWidthSourceMetric));
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, nameof(MissingTimeBucketWidthSourceMetric.Timestamp));
            });
        }
    }

    [Fact]
    public void Should_Skip_When_TimeBucketWidth_Is_Missing()
    {
        // Arrange
        using MissingTimeBucketWidthContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_TimeBucketSourceColumn_Annotation_Is_Missing

    private class MissingTimeBucketSourceAnnotationSourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class MissingTimeBucketSourceAnnotationHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MissingTimeBucketSourceAnnotationContext : DbContext
    {
        public DbSet<MissingTimeBucketSourceAnnotationSourceMetric> Metrics => Set<MissingTimeBucketSourceAnnotationSourceMetric>();
        public DbSet<MissingTimeBucketSourceAnnotationHourlyMetric> HourlyMetrics => Set<MissingTimeBucketSourceAnnotationHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingTimeBucketSourceAnnotationSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingTimeBucketSourceAnnotationHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MissingTimeBucketSourceAnnotationSourceMetric));
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Skip_When_TimeBucketSourceColumn_Annotation_Is_Missing()
    {
        // Arrange
        using MissingTimeBucketSourceAnnotationContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_When_TimeBucketSourceColumn_Property_Not_Found

    private class MissingTimeBucketPropertySourceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class MissingTimeBucketPropertyHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class MissingTimeBucketPropertyContext : DbContext
    {
        public DbSet<MissingTimeBucketPropertySourceMetric> Metrics => Set<MissingTimeBucketPropertySourceMetric>();
        public DbSet<MissingTimeBucketPropertyHourlyMetric> HourlyMetrics => Set<MissingTimeBucketPropertyHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingTimeBucketPropertySourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingTimeBucketPropertyHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(MissingTimeBucketPropertySourceMetric));
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, "NonExistentColumn");
            });
        }
    }

    [Fact]
    public void Should_Skip_When_TimeBucketSourceColumn_Property_Not_Found()
    {
        // Arrange
        using MissingTimeBucketPropertyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_Malformed_AggregateFunction_String

    private class MalformedAggregateFunctionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MalformedAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MalformedAggregateFunctionContext : DbContext
    {
        public DbSet<MalformedAggregateFunctionSourceMetric> Metrics => Set<MalformedAggregateFunctionSourceMetric>();
        public DbSet<MalformedAggregateFunctionHourlyMetric> HourlyMetrics => Set<MalformedAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MalformedAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MalformedAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MalformedAggregateFunctionHourlyMetric, MalformedAggregateFunctionSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
                List<string> malformedList = ["AvgValue:Avg", "GoodValue:Sum:Value"];
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, malformedList);
            });
        }
    }

    [Fact]
    public void Should_Skip_Malformed_AggregateFunction_String()
    {
        // Arrange
        using MalformedAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("GoodValue:Sum:Value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Skip_AggregateFunction_When_SourceColumn_Not_Found

    private class MissingAggregateFunctionSourceColumnSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MissingAggregateFunctionSourceColumnHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
    }

    private class MissingAggregateFunctionSourceColumnContext : DbContext
    {
        public DbSet<MissingAggregateFunctionSourceColumnSourceMetric> Metrics => Set<MissingAggregateFunctionSourceColumnSourceMetric>();
        public DbSet<MissingAggregateFunctionSourceColumnHourlyMetric> HourlyMetrics => Set<MissingAggregateFunctionSourceColumnHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingAggregateFunctionSourceColumnSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingAggregateFunctionSourceColumnHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MissingAggregateFunctionSourceColumnHourlyMetric, MissingAggregateFunctionSourceColumnSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
                List<string> aggregateFunctions = [
                    "AvgValue:Avg:NonExistentColumn",
                    "MinValue:Min:Value"
                ];
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            });
        }
    }

    [Fact]
    public void Should_Skip_AggregateFunction_When_SourceColumn_Not_Found()
    {
        // Arrange
        using MissingAggregateFunctionSourceColumnContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("MinValue:Min:Value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Use_Model_Name_As_Alias_When_Property_Not_Found_In_Aggregate_Entity

    private class FallbackAliasSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FallbackAliasHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class FallbackAliasContext : DbContext
    {
        public DbSet<FallbackAliasSourceMetric> Metrics => Set<FallbackAliasSourceMetric>();
        public DbSet<FallbackAliasHourlyMetric> HourlyMetrics => Set<FallbackAliasHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FallbackAliasSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<FallbackAliasHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<FallbackAliasHourlyMetric, FallbackAliasSourceMetric>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                );
                List<string> aggregateFunctions = ["AvgValue:Avg:Value"];
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            });
        }
    }

    [Fact]
    public void Should_Use_Model_Name_As_Alias_When_Property_Not_Found_In_Aggregate_Entity()
    {
        // Arrange
        using FallbackAliasContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("AvgValue:Avg:Value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_ContinuousAggregate_When_TimeBucketSourceColumn_Annotation_Holds_Column_Name_Under_SnakeCase

    private class ScaffoldedTimeBucketSourceMetric
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldedTimeBucketHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class ScaffoldedTimeBucketContext : DbContext
    {
        public DbSet<ScaffoldedTimeBucketSourceMetric> Metrics => Set<ScaffoldedTimeBucketSourceMetric>();
        public DbSet<ScaffoldedTimeBucketHourlyMetric> HourlyMetrics => Set<ScaffoldedTimeBucketHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldedTimeBucketSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<ScaffoldedTimeBucketHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(ScaffoldedTimeBucketSourceMetric));
                entity.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, "time");
            });
        }
    }

    [Fact]
    public void Should_Extract_ContinuousAggregate_When_TimeBucketSourceColumn_Annotation_Holds_Column_Name_Under_SnakeCase()
    {
        // Arrange
        using ScaffoldedTimeBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Equal("time", Assert.Single(operations).TimeBucketSourceColumn);
    }

    #endregion

    #region Should_Extract_AggregateFunction_When_SourceColumnName_Annotation_Holds_Column_Name_Under_SnakeCase

    private class ScaffoldedAggregateFunctionSourceMetric
    {
        public DateTime Time { get; set; }
        public double SensorValue { get; set; }
    }

    private class ScaffoldedAggregateFunctionHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ScaffoldedAggregateFunctionContext : DbContext
    {
        public DbSet<ScaffoldedAggregateFunctionSourceMetric> Metrics => Set<ScaffoldedAggregateFunctionSourceMetric>();
        public DbSet<ScaffoldedAggregateFunctionHourlyMetric> HourlyMetrics => Set<ScaffoldedAggregateFunctionHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldedAggregateFunctionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("metrics");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<ScaffoldedAggregateFunctionHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(ScaffoldedAggregateFunctionSourceMetric));
                entity.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, "1 hour");
                entity.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, "time");

                List<string> aggregateFunctions = ["AvgValue:Avg:sensor_value"];
                entity.Metadata.SetAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            });
        }
    }

    [Fact]
    public void Should_Extract_AggregateFunction_When_SourceColumnName_Annotation_Holds_Column_Name_Under_SnakeCase()
    {
        // Arrange
        using ScaffoldedAggregateFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("avg_value:Avg:sensor_value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Extract_ContinuousAggregate_When_ViewDefinition_Is_Set_Without_StructuredFields

    private class RawDefinitionSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RawDefinitionAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class RawDefinitionContext : DbContext
    {
        public DbSet<RawDefinitionSourceMetric> Metrics => Set<RawDefinitionSourceMetric>();
        public DbSet<RawDefinitionAggregate> HourlyMetrics => Set<RawDefinitionAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RawDefinitionSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RawDefinitionAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(RawDefinitionSourceMetric));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"Metrics\" GROUP BY bucket;");
            });
        }
    }

    [Fact]
    public void Should_Extract_ContinuousAggregate_When_ViewDefinition_Is_Set_Without_StructuredFields()
    {
        // Arrange
        using RawDefinitionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.NotNull(operation.ViewDefinition);
        Assert.Contains("time_bucket('1 hour'", operation.ViewDefinition);
        Assert.Equal(string.Empty, operation.TimeBucketWidth);
        Assert.Equal(string.Empty, operation.TimeBucketSourceColumn);
        Assert.Equal("hourly_metrics", operation.MaterializedViewName);
    }

    #endregion

    #region Should_Skip_ContinuousAggregate_When_Neither_ViewDefinition_Nor_TimeBucketWidth_Is_Set

    private class NoStructuredOrRawSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoStructuredOrRawAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class NoStructuredOrRawContext : DbContext
    {
        public DbSet<NoStructuredOrRawSourceMetric> Metrics => Set<NoStructuredOrRawSourceMetric>();
        public DbSet<NoStructuredOrRawAggregate> HourlyMetrics => Set<NoStructuredOrRawAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoStructuredOrRawSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoStructuredOrRawAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_metrics");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(NoStructuredOrRawSourceMetric));
            });
        }
    }

    [Fact]
    public void Should_Skip_ContinuousAggregate_When_Neither_ViewDefinition_Nor_TimeBucketWidth_Is_Set()
    {
        // Arrange
        using NoStructuredOrRawContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Resolve_Parent_When_ParentName_Annotation_Holds_TableName_Not_ClrName

    private class ApiRequestLog
    {
        public DateTime Timestamp { get; set; }
        public int StatusCode { get; set; }
    }

    private class ApiRequestLogHourlyAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class TableNameParentLookupContext : DbContext
    {
        public DbSet<ApiRequestLog> Logs => Set<ApiRequestLog>();
        public DbSet<ApiRequestLogHourlyAggregate> HourlyLogs => Set<ApiRequestLogHourlyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiRequestLog>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ApiRequestLogs");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ApiRequestLogHourlyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_api_logs");
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_api_logs");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "ApiRequestLogs");
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"ApiRequestLogs\" GROUP BY bucket;");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Parent_When_ParentName_Annotation_Holds_TableName_Not_ClrName()
    {
        // Arrange
        using TableNameParentLookupContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Equal("ApiRequestLogs", operation.ParentName);
        Assert.Equal("hourly_api_logs", operation.MaterializedViewName);
    }

    #endregion

    #region Should_Use_View_Schema_When_ToView_Specifies_Custom_Schema

    private class ViewSchemaSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ViewSchemaAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class ViewSchemaContext : DbContext
    {
        public DbSet<ViewSchemaSourceMetric> Metrics => Set<ViewSchemaSourceMetric>();
        public DbSet<ViewSchemaAggregate> HourlyMetrics => Set<ViewSchemaAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewSchemaSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics", "telemetry");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ViewSchemaAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("agg_view", "custom_schema");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(ViewSchemaSourceMetric));
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"Timestamp\") AS bucket FROM \"telemetry\".\"Metrics\" GROUP BY bucket;");
            });
        }
    }

    [Fact]
    public void Should_Use_View_Schema_When_ToView_Specifies_Custom_Schema()
    {
        // Arrange
        using ViewSchemaContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Equal("custom_schema", operation.Schema);
    }

    #endregion

    // ── Complex-type support ──

    #region Should_Resolve_AggregateFunction_Source_Column_Inside_ComplexType

    [ComplexType]
    private class ComplexMeasurement1
    {
        public double Value { get; set; }
    }

    private class ComplexAggSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public ComplexMeasurement1 Param1 { get; set; } = new();
    }

    private class ComplexAggHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ComplexAggFunctionContext : DbContext
    {
        public DbSet<ComplexAggSourceMetric> Metrics => Set<ComplexAggSourceMetric>();
        public DbSet<ComplexAggHourlyMetric> HourlyMetrics => Set<ComplexAggHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexAggSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_agg_src_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ComplexAggHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ComplexAggHourlyMetric, ComplexAggSourceMetric>(
                    "complex_agg_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgValue,
                    x => x.Param1.Value,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public void Should_Resolve_AggregateFunction_Source_Column_Inside_ComplexType()
    {
        // Arrange
        using ComplexAggFunctionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("AvgValue:Avg:Param1_Value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Resolve_AggregateFunction_Source_Column_Inside_ComplexType_Under_SnakeCase

    [ComplexType]
    private class ComplexMeasurement2
    {
        public double SensorValue { get; set; }
    }

    private class ComplexAggSnakeSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public ComplexMeasurement2 Param1 { get; set; } = new();
    }

    private class ComplexAggSnakeHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public double AvgSensorValue { get; set; }
    }

    private class ComplexAggSnakeCaseContext : DbContext
    {
        public DbSet<ComplexAggSnakeSourceMetric> Metrics => Set<ComplexAggSnakeSourceMetric>();
        public DbSet<ComplexAggSnakeHourlyMetric> HourlyMetrics => Set<ComplexAggSnakeHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexAggSnakeSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_agg_snake_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ComplexAggSnakeHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ComplexAggSnakeHourlyMetric, ComplexAggSnakeSourceMetric>(
                    "complex_agg_snake_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(
                    x => x.AvgSensorValue,
                    x => x.Param1.SensorValue,
                    EAggregateFunction.Avg
                );
            });
        }
    }

    [Fact]
    public void Should_Resolve_AggregateFunction_Source_Column_Inside_ComplexType_Under_SnakeCase()
    {
        // Arrange
        using ComplexAggSnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.AggregateFunctions);
        Assert.Equal("avg_sensor_value:Avg:param1_sensor_value", operation.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Resolve_GroupBy_On_ComplexType_Member

    [ComplexType]
    private class ComplexMeasurement3
    {
        public string DeviceId { get; set; } = string.Empty;
    }

    private class ComplexGroupBySourceMetric
    {
        public DateTime Timestamp { get; set; }
        public ComplexMeasurement3 Param1 { get; set; } = new();
        public double Value { get; set; }
    }

    private class ComplexGroupByHourlyMetric
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class ComplexGroupByContext : DbContext
    {
        public DbSet<ComplexGroupBySourceMetric> Metrics => Set<ComplexGroupBySourceMetric>();
        public DbSet<ComplexGroupByHourlyMetric> HourlyMetrics => Set<ComplexGroupByHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexGroupBySourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_grp_by_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ComplexGroupByHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ComplexGroupByHourlyMetric, ComplexGroupBySourceMetric>(
                    "complex_grp_by_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.Param1.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Resolve_GroupBy_On_ComplexType_Member()
    {
        // Arrange
        using ComplexGroupByContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Single(operation.GroupByColumns);
        Assert.Equal("Param1_DeviceId", operation.GroupByColumns[0]);
    }

    #endregion

    #region Should_Resolve_TimeBucket_Source_Inside_ComplexType

    [ComplexType]
    private class ComplexMeasurement4
    {
        public DateTime Timestamp { get; set; }
    }

    private class ComplexTimeBucketSourceMetric
    {
        public double Value { get; set; }
        public ComplexMeasurement4 Meta { get; set; } = new();
    }

    private class ComplexTimeBucketHourlyMetric
    {
        public DateTime Bucket { get; set; }
    }

    private class ComplexTimeBucketContext : DbContext
    {
        public DbSet<ComplexTimeBucketSourceMetric> Metrics => Set<ComplexTimeBucketSourceMetric>();
        public DbSet<ComplexTimeBucketHourlyMetric> HourlyMetrics => Set<ComplexTimeBucketHourlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexTimeBucketSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_tb_src_metrics");
                entity.IsHypertable<ComplexTimeBucketSourceMetric, DateTime>(x => x.Meta.Timestamp);
            });

            modelBuilder.Entity<ComplexTimeBucketHourlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ComplexTimeBucketHourlyMetric, ComplexTimeBucketSourceMetric, DateTime>(
                    "complex_tb_hourly",
                    "1 hour",
                    x => x.Meta.Timestamp
                );
            });
        }
    }

    [Fact]
    public void Should_Resolve_TimeBucket_Source_Inside_ComplexType()
    {
        // Arrange
        using ComplexTimeBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        // Assert
        CreateContinuousAggregateOperation operation = Assert.Single(operations);
        Assert.Equal("Meta_Timestamp", operation.TimeBucketSourceColumn);
    }

    #endregion

    // ── Hierarchical continuous aggregates ──

    #region Should_Extract_Hierarchical_ContinuousAggregate

    private class HierarchicalProbeRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HierarchicalProbeHourly
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierarchicalProbeDaily
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class HierarchicalContext : DbContext
    {
        public DbSet<HierarchicalProbeRaw> ProbeRaw => Set<HierarchicalProbeRaw>();
        public DbSet<HierarchicalProbeHourly> ProbeHourly => Set<HierarchicalProbeHourly>();
        public DbSet<HierarchicalProbeDaily> ProbeDaily => Set<HierarchicalProbeDaily>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HierarchicalProbeRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("probe_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<HierarchicalProbeHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierarchicalProbeHourly, HierarchicalProbeRaw>(
                    "probe_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<HierarchicalProbeDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<HierarchicalProbeDaily, HierarchicalProbeHourly>(
                    "probe_daily",
                    "1 day",
                    x => x.TimeBucket
                ).AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Extract_Hierarchical_ContinuousAggregate()
    {
        using HierarchicalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal(2, operations.Count);

        CreateContinuousAggregateOperation daily = Assert.Single(operations, op => op.MaterializedViewName == "probe_daily");
        Assert.Equal("probe_hourly", daily.ParentName);
        Assert.Equal("time_bucket", daily.TimeBucketSourceColumn);
        Assert.Equal("1 day", daily.TimeBucketWidth);
        Assert.Single(daily.AggregateFunctions);
        Assert.Equal("AvgValue:Avg:AvgValue", daily.AggregateFunctions[0]);
    }

    #endregion

    #region Should_Order_Hierarchical_ContinuousAggregates_ParentFirst

    private class AlphaChildDaily
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BetaMiddleHourly
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class GammaRoot
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderingContext : DbContext
    {
        public DbSet<AlphaChildDaily> Daily => Set<AlphaChildDaily>();
        public DbSet<BetaMiddleHourly> Hourly => Set<BetaMiddleHourly>();
        public DbSet<GammaRoot> Raw => Set<GammaRoot>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GammaRoot>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("gamma_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BetaMiddleHourly>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<BetaMiddleHourly, GammaRoot>(
                    "beta_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<AlphaChildDaily>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<AlphaChildDaily, BetaMiddleHourly>(
                    "alpha_daily",
                    "1 day",
                    x => x.TimeBucket
                ).AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Order_Hierarchical_ContinuousAggregates_ParentFirst()
    {
        using OrderingContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        int hourlyIndex = operations.FindIndex(op => op.MaterializedViewName == "beta_hourly");
        int dailyIndex = operations.FindIndex(op => op.MaterializedViewName == "alpha_daily");

        Assert.True(hourlyIndex >= 0);
        Assert.True(dailyIndex >= 0);
        Assert.True(hourlyIndex < dailyIndex);
    }

    #endregion

    // ── Time-bucket target property ──

    #region Should_Default_TimeBucketColumnName_When_Undesignated

    private class UndesignatedBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class UndesignatedBucketAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class UndesignatedBucketContext : DbContext
    {
        public DbSet<UndesignatedBucketSource> Metrics => Set<UndesignatedBucketSource>();
        public DbSet<UndesignatedBucketAggregate> HourlyMetrics => Set<UndesignatedBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UndesignatedBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<UndesignatedBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<UndesignatedBucketAggregate, UndesignatedBucketSource>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Default_TimeBucketColumnName_When_Undesignated()
    {
        using UndesignatedBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal("time_bucket", Assert.Single(operations).TimeBucketColumnName);
    }

    #endregion

    #region Should_Resolve_Designated_BucketProperty_To_Explicit_ColumnName

    private class ExplicitBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ExplicitBucketAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ExplicitBucketContext : DbContext
    {
        public DbSet<ExplicitBucketSource> Metrics => Set<ExplicitBucketSource>();
        public DbSet<ExplicitBucketAggregate> HourlyMetrics => Set<ExplicitBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExplicitBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ExplicitBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<ExplicitBucketAggregate, ExplicitBucketSource>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).WithTimeBucketProperty(x => x.Bucket)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Resolve_Designated_BucketProperty_To_Explicit_ColumnName()
    {
        using ExplicitBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal("hour_start", Assert.Single(operations).TimeBucketColumnName);
    }

    #endregion

    #region Should_Resolve_Designated_BucketProperty_With_Naming_Convention

    private class ConventionBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ConventionBucketAggregate
    {
        public DateTime HourStart { get; set; }
        public double AvgValue { get; set; }
    }

    private class ConventionBucketContext : DbContext
    {
        public DbSet<ConventionBucketSource> Metrics => Set<ConventionBucketSource>();
        public DbSet<ConventionBucketAggregate> HourlyMetrics => Set<ConventionBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConventionBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ConventionBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ConventionBucketAggregate, ConventionBucketSource>(
                    "hourly_metrics",
                    "1 hour",
                    x => x.Timestamp
                ).WithTimeBucketProperty(x => x.HourStart)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Resolve_Designated_BucketProperty_With_Naming_Convention()
    {
        using ConventionBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal("hour_start", Assert.Single(operations).TimeBucketColumnName);
    }

    #endregion

    #region Should_Designate_BucketProperty_Via_Property_Attribute

    private class AttributeBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_attr_metrics", ParentName = nameof(AttributeBucketSource))]
    private class AttributeBucketAggregate
    {
        [TimeBucket("1 hour", nameof(AttributeBucketSource.Timestamp))]
        [Column("hour_start")]
        public DateTime Bucket { get; set; }

        [Aggregate(EAggregateFunction.Avg, nameof(AttributeBucketSource.Value))]
        public double AvgValue { get; set; }
    }

    private class AttributeBucketContext : DbContext
    {
        public DbSet<AttributeBucketSource> Metrics => Set<AttributeBucketSource>();
        public DbSet<AttributeBucketAggregate> HourlyMetrics => Set<AttributeBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttributeBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("attr_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AttributeBucketAggregate>(entity => entity.HasNoKey());
        }
    }

    [Fact]
    public void Should_Designate_BucketProperty_Via_Property_Attribute()
    {
        using AttributeBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal("hour_start", Assert.Single(operations).TimeBucketColumnName);
    }

    #endregion

    #region Should_Designate_BucketProperty_Via_StringBuilder

    private class StringBuilderBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderBucketAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StringBuilderBucketContext : DbContext
    {
        public DbSet<StringBuilderBucketSource> Metrics => Set<StringBuilderBucketSource>();
        public DbSet<StringBuilderBucketAggregate> HourlyMetrics => Set<StringBuilderBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<StringBuilderBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<StringBuilderBucketAggregate>(
                    "hourly_metrics",
                    "Metrics",
                    "1 hour",
                    "Timestamp"
                ).WithTimeBucketProperty("Bucket")
                 .AddAggregateFunction("avg_value", "Value", EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Designate_BucketProperty_Via_StringBuilder()
    {
        using StringBuilderBucketContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(relationalModel)];

        Assert.Equal("hour_start", Assert.Single(operations).TimeBucketColumnName);
    }

    #endregion
}
