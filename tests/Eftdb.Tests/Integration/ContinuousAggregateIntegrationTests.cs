using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration
{
    public class ContinuousAggregateIntegrationTests : MigrationTestBase, IAsyncLifetime
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

        #region Should_Create_ContinuousAggregate_With_BasicAggregates

        private class BasicAggregatesTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class BasicAggregatesAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
            public decimal MaxPrice { get; set; }
            public decimal MinPrice { get; set; }
            public decimal SumPrice { get; set; }
            public long CountPrice { get; set; }
        }

        private class BasicAggregatesContext(string connectionString) : DbContext
        {
            public DbSet<BasicAggregatesTrade> Trades => Set<BasicAggregatesTrade>();
            public DbSet<BasicAggregatesAggregate> TradeAggregates => Set<BasicAggregatesAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<BasicAggregatesTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<BasicAggregatesAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<BasicAggregatesAggregate, BasicAggregatesTrade>(
                            "trade_aggregate_basic",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
                        .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
                        .AddAggregateFunction(x => x.SumPrice, x => x.Price, EAggregateFunction.Sum)
                        .AddAggregateFunction(x => x.CountPrice, x => x.Price, EAggregateFunction.Count);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                    entity.Property(x => x.MaxPrice).HasColumnName("MaxPrice");
                    entity.Property(x => x.MinPrice).HasColumnName("MinPrice");
                    entity.Property(x => x.SumPrice).HasColumnName("SumPrice");
                    entity.Property(x => x.CountPrice).HasColumnName("CountPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_BasicAggregates()
        {
            await using BasicAggregatesContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_basic', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<BasicAggregatesAggregate> aggregates = await context.TradeAggregates
                .OrderBy(a => a.TimeBucket)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(aggregates);
            BasicAggregatesAggregate firstAggregate = aggregates.First();
            Assert.True(firstAggregate.AvgPrice > 0);
            Assert.True(firstAggregate.MaxPrice >= firstAggregate.MinPrice);
            Assert.True(firstAggregate.SumPrice > 0);
            Assert.True(firstAggregate.CountPrice > 0);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_FirstAndLast_Functions

        private class FirstLastTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class FirstLastAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal FirstPrice { get; set; }
            public decimal LastPrice { get; set; }
        }

        private class FirstLastContext(string connectionString) : DbContext
        {
            public DbSet<FirstLastTrade> Trades => Set<FirstLastTrade>();
            public DbSet<FirstLastAggregate> TradeAggregates => Set<FirstLastAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<FirstLastTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<FirstLastAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<FirstLastAggregate, FirstLastTrade>(
                            "trade_aggregate_first_last",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.FirstPrice, x => x.Price, EAggregateFunction.First)
                        .AddAggregateFunction(x => x.LastPrice, x => x.Price, EAggregateFunction.Last);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.FirstPrice).HasColumnName("FirstPrice");
                    entity.Property(x => x.LastPrice).HasColumnName("LastPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_FirstAndLast_Functions()
        {
            await using FirstLastContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {105.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {103.00m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_first_last', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<FirstLastAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            FirstLastAggregate aggregate = Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregate.FirstPrice);
            Assert.Equal(103.00m, aggregate.LastPrice);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_GroupByColumns

        private class GroupByTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class GroupByAggregate
        {
            public DateTime TimeBucket { get; set; }
            public string Exchange { get; set; } = string.Empty;
            public decimal AvgPrice { get; set; }
        }

        private class GroupByContext(string connectionString) : DbContext
        {
            public DbSet<GroupByTrade> Trades => Set<GroupByTrade>();
            public DbSet<GroupByAggregate> TradeAggregates => Set<GroupByAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<GroupByTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<GroupByAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<GroupByAggregate, GroupByTrade>(
                            "trade_aggregate_grouped",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_GroupByColumns()
        {
            await using GroupByContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {110.00m}, {200}, {"NASDAQ"}),
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {105.00m}, {150}, {"LSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_grouped', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<GroupByAggregate> aggregates = await context.TradeAggregates
                .OrderBy(a => a.Exchange)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(3, aggregates.Count);
            Assert.Equal("LSE", aggregates[0].Exchange);
            Assert.Equal(105.00m, aggregates[0].AvgPrice);
            Assert.Equal("NASDAQ", aggregates[1].Exchange);
            Assert.Equal(110.00m, aggregates[1].AvgPrice);
            Assert.Equal("NYSE", aggregates[2].Exchange);
            Assert.Equal(100.00m, aggregates[2].AvgPrice);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_WhereClause

        private class WhereClauseTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class WhereClauseAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class WhereClauseContext(string connectionString) : DbContext
        {
            public DbSet<WhereClauseTrade> Trades => Set<WhereClauseTrade>();
            public DbSet<WhereClauseAggregate> TradeAggregates => Set<WhereClauseAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<WhereClauseTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseAggregate, WhereClauseTrade>(
                            "trade_aggregate_filtered",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .Where("\"Ticker\" = 'AAPL'");

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_WhereClause()
        {
            await using WhereClauseContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"TSLA"}, {200.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"MSFT"}, {300.00m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_filtered', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<WhereClauseAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(100.00m, Assert.Single(aggregates).AvgPrice);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_WithNoData_Option

        private class WithNoDataTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class WithNoDataAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class WithNoDataContext(string connectionString) : DbContext
        {
            public DbSet<WithNoDataTrade> Trades => Set<WithNoDataTrade>();
            public DbSet<WithNoDataAggregate> TradeAggregates => Set<WithNoDataAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<WithNoDataTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WithNoDataAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WithNoDataAggregate, WithNoDataTrade>(
                            "trade_aggregate_no_data",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .WithNoData(true)
                        .MaterializedOnly(true);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_WithNoData_Option()
        {
            await using WithNoDataContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})", TestContext.Current.CancellationToken);

            List<WithNoDataAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Empty(aggregates);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_no_data', NULL, NULL);", [], TestContext.Current.CancellationToken);

            aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(aggregates);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_CustomChunkInterval

        private class CustomChunkIntervalTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class CustomChunkIntervalAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class CustomChunkIntervalContext(string connectionString) : DbContext
        {
            public DbSet<CustomChunkIntervalTrade> Trades => Set<CustomChunkIntervalTrade>();
            public DbSet<CustomChunkIntervalAggregate> TradeAggregates => Set<CustomChunkIntervalAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<CustomChunkIntervalTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<CustomChunkIntervalAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<CustomChunkIntervalAggregate, CustomChunkIntervalTrade>(
                            "trade_aggregate_custom_chunk",
                            "1 hour",
                            x => x.Timestamp,
                            chunkInterval: "1 day")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_CustomChunkInterval()
        {
            await using CustomChunkIntervalContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_custom_chunk', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<CustomChunkIntervalAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(aggregates);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_CreateGroupIndexes

        private class CreateGroupIndexesTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class CreateGroupIndexesAggregate
        {
            public DateTime TimeBucket { get; set; }
            public string Exchange { get; set; } = string.Empty;
            public decimal AvgPrice { get; set; }
        }

        private class CreateGroupIndexesContext(string connectionString) : DbContext
        {
            public DbSet<CreateGroupIndexesTrade> Trades => Set<CreateGroupIndexesTrade>();
            public DbSet<CreateGroupIndexesAggregate> TradeAggregates => Set<CreateGroupIndexesAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<CreateGroupIndexesTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<CreateGroupIndexesAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<CreateGroupIndexesAggregate, CreateGroupIndexesTrade>(
                            "trade_aggregate_with_indexes",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange)
                        .CreateGroupIndexes(true);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_CreateGroupIndexes()
        {
            await using CreateGroupIndexesContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_with_indexes', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<CreateGroupIndexesAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(aggregates);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_MaterializedOnly_False

        private class MaterializedOnlyFalseTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class MaterializedOnlyFalseAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class MaterializedOnlyFalseContext(string connectionString) : DbContext
        {
            public DbSet<MaterializedOnlyFalseTrade> Trades => Set<MaterializedOnlyFalseTrade>();
            public DbSet<MaterializedOnlyFalseAggregate> TradeAggregates => Set<MaterializedOnlyFalseAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<MaterializedOnlyFalseTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<MaterializedOnlyFalseAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<MaterializedOnlyFalseAggregate, MaterializedOnlyFalseTrade>(
                            "trade_aggregate_realtime",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .MaterializedOnly(false);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_MaterializedOnly_False()
        {
            await using MaterializedOnlyFalseContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})", TestContext.Current.CancellationToken);

            List<MaterializedOnlyFalseAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(100.00m, Assert.Single(aggregates).AvgPrice);
        }

        #endregion

        #region Should_Alter_ContinuousAggregate_ChunkInterval

        private class AlterChunkIntervalTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class AlterChunkIntervalAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class AlterChunkIntervalInitialContext(string connectionString) : DbContext
        {
            public DbSet<AlterChunkIntervalTrade> Trades => Set<AlterChunkIntervalTrade>();
            public DbSet<AlterChunkIntervalAggregate> TradeAggregates => Set<AlterChunkIntervalAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<AlterChunkIntervalTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<AlterChunkIntervalAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<AlterChunkIntervalAggregate, AlterChunkIntervalTrade>(
                            "trade_aggregate_alterable",
                            "1 hour",
                            x => x.Timestamp,
                            chunkInterval: "7 days")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class AlterChunkIntervalModifiedContext(string connectionString) : DbContext
        {
            public DbSet<AlterChunkIntervalTrade> Trades => Set<AlterChunkIntervalTrade>();
            public DbSet<AlterChunkIntervalAggregate> TradeAggregates => Set<AlterChunkIntervalAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<AlterChunkIntervalTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<AlterChunkIntervalAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<AlterChunkIntervalAggregate, AlterChunkIntervalTrade>(
                            "trade_aggregate_alterable",
                            "1 hour",
                            x => x.Timestamp,
                            chunkInterval: "14 days")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_ChunkInterval()
        {
            await using AlterChunkIntervalInitialContext context1 = new(_connectionString!);
            await context1.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            await context1.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context1.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_alterable', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<AlterChunkIntervalAggregate> aggregatesBefore = await context1.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(aggregatesBefore);

            await using AlterChunkIntervalModifiedContext context2 = new(_connectionString!);

            await context2.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_alterable
                SET (timescaledb.chunk_interval = '14 days');
            ", [], TestContext.Current.CancellationToken);

            List<AlterChunkIntervalAggregate> aggregatesAfter = await context2.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(aggregatesAfter);
            Assert.Equal(aggregatesBefore.Count, aggregatesAfter.Count);
        }

        #endregion

        #region Should_Alter_ContinuousAggregate_MaterializedOnly

        private class AlterMaterializedOnlyTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class AlterMaterializedOnlyAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class AlterMaterializedOnlyContext(string connectionString) : DbContext
        {
            public DbSet<AlterMaterializedOnlyTrade> Trades => Set<AlterMaterializedOnlyTrade>();
            public DbSet<AlterMaterializedOnlyAggregate> TradeAggregates => Set<AlterMaterializedOnlyAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<AlterMaterializedOnlyTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<AlterMaterializedOnlyAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<AlterMaterializedOnlyAggregate, AlterMaterializedOnlyTrade>(
                            "trade_aggregate_materialized_only",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .MaterializedOnly(false);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_MaterializedOnly()
        {
            await using AlterMaterializedOnlyContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_materialized_only', NULL, NULL);", [], TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_materialized_only
                SET (timescaledb.materialized_only = true);
            ", [], TestContext.Current.CancellationToken);

            List<AlterMaterializedOnlyAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(aggregates);
        }

        #endregion

        #region Should_Alter_ContinuousAggregate_CreateGroupIndexes

        private class AlterGroupIndexesTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class AlterGroupIndexesAggregate
        {
            public DateTime TimeBucket { get; set; }
            public string Exchange { get; set; } = string.Empty;
            public decimal AvgPrice { get; set; }
        }

        private class AlterGroupIndexesContext(string connectionString) : DbContext
        {
            public DbSet<AlterGroupIndexesTrade> Trades => Set<AlterGroupIndexesTrade>();
            public DbSet<AlterGroupIndexesAggregate> TradeAggregates => Set<AlterGroupIndexesAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<AlterGroupIndexesTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<AlterGroupIndexesAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<AlterGroupIndexesAggregate, AlterGroupIndexesTrade>(
                            "trade_aggregate_group_indexes",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange)
                        .CreateGroupIndexes(false);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_CreateGroupIndexes()
        {
            await using AlterGroupIndexesContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER MATERIALIZED VIEW trade_aggregate_group_indexes
                    SET (timescaledb.create_group_indexes = true);
                ", [], TestContext.Current.CancellationToken);
            });
        }

        #endregion

        #region Should_Drop_ContinuousAggregate_Successfully

        private class DropTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class DropAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class DropContext(string connectionString) : DbContext
        {
            public DbSet<DropTrade> Trades => Set<DropTrade>();
            public DbSet<DropAggregate> TradeAggregates => Set<DropAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<DropTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<DropAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<DropAggregate, DropTrade>(
                            "trade_aggregate_to_drop",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Drop_ContinuousAggregate_Successfully()
        {
            await using DropContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_to_drop', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<DropAggregate> aggregatesBefore = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(aggregatesBefore);

            await context.Database.ExecuteSqlRawAsync(
                "DROP MATERIALIZED VIEW IF EXISTS trade_aggregate_to_drop;", [], TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            {
                await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);
            });
        }

        #endregion

        #region Should_Generate_Correct_SQL_For_ContinuousAggregate

        private class SqlGenerationTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class SqlGenerationAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class SqlGenerationContext(string connectionString) : DbContext
        {
            public DbSet<SqlGenerationTrade> Trades => Set<SqlGenerationTrade>();
            public DbSet<SqlGenerationAggregate> TradeAggregates => Set<SqlGenerationAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<SqlGenerationTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<SqlGenerationAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<SqlGenerationAggregate, SqlGenerationTrade>(
                            "trade_aggregate_sql_gen",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        [Fact]
        public async Task Should_Generate_Correct_SQL_For_ContinuousAggregate()
        {
            await using SqlGenerationContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_sql_gen', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<SqlGenerationAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(aggregates);
            SqlGenerationAggregate firstAggregate = aggregates.First();
            Assert.True(firstAggregate.AvgPrice > 0);
        }

        #endregion

        #region Should_Handle_SnakeCase_Naming_Convention

        private class SnakeCaseTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class SnakeCaseAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class SnakeCaseContext(string connectionString) : DbContext
        {
            public DbSet<SnakeCaseTrade> Trades => Set<SnakeCaseTrade>();
            public DbSet<SnakeCaseAggregate> TradeAggregates => Set<SnakeCaseAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention()
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<SnakeCaseTrade>(entity =>
                {
                    entity.ToTable("trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<SnakeCaseAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<SnakeCaseAggregate, SnakeCaseTrade>(
                            "snake_case_test_aggregate",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);
                });
            }
        }

        [Fact]
        public async Task Should_Handle_SnakeCase_Naming_Convention()
        {
            await using SnakeCaseContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO trades (timestamp, ticker, price, size, exchange)
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.snake_case_test_aggregate', NULL, NULL);", [], TestContext.Current.CancellationToken);

            List<SnakeCaseAggregate> aggregates = await context.TradeAggregates.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(100.00m, Assert.Single(aggregates).AvgPrice);
        }

        #endregion

        #region Should_Create_ContinuousAggregate_With_CountStar_And_Verify_Counts

        private class CountStarEvent
        {
            public DateTime Timestamp { get; set; }
            public string? Category { get; set; }
            public decimal Value { get; set; }
        }

        [ContinuousAggregate(MaterializedViewName = "count_star_integration_aggregate", ParentName = nameof(CountStarEvent))]
        [TimeBucket("1 hour", nameof(CountStarEvent.Timestamp))]
        private class CountStarAggregate
        {
            public DateTime Bucket { get; set; }

            [Aggregate(EAggregateFunction.Count, "*")]
            public long TotalCount { get; set; }

            [Aggregate(EAggregateFunction.Count, nameof(CountStarEvent.Category))]
            public long CategoryCount { get; set; }
        }

        private class CountStarIntegrationContext(string connectionString) : DbContext
        {
            public DbSet<CountStarEvent> Events => Set<CountStarEvent>();
            public DbSet<CountStarAggregate> Aggregates => Set<CountStarAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<CountStarEvent>(entity =>
                {
                    entity.ToTable("count_star_events");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<CountStarAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                    entity.Property(x => x.TotalCount).HasColumnName("TotalCount");
                    entity.Property(x => x.CategoryCount).HasColumnName("CategoryCount");
                });
            }
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_CountStar_And_Verify_Counts()
        {
            await using CountStarIntegrationContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO count_star_events (""Timestamp"", ""Category"", ""Value"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"sports"}, {1.0m}),
                    ({new DateTime(2025, 1, 6, 10, 15, 0, DateTimeKind.Utc)}, {"news"}, {2.0m}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {(string?)null}, {3.0m})",
                TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.count_star_integration_aggregate', NULL, NULL);",
                [], TestContext.Current.CancellationToken);

            List<CountStarAggregate> aggregates = await context.Aggregates
                .ToListAsync(TestContext.Current.CancellationToken);

            CountStarAggregate aggregate = Assert.Single(aggregates);
            Assert.Equal(3L, aggregate.TotalCount);
            Assert.Equal(2L, aggregate.CategoryCount);
        }

        #endregion

        #region Should_Create_Hierarchical_ContinuousAggregate

        private class HierProbeRaw
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
        }

        private class HierProbeHourly
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class HierProbeDaily
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class HierarchicalContext(string connectionString) : DbContext
        {
            public DbSet<HierProbeRaw> ProbeRaw => Set<HierProbeRaw>();
            public DbSet<HierProbeHourly> ProbeHourly => Set<HierProbeHourly>();
            public DbSet<HierProbeDaily> ProbeDaily => Set<HierProbeDaily>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<HierProbeRaw>(entity =>
                {
                    entity.ToTable("hier_probe_raw");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<HierProbeHourly>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<HierProbeHourly, HierProbeRaw>(
                            "hier_probe_hourly",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<HierProbeDaily>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<HierProbeDaily, HierProbeHourly>(
                            "hier_probe_daily",
                            "1 day",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });
            }
        }

        [Fact]
        public async Task Should_Create_Hierarchical_ContinuousAggregate()
        {
            await using HierarchicalContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            List<string> viewNames = await context.Database
                .SqlQuery<string>($@"
                    SELECT view_name AS ""Value""
                    FROM timescaledb_information.continuous_aggregates
                    WHERE view_name IN ('hier_probe_hourly', 'hier_probe_daily')
                    ORDER BY view_name")
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains("hier_probe_hourly", viewNames);
            Assert.Contains("hier_probe_daily", viewNames);

            List<string> dailyParents = await context.Database
                .SqlQuery<string>($@"
                    SELECT parent.user_view_name AS ""Value""
                    FROM _timescaledb_catalog.continuous_agg child
                    JOIN _timescaledb_catalog.continuous_agg parent
                        ON child.parent_mat_hypertable_id = parent.mat_hypertable_id
                    WHERE child.user_view_name = 'hier_probe_daily'")
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("hier_probe_hourly", Assert.Single(dailyParents));
        }

        #endregion

        #region Should_Create_Five_Level_Hierarchical_ContinuousAggregate_Chain

        private class AChainDaily
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class BChainFourHourly
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class CChainHourly
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class DChainQuarterHourly
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class EChainFiveMinutely
        {
            public DateTime TimeBucket { get; set; }
            public double AvgValue { get; set; }
        }

        private class FChainRawMetric
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
        }

        private class FiveLevelChainContext(string connectionString) : DbContext
        {
            public DbSet<FChainRawMetric> RawMetrics => Set<FChainRawMetric>();
            public DbSet<AChainDaily> DailyAggregates => Set<AChainDaily>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<FChainRawMetric>(entity =>
                {
                    entity.ToTable("hier_chain_raw");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<EChainFiveMinutely>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<EChainFiveMinutely, FChainRawMetric>(
                            "hier_chain_5m",
                            "5 minutes",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<DChainQuarterHourly>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<DChainQuarterHourly, EChainFiveMinutely>(
                            "hier_chain_15m",
                            "15 minutes",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<CChainHourly>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<CChainHourly, DChainQuarterHourly>(
                            "hier_chain_1h",
                            "1 hour",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<BChainFourHourly>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<BChainFourHourly, CChainHourly>(
                            "hier_chain_4h",
                            "4 hours",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<AChainDaily>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<AChainDaily, BChainFourHourly>(
                            "hier_chain_1d",
                            "1 day",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });
            }
        }

        [Fact]
        public async Task Should_Create_Five_Level_Hierarchical_ContinuousAggregate_Chain()
        {
            // Arrange
            await using FiveLevelChainContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            // Act
            List<string> viewNames = await context.Database
                .SqlQuery<string>($@"
                    SELECT view_name AS ""Value""
                    FROM timescaledb_information.continuous_aggregates
                    WHERE view_name LIKE 'hier_chain_%'")
                .ToListAsync(TestContext.Current.CancellationToken);

            List<string> parentLinks = await context.Database
                .SqlQuery<string>($@"
                    SELECT child.user_view_name || '<-' || parent.user_view_name AS ""Value""
                    FROM _timescaledb_catalog.continuous_agg child
                    JOIN _timescaledb_catalog.continuous_agg parent
                        ON child.parent_mat_hypertable_id = parent.mat_hypertable_id
                    WHERE child.user_view_name LIKE 'hier_chain_%'")
                .ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, viewNames.Count);
            Assert.Contains("hier_chain_5m", viewNames);
            Assert.Contains("hier_chain_15m", viewNames);
            Assert.Contains("hier_chain_1h", viewNames);
            Assert.Contains("hier_chain_4h", viewNames);
            Assert.Contains("hier_chain_1d", viewNames);

            Assert.Equal(4, parentLinks.Count);
            Assert.Contains("hier_chain_15m<-hier_chain_5m", parentLinks);
            Assert.Contains("hier_chain_1h<-hier_chain_15m", parentLinks);
            Assert.Contains("hier_chain_4h<-hier_chain_1h", parentLinks);
            Assert.Contains("hier_chain_1d<-hier_chain_4h", parentLinks);

            // Act
            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO hier_chain_raw (""Timestamp"", ""Value"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {10.0}),
                    ({new DateTime(2025, 1, 6, 10, 7, 0, DateTimeKind.Utc)}, {20.0}),
                    ({new DateTime(2025, 1, 6, 10, 20, 0, DateTimeKind.Utc)}, {30.0})",
                TestContext.Current.CancellationToken);

            foreach (string refreshSql in (string[])
            [
                "CALL refresh_continuous_aggregate('public.hier_chain_5m', NULL, NULL);",
                "CALL refresh_continuous_aggregate('public.hier_chain_15m', NULL, NULL);",
                "CALL refresh_continuous_aggregate('public.hier_chain_1h', NULL, NULL);",
                "CALL refresh_continuous_aggregate('public.hier_chain_4h', NULL, NULL);",
                "CALL refresh_continuous_aggregate('public.hier_chain_1d', NULL, NULL);",
            ])
            {
                await context.Database.ExecuteSqlRawAsync(refreshSql, [], TestContext.Current.CancellationToken);
            }

            List<AChainDaily> dailyAggregates = await context.DailyAggregates
                .ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            AChainDaily dailyAggregate = Assert.Single(dailyAggregates);
            Assert.Equal(22.5, dailyAggregate.AvgValue);
        }

        #endregion

        #region Should_Create_Hierarchical_ContinuousAggregate_With_GroupBy_And_RefreshPolicies

        private class ComboMeterReading
        {
            public DateTime Timestamp { get; set; }
            public string MeterId { get; set; } = string.Empty;
            public double PowerKw { get; set; }
        }

        private class ComboHourlyUsage
        {
            public DateTime TimeBucket { get; set; }
            public string MeterId { get; set; } = string.Empty;
            public double MinPowerKw { get; set; }
            public double MaxPowerKw { get; set; }
            public double TotalPowerKw { get; set; }
            public long ReadingCount { get; set; }
        }

        private class ComboDailyUsage
        {
            public DateTime TimeBucket { get; set; }
            public string MeterId { get; set; } = string.Empty;
            public double MinPowerKw { get; set; }
            public double MaxPowerKw { get; set; }
            public double TotalPowerKw { get; set; }
            public long ReadingCount { get; set; }
        }

        private class ComboHierarchicalContext(string connectionString) : DbContext
        {
            public DbSet<ComboMeterReading> Readings => Set<ComboMeterReading>();
            public DbSet<ComboDailyUsage> DailyUsages => Set<ComboDailyUsage>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<ComboMeterReading>(entity =>
                {
                    entity.ToTable("hier_combo_readings");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<ComboHourlyUsage>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<ComboHourlyUsage, ComboMeterReading>(
                            "hier_combo_hourly",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.MinPowerKw, x => x.PowerKw, EAggregateFunction.Min)
                        .AddAggregateFunction(x => x.MaxPowerKw, x => x.PowerKw, EAggregateFunction.Max)
                        .AddAggregateFunction(x => x.TotalPowerKw, x => x.PowerKw, EAggregateFunction.Sum)
                        .AddAggregateFunction(x => x.ReadingCount, x => x.Timestamp, EAggregateFunction.Count)
                        .AddGroupByColumn(x => x.MeterId)
                        .WithRefreshPolicy(startOffset: "3 days", endOffset: "1 hour", scheduleInterval: "1 hour");
                });

                modelBuilder.Entity<ComboDailyUsage>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.IsContinuousAggregate<ComboDailyUsage, ComboHourlyUsage>(
                            "hier_combo_daily",
                            "1 day",
                            x => x.TimeBucket)
                        .AddAggregateFunction(x => x.MinPowerKw, x => x.MinPowerKw, EAggregateFunction.Min)
                        .AddAggregateFunction(x => x.MaxPowerKw, x => x.MaxPowerKw, EAggregateFunction.Max)
                        .AddAggregateFunction(x => x.TotalPowerKw, x => x.TotalPowerKw, EAggregateFunction.Sum)
                        .AddAggregateFunction(x => x.ReadingCount, x => x.ReadingCount, EAggregateFunction.Sum)
                        .AddGroupByColumn(x => x.MeterId)
                        .WithRefreshPolicy(startOffset: "30 days", endOffset: "1 day", scheduleInterval: "1 hour");
                });
            }
        }

        [Fact]
        public async Task Should_Create_Hierarchical_ContinuousAggregate_With_GroupBy_And_RefreshPolicies()
        {
            // Arrange
            await using ComboHierarchicalContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            // Act
            List<string> policyTargets = await context.Database
                .SqlQuery<string>($@"
                    SELECT j.hypertable_name AS ""Value""
                    FROM timescaledb_information.jobs j
                    WHERE j.proc_name = 'policy_refresh_continuous_aggregate'
                        AND j.hypertable_name LIKE 'hier_combo_%'")
                .ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, policyTargets.Count);
            Assert.Contains("hier_combo_hourly", policyTargets);
            Assert.Contains("hier_combo_daily", policyTargets);

            // Act
            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO hier_combo_readings (""Timestamp"", ""MeterId"", ""PowerKw"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"meter-a"}, {1.0}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"meter-a"}, {3.0}),
                    ({new DateTime(2025, 1, 6, 11, 15, 0, DateTimeKind.Utc)}, {"meter-a"}, {5.0}),
                    ({new DateTime(2025, 1, 6, 10, 15, 0, DateTimeKind.Utc)}, {"meter-b"}, {10.0})",
                TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.hier_combo_hourly', NULL, NULL);",
                [], TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.hier_combo_daily', NULL, NULL);",
                [], TestContext.Current.CancellationToken);

            List<ComboDailyUsage> dailyUsages = await context.DailyUsages
                .OrderBy(x => x.MeterId)
                .ToListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, dailyUsages.Count);

            Assert.Equal("meter-a", dailyUsages[0].MeterId);
            Assert.Equal(1.0, dailyUsages[0].MinPowerKw);
            Assert.Equal(5.0, dailyUsages[0].MaxPowerKw);
            Assert.Equal(9.0, dailyUsages[0].TotalPowerKw);
            Assert.Equal(3L, dailyUsages[0].ReadingCount);

            Assert.Equal("meter-b", dailyUsages[1].MeterId);
            Assert.Equal(10.0, dailyUsages[1].MinPowerKw);
            Assert.Equal(10.0, dailyUsages[1].MaxPowerKw);
            Assert.Equal(10.0, dailyUsages[1].TotalPowerKw);
            Assert.Equal(1L, dailyUsages[1].ReadingCount);
        }

        #endregion

        #region Should_Create_Hierarchical_ContinuousAggregate_With_Designated_BucketColumn

        private class DesignatedBucketRaw
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
        }

        private class DesignatedBucketHourly
        {
            public DateTime HourStart { get; set; }
            public double AvgValue { get; set; }
        }

        private class DesignatedBucketDaily
        {
            public DateTime DayStart { get; set; }
            public double AvgValue { get; set; }
        }

        private class DesignatedBucketContext(string connectionString) : DbContext
        {
            public DbSet<DesignatedBucketRaw> Raw => Set<DesignatedBucketRaw>();
            public DbSet<DesignatedBucketHourly> Hourly => Set<DesignatedBucketHourly>();
            public DbSet<DesignatedBucketDaily> Daily => Set<DesignatedBucketDaily>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<DesignatedBucketRaw>(entity =>
                {
                    entity.ToTable("designated_bucket_raw");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<DesignatedBucketHourly>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.HourStart).HasColumnName("hour_start");
                    entity.IsContinuousAggregate<DesignatedBucketHourly, DesignatedBucketRaw>(
                            "designated_bucket_hourly",
                            "1 hour",
                            x => x.Timestamp)
                        .WithTimeBucketProperty(x => x.HourStart)
                        .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                        .WithNoData(true);
                });

                modelBuilder.Entity<DesignatedBucketDaily>(entity =>
                {
                    entity.HasNoKey();
                    entity.Property(x => x.DayStart).HasColumnName("day_start");
                    entity.IsContinuousAggregate<DesignatedBucketDaily, DesignatedBucketHourly>(
                            "designated_bucket_daily",
                            "1 day",
                            x => x.HourStart)
                        .WithTimeBucketProperty(x => x.DayStart)
                        .AddAggregateFunction(x => x.AvgValue, x => x.AvgValue, EAggregateFunction.Avg)
                        .WithNoData(true);
                });
            }
        }

        [Fact]
        public async Task Should_Create_Hierarchical_ContinuousAggregate_With_Designated_BucketColumn()
        {
            await using DesignatedBucketContext context = new(_connectionString!);
            await CreateDatabaseViaMigrationAsync(context);

            List<string> hourlyColumns = await context.Database
                .SqlQuery<string>($@"
                    SELECT column_name AS ""Value""
                    FROM information_schema.columns
                    WHERE table_name = 'designated_bucket_hourly'
                    ORDER BY column_name")
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains("hour_start", hourlyColumns);
            Assert.DoesNotContain("time_bucket", hourlyColumns);

            List<string> dailyColumns = await context.Database
                .SqlQuery<string>($@"
                    SELECT column_name AS ""Value""
                    FROM information_schema.columns
                    WHERE table_name = 'designated_bucket_daily'
                    ORDER BY column_name")
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains("day_start", dailyColumns);

            List<string> dailyParents = await context.Database
                .SqlQuery<string>($@"
                    SELECT parent.user_view_name AS ""Value""
                    FROM _timescaledb_catalog.continuous_agg child
                    JOIN _timescaledb_catalog.continuous_agg parent
                        ON child.parent_mat_hypertable_id = parent.mat_hypertable_id
                    WHERE child.user_view_name = 'designated_bucket_daily'")
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("designated_bucket_hourly", Assert.Single(dailyParents));
        }

        #endregion
    }
}
