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

/// <summary>
/// Tests that changes to aggregate and group-by expressions recreate the continuous aggregate.
/// </summary>
public class ContinuousAggregateExpressionDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

    private class RankEvent
    {
        public DateTime OccurredAt { get; set; }
        public string ActorId { get; set; } = "";
        public string? ImpersonatedActorId { get; set; }
    }

    private class Rank
    {
        public DateTime Bucket { get; set; }
        public bool Impersonated { get; set; }
        public string Actors { get; set; } = "";
    }

    private class RankContext(string aggregateSql, string groupBySql) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                             .EnableServiceProviderCaching(false)
                             .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RankEvent>(e =>
            {
                e.HasNoKey();
                e.ToTable("expression_diff_events");
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<Rank>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<Rank, RankEvent>("expression_diff_ranks", "1 day", x => x.OccurredAt)
                    .AddGroupByExpression(x => x.Impersonated, groupBySql, x => x.ImpersonatedActorId)
                    .AddAggregateExpression(x => x.Actors, aggregateSql, x => x.ActorId);
            });
        }
    }

    #region Should_Not_Diff_Unchanged_Expressions

    [Fact]
    public void Should_Not_Diff_Unchanged_Expressions()
    {
        // Arrange
        using RankContext source = new("mcv_agg(500, {0})", "{0} IS NOT NULL");
        using RankContext target = new("mcv_agg(500, {0})", "{0} IS NOT NULL");

        // Act
        IReadOnlyList<MigrationOperation> operations = new ContinuousAggregateDiffer().GetDifferences(GetModel(source), GetModel(target));

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Recreate_When_Aggregate_Expression_Changes

    [Fact]
    public void Should_Recreate_When_Aggregate_Expression_Changes()
    {
        // Arrange
        using RankContext source = new("mcv_agg(500, {0})", "{0} IS NOT NULL");
        using RankContext target = new("mcv_agg(50, 2.0, {0})", "{0} IS NOT NULL");

        // Act
        IReadOnlyList<MigrationOperation> operations = new ContinuousAggregateDiffer().GetDifferences(GetModel(source), GetModel(target));

        // Assert
        Assert.Single(operations.OfType<DropContinuousAggregateOperation>());
        CreateContinuousAggregateOperation create = Assert.Single(operations.OfType<CreateContinuousAggregateOperation>());
        Assert.Equal("mcv_agg(50, 2.0, {0})", Assert.Single(create.AggregateExpressions).Sql);
    }

    #endregion

    #region Should_Recreate_When_GroupBy_Expression_Changes

    [Fact]
    public void Should_Recreate_When_GroupBy_Expression_Changes()
    {
        // Arrange
        using RankContext source = new("mcv_agg(500, {0})", "{0} IS NOT NULL");
        using RankContext target = new("mcv_agg(500, {0})", "{0} IS NULL");

        // Act
        IReadOnlyList<MigrationOperation> operations = new ContinuousAggregateDiffer().GetDifferences(GetModel(source), GetModel(target));

        // Assert
        Assert.Single(operations.OfType<DropContinuousAggregateOperation>());
        Assert.Single(operations.OfType<CreateContinuousAggregateOperation>());
    }

    #endregion
}
