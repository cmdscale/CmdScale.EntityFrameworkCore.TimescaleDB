using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration
{
    public class ContinuousAggregateIntegrationTests : MigrationTestBase, IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private string? _connectionString;

        public async Task InitializeAsync()
        {
            _container = new PostgreSqlBuilder()
                .WithImage("timescale/timescaledb:latest-pg16")
                .WithDatabase("test_db")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_basic', NULL, NULL);");

            List<BasicAggregatesAggregate> aggregates = await context.TradeAggregates
                .OrderBy(a => a.TimeBucket)
                .ToListAsync();

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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {103.00m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_first_last', NULL, NULL);");

            List<FirstLastAggregate> aggregates = await context.TradeAggregates.ToListAsync();

            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].FirstPrice);
            Assert.Equal(103.00m, aggregates[0].LastPrice);
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
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {105.00m}, {150}, {"LSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_grouped', NULL, NULL);");

            List<GroupByAggregate> aggregates = await context.TradeAggregates
                .OrderBy(a => a.Exchange)
                .ToListAsync();

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
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"MSFT"}, {300.00m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_filtered', NULL, NULL);");

            List<WhereClauseAggregate> aggregates = await context.TradeAggregates.ToListAsync();

            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].AvgPrice);
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
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})");

            List<WithNoDataAggregate> aggregates = await context.TradeAggregates.ToListAsync();

            Assert.Empty(aggregates);

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_no_data', NULL, NULL);");

            aggregates = await context.TradeAggregates.ToListAsync();
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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_custom_chunk', NULL, NULL);");

            List<CustomChunkIntervalAggregate> aggregates = await context.TradeAggregates.ToListAsync();

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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_with_indexes', NULL, NULL);");

            List<CreateGroupIndexesAggregate> aggregates = await context.TradeAggregates.ToListAsync();

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
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})");

            List<MaterializedOnlyFalseAggregate> aggregates = await context.TradeAggregates.ToListAsync();

            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].AvgPrice);
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
                            chunkInterval: "14 days") // <-- Changed from "7 days"
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
            await context1.Database.EnsureCreatedAsync();

            await context1.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {150.50m}, {100}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 30, 0, DateTimeKind.Utc)}, {"AAPL"}, {151.00m}, {200}, {"NYSE"}),
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context1.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_alterable', NULL, NULL);");

            List<AlterChunkIntervalAggregate> aggregatesBefore = await context1.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregatesBefore);

            await using AlterChunkIntervalModifiedContext context2 = new(_connectionString!);

            await context2.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_alterable
                SET (timescaledb.chunk_interval = '14 days');
            ");

            List<AlterChunkIntervalAggregate> aggregatesAfter = await context2.TradeAggregates.ToListAsync();
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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_materialized_only', NULL, NULL);");

            await context.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_materialized_only
                SET (timescaledb.materialized_only = true);
            ");

            List<AlterMaterializedOnlyAggregate> aggregates = await context.TradeAggregates.ToListAsync();
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
                ");
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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_to_drop', NULL, NULL);");

            List<DropAggregate> aggregatesBefore = await context.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregatesBefore);

            await context.Database.ExecuteSqlRawAsync(
                "DROP MATERIALIZED VIEW IF EXISTS trade_aggregate_to_drop;");

            await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            {
                await context.TradeAggregates.ToListAsync();
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
                    ({new DateTime(2025, 1, 6, 10, 45, 0, DateTimeKind.Utc)}, {"AAPL"}, {149.75m}, {150}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_sql_gen', NULL, NULL);");

            List<SqlGenerationAggregate> aggregates = await context.TradeAggregates.ToListAsync();

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
                VALUES ({new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc)}, {"AAPL"}, {100.00m}, {100}, {"NYSE"})");

            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.snake_case_test_aggregate', NULL, NULL);");

            List<SnakeCaseAggregate> aggregates = await context.TradeAggregates.ToListAsync();

            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].AvgPrice);
        }

        #endregion
    }
}
