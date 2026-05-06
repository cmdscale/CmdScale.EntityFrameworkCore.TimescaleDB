using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that verify ContinuousAggregatePolicyModelExtractor correctly extracts policy configurations
/// from EF Core models and converts them to AddContinuousAggregatePolicyOperation objects.
/// </summary>
public class ContinuousAggregatePolicyModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Extract_Policy_With_All_Parameters

    private class AllParamsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AllParamsAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AllParamsContext : DbContext
    {
        public DbSet<AllParamsMetric> Metrics => Set<AllParamsMetric>();
        public DbSet<AllParamsAggregate> Aggregates => Set<AllParamsAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllParamsMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AllParamsAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AllParamsAggregate, AllParamsMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "30 minutes")
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    .WithIfNotExists(true)
                    .WithIncludeTieredData(true)
                    .WithBucketsPerBatch(5)
                    .WithMaxBatchesPerExecution(10)
                    .WithRefreshNewestFirst(false);
            });
        }
    }

    [Fact]
    public void Should_Extract_Policy_With_All_Parameters()
    {
        using AllParamsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Single(operations);
        AddContinuousAggregatePolicyOperation op = operations[0];
        Assert.Equal("hourly_metrics", op.MaterializedViewName);
        Assert.Equal("7 days", op.StartOffset);
        Assert.Equal("1 hour", op.EndOffset);
        Assert.Equal("30 minutes", op.ScheduleInterval);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), op.InitialStart);
        Assert.True(op.IfNotExists);
        Assert.Equal(true, op.IncludeTieredData);
        Assert.Equal(5, op.BucketsPerBatch);
        Assert.Equal(10, op.MaxBatchesPerExecution);
        Assert.False(op.RefreshNewestFirst);
    }

    #endregion

    #region Should_Extract_Policy_With_Minimal_Parameters

    private class MinimalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MinimalPolicyContext : DbContext
    {
        public DbSet<MinimalMetric> Metrics => Set<MinimalMetric>();
        public DbSet<MinimalAggregate> Aggregates => Set<MinimalAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MinimalAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MinimalAggregate, MinimalMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Extract_Policy_With_Minimal_Parameters()
    {
        using MinimalPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Single(operations);
        AddContinuousAggregatePolicyOperation op = operations[0];
        Assert.Equal("hourly_metrics", op.MaterializedViewName);
        Assert.Equal("1 month", op.StartOffset);
        Assert.Equal("1 hour", op.EndOffset);
        Assert.Equal("1 hour", op.ScheduleInterval);
        Assert.Null(op.InitialStart);
        Assert.False(op.IfNotExists);
        Assert.Null(op.IncludeTieredData);
        Assert.Equal(1, op.BucketsPerBatch);
        Assert.Equal(0, op.MaxBatchesPerExecution);
        Assert.True(op.RefreshNewestFirst);
    }

    #endregion

    #region Should_Return_Empty_When_No_ContinuousAggregate_Annotation

    private class PlainMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoContinuousAggregateContext : DbContext
    {
        public DbSet<PlainMetric> Metrics => Set<PlainMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_ContinuousAggregate_Annotation()
    {
        using NoContinuousAggregateContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_No_HasRefreshPolicy_Annotation

    private class NoRefreshMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoRefreshAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoRefreshPolicyContext : DbContext
    {
        public DbSet<NoRefreshMetric> Metrics => Set<NoRefreshMetric>();
        public DbSet<NoRefreshAggregate> Aggregates => Set<NoRefreshAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoRefreshMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoRefreshAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<NoRefreshAggregate, NoRefreshMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
                // Note: No WithRefreshPolicy() call
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_HasRefreshPolicy_Annotation()
    {
        using NoRefreshPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_Null_Model

    [Fact]
    public void Should_Return_Empty_When_Null_Model()
    {
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(null)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Use_Parent_Entity_Schema

    private class SchemaMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SchemaAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ParentSchemaContext : DbContext
    {
        public DbSet<SchemaMetric> Metrics => Set<SchemaMetric>();
        public DbSet<SchemaAggregate> Aggregates => Set<SchemaAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SchemaMetric>(entity =>
            {
                entity.ToTable("Metrics", "telemetry");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<SchemaAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<SchemaAggregate, SchemaMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Use_Parent_Entity_Schema()
    {
        using ParentSchemaContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("telemetry", operations[0].Schema);
    }

    #endregion

    #region Should_Apply_Default_Values_For_Missing_Annotations

    private class DefaultMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DefaultValuesContext : DbContext
    {
        public DbSet<DefaultMetric> Metrics => Set<DefaultMetric>();
        public DbSet<DefaultAggregate> Aggregates => Set<DefaultAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DefaultAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<DefaultAggregate, DefaultMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                // No optional policy methods called
            });
        }
    }

    [Fact]
    public void Should_Apply_Default_Values_For_Missing_Annotations()
    {
        using DefaultValuesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Single(operations);
        AddContinuousAggregatePolicyOperation op = operations[0];
        Assert.False(op.IfNotExists);
        Assert.Equal(1, op.BucketsPerBatch);
        Assert.Equal(0, op.MaxBatchesPerExecution);
        Assert.True(op.RefreshNewestFirst);
    }

    #endregion

    #region Should_Extract_Policies_From_Multiple_Entities

    private class MultiMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiAggregate1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultiAggregate2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultiplePoliciesContext : DbContext
    {
        public DbSet<MultiMetric> Metrics => Set<MultiMetric>();
        public DbSet<MultiAggregate1> HourlyAggregates => Set<MultiAggregate1>();
        public DbSet<MultiAggregate2> DailyAggregates => Set<MultiAggregate2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultiAggregate1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultiAggregate1, MultiMetric>(
                        "hourly_metrics", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour");
            });

            modelBuilder.Entity<MultiAggregate2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MultiAggregate2, MultiMetric>(
                        "daily_metrics", "1 day", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 day", scheduleInterval: "1 day");
            });
        }
    }

    [Fact]
    public void Should_Extract_Policies_From_Multiple_Entities()
    {
        using MultiplePoliciesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, op => op.MaterializedViewName == "hourly_metrics");
        Assert.Contains(operations, op => op.MaterializedViewName == "daily_metrics");
    }

    #endregion

    #region Should_Extract_Policy_From_Attribute

    private class AttrMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "hourly_attr_metrics", ParentName = "Metrics")]
    [ContinuousAggregatePolicy(StartOffset = "7 days", EndOffset = "1 hour", ScheduleInterval = "1 hour", BucketsPerBatch = 3)]
    private class AttrAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AttributePolicyContext : DbContext
    {
        public DbSet<AttrMetric> Metrics => Set<AttrMetric>();
        public DbSet<AttrAggregate> Aggregates => Set<AttrAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttrMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AttrAggregate>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Extract_Policy_From_Attribute()
    {
        using AttributePolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        Assert.Single(operations);
        AddContinuousAggregatePolicyOperation op = operations[0];
        Assert.Equal("hourly_attr_metrics", op.MaterializedViewName);
        Assert.Equal("7 days", op.StartOffset);
        Assert.Equal("1 hour", op.EndOffset);
        Assert.Equal("1 hour", op.ScheduleInterval);
        Assert.Equal(3, op.BucketsPerBatch);
    }

    #endregion

    #region Should_Use_View_Schema_For_Policy_When_ToView_Specifies_Custom_Schema

    private class ViewSchemaPolicySourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ViewSchemaPolicyAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class ViewSchemaPolicyContext : DbContext
    {
        public DbSet<ViewSchemaPolicySourceMetric> Metrics => Set<ViewSchemaPolicySourceMetric>();
        public DbSet<ViewSchemaPolicyAggregate> Aggregates => Set<ViewSchemaPolicyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewSchemaPolicySourceMetric>(entity =>
            {
                entity.HasNoKey();
                // Parent in a different schema than the CA's view schema
                entity.ToTable("Metrics", "telemetry");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ViewSchemaPolicyAggregate>(entity =>
            {
                entity.HasNoKey();
                // CA mapped via .ToView with explicit custom schema
                entity.ToView("agg_view", "custom_schema");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(ViewSchemaPolicySourceMetric));
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "1 month");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Use_View_Schema_For_Policy_When_ToView_Specifies_Custom_Schema()
    {
        // Arrange
        using ViewSchemaPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Equal("custom_schema", op.Schema);
        Assert.Equal("agg_view", op.MaterializedViewName);
    }

    #endregion

    #region Should_Resolve_Parent_For_Policy_When_ParentName_Annotation_Holds_TableName_Not_ClrName

    private class TableNameParentLookupPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public int StatusCode { get; set; }
    }

    private class TableNameParentLookupPolicyAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class TableNameParentLookupPolicyContext : DbContext
    {
        // Note: CLR name "TableNameParentLookupPolicyMetric" but the table is "ApiRequestLogs".
        public DbSet<TableNameParentLookupPolicyMetric> Logs => Set<TableNameParentLookupPolicyMetric>();
        public DbSet<TableNameParentLookupPolicyAggregate> HourlyLogs => Set<TableNameParentLookupPolicyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TableNameParentLookupPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ApiRequestLogs", "telemetry");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<TableNameParentLookupPolicyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("hourly_api_logs", "telemetry");

                // Scaffolder writes the table name into ParentName, not the CLR / short name.
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "hourly_api_logs");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "ApiRequestLogs");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "1 month");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Parent_For_Policy_When_ParentName_Annotation_Holds_TableName_Not_ClrName()
    {
        // Arrange
        using TableNameParentLookupPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Equal("hourly_api_logs", op.MaterializedViewName);
        Assert.Equal("telemetry", op.Schema);
    }

    #endregion

    #region Should_Emit_Policy_When_ParentName_Annotation_Is_Missing

    private class NoParentNameMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoParentNameAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class NoParentNamePolicyContext : DbContext
    {
        public DbSet<NoParentNameMetric> Metrics => Set<NoParentNameMetric>();
        public DbSet<NoParentNameAggregate> Aggregates => Set<NoParentNameAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoParentNameMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoParentNameAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("agg_view", "custom_schema");

                // HasRefreshPolicy set, but ParentName annotation deliberately omitted
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "1 month");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Emit_Policy_When_ParentName_Annotation_Is_Missing()
    {
        // Arrange
        using NoParentNamePolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert: policy is still produced; schema falls through to the view's own schema
        // because the parent-lookup branch is short-circuited by the IsNullOrWhiteSpace guard.
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Equal("agg_view", op.MaterializedViewName);
        Assert.Equal("custom_schema", op.Schema);
    }

    #endregion

    #region Should_Emit_Policy_When_ParentName_Does_Not_Match_Any_Entity

    private class UnmatchedParentMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class UnmatchedParentAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class UnmatchedParentPolicyContext : DbContext
    {
        public DbSet<UnmatchedParentMetric> Metrics => Set<UnmatchedParentMetric>();
        public DbSet<UnmatchedParentAggregate> Aggregates => Set<UnmatchedParentAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnmatchedParentMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<UnmatchedParentAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("agg_view", "custom_schema");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                // ParentName matches nothing in the model — covers the path where all three
                // disjuncts in the FirstOrDefault predicate (CLR name / ShortName / table name)
                // return false for every entity.
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "DoesNotExist");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "1 month");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Emit_Policy_When_ParentName_Does_Not_Match_Any_Entity()
    {
        // Arrange
        using UnmatchedParentPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert: parent lookup yields null, schema falls through to the view's own schema.
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Equal("custom_schema", op.Schema);
    }

    #endregion

    #region Should_Emit_Policy_With_Null_Offsets_And_ScheduleInterval

    private class OptionalOffsetsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OptionalOffsetsAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class OptionalOffsetsPolicyContext : DbContext
    {
        public DbSet<OptionalOffsetsMetric> Metrics => Set<OptionalOffsetsMetric>();
        public DbSet<OptionalOffsetsAggregate> Aggregates => Set<OptionalOffsetsAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OptionalOffsetsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OptionalOffsetsAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("agg_view", "custom_schema");

                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(OptionalOffsetsMetric));
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
            });
        }
    }

    [Fact]
    public void Should_Emit_Policy_With_Null_Offsets_And_ScheduleInterval()
    {
        // Arrange
        using OptionalOffsetsPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert: optional fields stay null when their annotations are absent.
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Null(op.StartOffset);
        Assert.Null(op.EndOffset);
        Assert.Null(op.ScheduleInterval);
    }

    #endregion

    #region Should_Use_DefaultSchema_When_No_Schema_Sources_Available

    private class NoSchemaMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoSchemaAggregate
    {
        public DateTime Bucket { get; set; }
    }

    private class NoSchemaPolicyContext : DbContext
    {
        public DbSet<NoSchemaMetric> Metrics => Set<NoSchemaMetric>();
        public DbSet<NoSchemaAggregate> Aggregates => Set<NoSchemaAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Parent has no explicit schema.
            modelBuilder.Entity<NoSchemaMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoSchemaAggregate>(entity =>
            {
                entity.HasNoKey();
                // No .ToView / .ToTable — view schema and entity schema both null.
                // Combined with parent having no schema, the resolution chain falls
                // all the way through to DefaultValues.DefaultSchema.
                entity.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_view");
                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, nameof(NoSchemaMetric));
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "1 month");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Use_DefaultSchema_When_No_Schema_Sources_Available()
    {
        // Arrange
        using NoSchemaPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<AddContinuousAggregatePolicyOperation> operations =
            [.. ContinuousAggregatePolicyModelExtractor.GetContinuousAggregatePolicies(relationalModel)];

        // Assert: with no view schema, no entity schema, and no parent schema,
        // resolution falls through to DefaultValues.DefaultSchema ("public").
        AddContinuousAggregatePolicyOperation op = Assert.Single(operations);
        Assert.Equal("public", op.Schema);
    }

    #endregion
}
