using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration
{
    /// <summary>
    /// Tests continuous aggregates built from aggregate and group-by expressions against a real database.
    /// </summary>
    public class ContinuousAggregateExpressionIntegrationTests : MigrationTestBase, IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private string? _connectionString;

        public async ValueTask InitializeAsync()
        {
            _container = new PostgreSqlBuilder(TimescaleImages.Community)
                .WithDatabase("test_db")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        public async ValueTask DisposeAsync()
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }

        #region Should_Group_And_Aggregate_By_Expressions

        private class ExpressionEvent
        {
            public DateTime OccurredAt { get; set; }
            public string ActorId { get; set; } = string.Empty;
            public string? ImpersonatedActorId { get; set; }
            public short Action { get; set; }
        }

        private class ExpressionDaily
        {
            public DateTime Day { get; set; }
            public bool Impersonated { get; set; }
            public long Reads { get; set; }
            public bool AnyDeletes { get; set; }
        }

        private class ExpressionContext(string connectionString) : DbContext
        {
            public DbSet<ExpressionEvent> Events => Set<ExpressionEvent>();
            public DbSet<ExpressionDaily> Daily => Set<ExpressionDaily>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention().UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<ExpressionEvent>(entity =>
                {
                    entity.ToTable("expression_events");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.OccurredAt);
                });

                modelBuilder.Entity<ExpressionDaily>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<ExpressionDaily, ExpressionEvent>("expression_daily", "1 day", x => x.OccurredAt)
                        .WithTimeBucketProperty(x => x.Day)
                        .AddGroupByExpression(x => x.Impersonated, "{0} IS NOT NULL", x => x.ImpersonatedActorId)
                        .AddAggregateExpression(x => x.Reads, "count(*) FILTER (WHERE {0} = 1)", x => x.Action)
                        .AddAggregateExpression(x => x.AnyDeletes, "bool_or({0} = 4)", x => x.Action);
                });
            }
        }

        [Fact]
        public async Task Should_Group_And_Aggregate_By_Expressions()
        {
            // Arrange
            await using ExpressionContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);
            DateTime day = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO expression_events (occurred_at, actor_id, impersonated_actor_id, action)
                VALUES ({day}, {"a"}, NULL, {(short)1}), ({day}, {"a"}, NULL, {(short)1}), ({day}, {"b"}, {"p"}, {(short)4})
                """, TestContext.Current.CancellationToken);

            // Act
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.expression_daily', NULL, NULL);", [], TestContext.Current.CancellationToken);
            List<ExpressionDaily> rows = await context.Daily.OrderBy(x => x.Impersonated).ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Collection(
                rows,
                direct =>
                {
                    Assert.False(direct.Impersonated);
                    Assert.Equal(2, direct.Reads);
                    Assert.False(direct.AnyDeletes);
                },
                impersonated =>
                {
                    Assert.True(impersonated.Impersonated);
                    Assert.Equal(0, impersonated.Reads);
                    Assert.True(impersonated.AnyDeletes);
                });
        }

        #endregion
    }

    /// <summary>
    /// Tests continuous aggregates built from timescaledb_toolkit sketches, which need the toolkit image.
    /// </summary>
    [Trait("Category", TimescaleImages.ToolkitTrait)]
    public class ContinuousAggregateToolkitExpressionIntegrationTests : MigrationTestBase, IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private string? _connectionString;

        public async ValueTask InitializeAsync()
        {
            _container = new PostgreSqlBuilder(TimescaleImages.Toolkit)
                .WithDatabase("test_db")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        public async ValueTask DisposeAsync()
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }

        #region Should_Rank_With_Sketches_And_Roll_Them_Up

        private class SketchEvent
        {
            public DateTime OccurredAt { get; set; }
            public string ActorId { get; set; } = string.Empty;
        }

        private class SketchDaily
        {
            public DateTime Day { get; set; }
            public string Actors { get; set; } = string.Empty;
        }

        private class SketchWeekly
        {
            public DateTime Week { get; set; }
            public string Actors { get; set; } = string.Empty;
        }

        private class SketchContext(string connectionString) : DbContext
        {
            public DbSet<SketchEvent> Events => Set<SketchEvent>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention().UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.HasPostgresExtension("timescaledb_toolkit");

                modelBuilder.Entity<SketchEvent>(entity =>
                {
                    entity.ToTable("sketch_events");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.OccurredAt);
                });

                modelBuilder.Entity<SketchDaily>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<SketchDaily, SketchEvent>("sketch_daily", "1 day", x => x.OccurredAt)
                        .WithTimeBucketProperty(x => x.Day)
                        .AddAggregateExpression(x => x.Actors, "mcv_agg(5, {0})", x => x.ActorId);
                });

                modelBuilder.Entity<SketchWeekly>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<SketchWeekly, SketchDaily>("sketch_weekly", "7 days", x => x.Day)
                        .WithTimeBucketProperty(x => x.Week)
                        .AddAggregateExpression(x => x.Actors, "rollup({0})", x => x.Actors);
                });
            }
        }

        [Fact]
        public async Task Should_Rank_With_Sketches_And_Roll_Them_Up()
        {
            // Arrange
            await using SketchContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);
            DateTime monday = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
            DateTime tuesday = monday.AddDays(1);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sketch_events (occurred_at, actor_id)
                VALUES ({monday}, {"busy"}), ({monday}, {"busy"}), ({tuesday}, {"busy"}), ({tuesday}, {"quiet"})
                """, TestContext.Current.CancellationToken);

            // Act
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.sketch_daily', NULL, NULL);", [], TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.sketch_weekly', NULL, NULL);", [], TestContext.Current.CancellationToken);
            List<string> busiest = await context.Database
                .SqlQueryRaw<string>("SELECT topn(actors, 1) AS \"Value\" FROM sketch_weekly")
                .ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["busy"], busiest);
        }

        #endregion
    }
}
