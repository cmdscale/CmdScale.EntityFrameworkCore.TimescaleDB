using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests the annotations written by AddAggregateExpression, AddGroupByExpression, their attributes and
/// IsContinuousAggregateFromSql.
/// </summary>
public class ContinuousAggregateExpressionBuilderTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

    private static IEntityType GetEntityType<TEntity>(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))!;

    #region Should_Store_Expressions_With_Property_Names

    private class TypedEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
        public string? ImpersonatedActorId { get; set; }
    }

    private class TypedRank
    {
        public DateTime Day { get; set; }
        public bool Impersonated { get; set; }
        public string Actors { get; set; } = "";
    }

    private class TypedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TypedEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<TypedRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<TypedRank, TypedEvent>("typed_ranks", "1 day", x => x.OccurredAt)
                    .WithTimeBucketProperty(x => x.Day)
                    .AddGroupByExpression(x => x.Impersonated, "{0} IS NOT NULL", x => x.ImpersonatedActorId)
                    .AddAggregateExpression(x => x.Actors, "mcv_agg(500, {0})", x => x.ActorId);
            });
        }
    }

    [Fact]
    public void Should_Store_Expressions_With_Property_Names()
    {
        // Arrange
        using TypedContext context = new();

        // Act
        IEntityType entityType = GetEntityType<TypedRank>(context);

        // Assert
        ContinuousAggregateExpression aggregate = Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.AggregateExpressions));
        Assert.Equal(new ContinuousAggregateExpression("Actors", "mcv_agg(500, {0})", ["ActorId"]), aggregate);
        ContinuousAggregateExpression groupBy = Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.GroupByExpressions));
        Assert.Equal(new ContinuousAggregateExpression("Impersonated", "{0} IS NOT NULL", ["ImpersonatedActorId"]), groupBy);
    }

    #endregion

    #region Should_Replace_Expression_With_Same_Alias

    private class ReplaceEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class ReplaceRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
    }

    private class ReplaceContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReplaceEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<ReplaceRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<ReplaceRank, ReplaceEvent>("replace_ranks", "1 day", x => x.OccurredAt)
                    .AddAggregateExpression(x => x.Actors, "mcv_agg(10, {0})", x => x.ActorId)
                    .AddAggregateExpression(x => x.Actors, "mcv_agg(20, {0})", x => x.ActorId);
            });
        }
    }

    [Fact]
    public void Should_Replace_Expression_With_Same_Alias()
    {
        // Arrange
        using ReplaceContext context = new();

        // Act
        IEntityType entityType = GetEntityType<ReplaceRank>(context);

        // Assert
        ContinuousAggregateExpression aggregate = Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.AggregateExpressions));
        Assert.Equal("mcv_agg(20, {0})", aggregate.Sql);
    }

    #endregion

    #region Should_Throw_When_Placeholders_And_Columns_Do_Not_Match

    private class MismatchEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
        public string EntityId { get; set; } = "";
    }

    private class MismatchRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
    }

    private class MismatchContext(string sql, bool secondColumn) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).EnableServiceProviderCaching(false).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MismatchEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<MismatchRank>(e =>
            {
                e.HasNoKey();
                ContinuousAggregateBuilder<MismatchRank, MismatchEvent> builder =
                    e.IsContinuousAggregate<MismatchRank, MismatchEvent>("mismatch_ranks", "1 day", x => x.OccurredAt);
                if (secondColumn)
                {
                    builder.AddAggregateExpression(x => x.Actors, sql, x => x.ActorId, x => x.EntityId);
                }
                else
                {
                    builder.AddAggregateExpression(x => x.Actors, sql, x => x.ActorId);
                }
            });
        }
    }

    [Theory]
    [InlineData("mcv_agg(500, {1})", false)]
    [InlineData("mcv_agg(500, {0})", true)]
    [InlineData("mcv_agg(500, {actor})", false)]
    public void Should_Throw_When_Placeholders_And_Columns_Do_Not_Match(string sql, bool secondColumn)
    {
        // Arrange
        using MismatchContext context = new(sql, secondColumn);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => GetEntityType<MismatchRank>(context));
    }

    #endregion

    #region Should_Store_Expressions_From_String_Builder

    private class StringEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class StringRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
        public bool Late { get; set; }
    }

    private class StringContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<StringRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate("string_ranks", nameof(StringEvent), "1 day", nameof(StringEvent.OccurredAt))
                    .AddGroupByExpression(nameof(StringRank.Late), "{0} < now() - interval '1 day'", nameof(StringEvent.OccurredAt))
                    .AddAggregateExpression(nameof(StringRank.Actors), "mcv_agg(500, {0})", nameof(StringEvent.ActorId));
            });
        }
    }

    [Fact]
    public void Should_Store_Expressions_From_String_Builder()
    {
        // Arrange
        using StringContext context = new();

        // Act
        IEntityType entityType = GetEntityType<StringRank>(context);

        // Assert
        Assert.Equal("Actors", Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.AggregateExpressions)).Alias);
        Assert.Equal("Late", Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.GroupByExpressions)).Alias);
    }

    #endregion

    #region Should_Store_Expressions_From_Attributes

    private class AttributeEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
        public string? ImpersonatedActorId { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "attribute_ranks", ParentName = nameof(AttributeEvent))]
    [TimeBucket("1 day", nameof(AttributeEvent.OccurredAt))]
    private class AttributeRank
    {
        public DateTime Bucket { get; set; }

        [GroupByExpression("{0} IS NOT NULL", nameof(AttributeEvent.ImpersonatedActorId))]
        public bool Impersonated { get; set; }

        [AggregateExpression("mcv_agg(500, {0})", nameof(AttributeEvent.ActorId))]
        public string Actors { get; set; } = "";
    }

    private class AttributeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttributeEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<AttributeRank>().HasNoKey();
        }
    }

    [Fact]
    public void Should_Store_Expressions_From_Attributes()
    {
        // Arrange
        using AttributeContext context = new();

        // Act
        IEntityType entityType = GetEntityType<AttributeRank>(context);

        // Assert
        Assert.Equal(
            new ContinuousAggregateExpression("Actors", "mcv_agg(500, {0})", ["ActorId"]),
            Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.AggregateExpressions)));
        Assert.Equal(
            new ContinuousAggregateExpression("Impersonated", "{0} IS NOT NULL", ["ImpersonatedActorId"]),
            Assert.Single(ContinuousAggregateExpressionJson.Read(entityType, ContinuousAggregateAnnotations.GroupByExpressions)));
    }

    #endregion

    #region Should_Store_Raw_View_Definition

    private class RawEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
    }

    private class RawRank
    {
        public DateTime Bucket { get; set; }
        public string Actors { get; set; } = "";
    }

    private class RawContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RawEvent>(e =>
            {
                e.HasNoKey();
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<RawRank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregateFromSql(
                        "raw_ranks",
                        nameof(RawEvent),
                        """SELECT time_bucket('1 day', "OccurredAt") AS "Bucket", mcv_agg(5, "ActorId") AS "Actors" FROM "RawEvent" GROUP BY 1""")
                    .MaterializedOnly(false);
            });
        }
    }

    [Fact]
    public void Should_Store_Raw_View_Definition()
    {
        // Arrange
        using RawContext context = new();

        // Act
        IEntityType entityType = GetEntityType<RawRank>(context);

        // Assert
        Assert.Equal("raw_ranks", entityType.GetViewName());
        Assert.StartsWith("SELECT time_bucket", entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string);
        Assert.Null(entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth));
    }

    #endregion
}
