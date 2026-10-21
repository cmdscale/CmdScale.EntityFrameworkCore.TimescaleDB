using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that aggregate and group-by expressions resolve against the model and produce the view SQL.
/// </summary>
public class ContinuousAggregateExpressionExtractorTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

    private static CreateContinuousAggregateOperation ExtractSingle(DbContext context)
        => Assert.Single(ContinuousAggregateModelExtractor.GetContinuousAggregates(
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel()));

    #region Should_Resolve_Expression_Columns_With_Naming_Conventions

    private class SnakeEvent
    {
        public DateTime OccurredAt { get; set; }
        public string TenantId { get; set; } = "";
        public string ActorId { get; set; } = "";
        public string? ImpersonatedActorId { get; set; }
    }

    private class SnakeRank
    {
        public DateTime Day { get; set; }
        public string TenantId { get; set; } = "";
        public bool Impersonated { get; set; }
        public long EventCount { get; set; }
        public string Actors { get; set; } = "";
    }

    private class SnakeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SnakeEvent>(e =>
            {
                e.HasNoKey();
                e.ToTable("audit_events");
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<SnakeRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<SnakeRank, SnakeEvent>("audit_actor_ranks_daily", "1 day", x => x.OccurredAt)
                    .WithTimeBucketProperty(x => x.Day)
                    .MaterializedOnly(false)
                    .AddGroupByColumn(x => x.TenantId)
                    .AddGroupByExpression(x => x.Impersonated, "{0} IS NOT NULL", x => x.ImpersonatedActorId)
                    .AddAggregateFunction(x => x.EventCount, x => x.ActorId, EAggregateFunction.Count)
                    .AddAggregateExpression(x => x.Actors, "mcv_agg(500, {0})", x => x.ActorId);
            });
        }
    }

    [Fact]
    public void Should_Resolve_Expression_Columns_With_Naming_Conventions()
    {
        // Arrange
        using SnakeContext context = new();

        // Act
        CreateContinuousAggregateOperation operation = ExtractSingle(context);

        // Assert
        Assert.Equal(new ContinuousAggregateExpression("actors", "mcv_agg(500, {0})", ["actor_id"]), Assert.Single(operation.AggregateExpressions));
        Assert.Equal(new ContinuousAggregateExpression("impersonated", "{0} IS NOT NULL", ["impersonated_actor_id"]), Assert.Single(operation.GroupByExpressions));
    }

    [Fact]
    public void Should_Select_And_Group_By_Expressions()
    {
        // Arrange
        using SnakeContext context = new();
        CreateContinuousAggregateOperation operation = ExtractSingle(context);

        // Act
        string sql = Assert.Single(ContinuousAggregateSqlGenerator.Generate(operation));

        // Assert
        Assert.Equal(
            SqlHelper.NormalizeSql("""
                CREATE MATERIALIZED VIEW "public"."audit_actor_ranks_daily"
                WITH (timescaledb.continuous, timescaledb.materialized_only = false) AS
                SELECT time_bucket('1 day', "occurred_at") AS "day", "tenant_id", "impersonated_actor_id" IS NOT NULL AS "impersonated", COUNT("actor_id") AS "event_count", mcv_agg(500, "actor_id") AS "actors"
                FROM "public"."audit_events"
                GROUP BY 1, "tenant_id", ("impersonated_actor_id" IS NOT NULL);
                """),
            SqlHelper.NormalizeSql(sql));
    }

    #endregion

    #region Should_Resolve_Rollup_Against_Parent_Aggregate

    private class RollupEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class RollupDaily
    {
        public DateTime Day { get; set; }
        public string Actors { get; set; } = "";
    }

    private class RollupWeekly
    {
        public DateTime Week { get; set; }
        public string Actors { get; set; } = "";
    }

    private class RollupContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RollupEvent>(e =>
            {
                e.HasNoKey();
                e.ToTable("rollup_events");
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<RollupDaily>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<RollupDaily, RollupEvent>("rollup_daily", "1 day", x => x.OccurredAt)
                    .WithTimeBucketProperty(x => x.Day)
                    .AddAggregateExpression(x => x.Actors, "mcv_agg(500, {0})", x => x.ActorId);
            });
            modelBuilder.Entity<RollupWeekly>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<RollupWeekly, RollupDaily>("rollup_weekly", "7 days", x => x.Day)
                    .WithTimeBucketProperty(x => x.Week)
                    .AddAggregateExpression(x => x.Actors, "rollup({0})", x => x.Actors);
            });
        }
    }

    [Fact]
    public void Should_Resolve_Rollup_Against_Parent_Aggregate()
    {
        // Arrange
        using RollupContext context = new();

        // Act
        List<CreateContinuousAggregateOperation> operations = [.. ContinuousAggregateModelExtractor.GetContinuousAggregates(
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel())];

        // Assert
        CreateContinuousAggregateOperation weekly = Assert.Single(operations, x => x.MaterializedViewName == "rollup_weekly");
        Assert.Equal("rollup_daily", weekly.ParentName);
        Assert.Equal(new ContinuousAggregateExpression("actors", "rollup({0})", ["actors"]), Assert.Single(weekly.AggregateExpressions));
    }

    #endregion

    #region Should_Throw_When_Expression_Column_Does_Not_Resolve

    private class MissingEvent
    {
        public DateTime OccurredAt { get; set; }
    }

    private class MissingRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
    }

    private class MissingContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<MissingRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate("missing_ranks", nameof(MissingEvent), "1 day", nameof(MissingEvent.OccurredAt))
                    .AddAggregateExpression(nameof(MissingRank.Actors), "mcv_agg(500, {0})", "NoSuchColumn");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Expression_Column_Does_Not_Resolve()
    {
        // Arrange
        using MissingContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => context.GetService<IDesignTimeModel>().Model);
        Assert.Contains("NoSuchColumn", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Expressions_Combine_With_Raw_View_Definition

    private class MixedEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class MixedRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
    }

    private class MixedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MixedEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<MixedRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregateFromSql("mixed_ranks", nameof(MixedEvent), "SELECT 1")
                    .AddAggregateExpression(nameof(MixedRank.Actors), "mcv_agg(500, {0})", nameof(MixedEvent.ActorId));
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Expressions_Combine_With_Raw_View_Definition()
    {
        // Arrange
        using MixedContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => context.GetService<IDesignTimeModel>().Model);
        Assert.Contains("raw view definition", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Expression_Alias_Collides

    private class CollideEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class CollideRank
    {
        public DateTime Bucket { get; set; }
        public long Actors { get; set; }
    }

    private class CollideContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollideEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<CollideRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CollideRank, CollideEvent>("collide_ranks", "1 day", x => x.OccurredAt)
                    .AddAggregateFunction(x => x.Actors, x => x.ActorId, EAggregateFunction.Count)
                    .AddAggregateExpression(x => x.Actors, "count(DISTINCT {0})", x => x.ActorId);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Expression_Alias_Collides()
    {
        // Arrange
        using CollideContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => context.GetService<IDesignTimeModel>().Model);
        Assert.Contains("more than once", exception.Message);
    }

    #endregion
}
