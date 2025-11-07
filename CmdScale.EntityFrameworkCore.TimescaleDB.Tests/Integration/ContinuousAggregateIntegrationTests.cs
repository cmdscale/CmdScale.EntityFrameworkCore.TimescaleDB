using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration
{
    public class ContinuousAggregateIntegrationTests : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private string? _connectionString;

        public async Task InitializeAsync()
        {
            // Arrange: Start TimescaleDB container
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

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_BasicAggregates()
        {
            // Arrange: Create context with hypertable and continuous aggregate using basic aggregates
            await using var context = new BasicAggregatesTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await InsertTradeDataAsync(context);

            // Act: Refresh the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_basic', NULL, NULL);");

            var aggregates = await context.TradeAggregates
                .OrderBy(a => a.TimeBucket)
                .ToListAsync();

            // Assert: Verify aggregates were calculated correctly
            Assert.NotEmpty(aggregates);
            var firstAggregate = aggregates.First();
            Assert.True(firstAggregate.AvgPrice > 0);
            Assert.True(firstAggregate.MaxPrice >= firstAggregate.MinPrice);
            Assert.True(firstAggregate.SumPrice > 0);
            Assert.True(firstAggregate.CountPrice > 0);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_FirstAndLast_Functions()
        {
            // Arrange: Create context with First and Last aggregate functions
            await using var context = new FirstLastTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data with specific timestamps
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE'),
                    ('2025-01-06 10:30:00+00', 'AAPL', 105.00, 200, 'NYSE'),
                    ('2025-01-06 10:45:00+00', 'AAPL', 103.00, 150, 'NYSE');
            ");

            // Act: Refresh the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_first_last', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify first() returns earliest value and last() returns latest value
            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].FirstPrice); // First price at 10:00
            Assert.Equal(103.00m, aggregates[0].LastPrice);  // Last price at 10:45
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_GroupByColumns()
        {
            // Arrange: Create context with GROUP BY columns
            await using var context = new GroupByTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data for different exchanges
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE'),
                    ('2025-01-06 10:00:00+00', 'AAPL', 110.00, 200, 'NASDAQ'),
                    ('2025-01-06 10:00:00+00', 'AAPL', 105.00, 150, 'LSE');
            ");

            // Act: Refresh the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_grouped', NULL, NULL);");

            var aggregates = await context.TradeAggregates
                .OrderBy(a => a.Exchange)
                .ToListAsync();

            // Assert: Verify we have one aggregate per exchange
            Assert.Equal(3, aggregates.Count);
            Assert.Equal("LSE", aggregates[0].Exchange);
            Assert.Equal(105.00m, aggregates[0].AvgPrice);
            Assert.Equal("NASDAQ", aggregates[1].Exchange);
            Assert.Equal(110.00m, aggregates[1].AvgPrice);
            Assert.Equal("NYSE", aggregates[2].Exchange);
            Assert.Equal(100.00m, aggregates[2].AvgPrice);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_WhereClause()
        {
            // Arrange: Create context with WHERE clause to filter data
            await using var context = new WhereClauseTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data with different tickers
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE'),
                    ('2025-01-06 10:00:00+00', 'TSLA', 200.00, 200, 'NYSE'),
                    ('2025-01-06 10:00:00+00', 'MSFT', 300.00, 150, 'NYSE');
            ");

            // Act: Refresh the continuous aggregate (should only include AAPL)
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_filtered', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify only AAPL data is included
            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].AvgPrice);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_WithNoData_Option()
        {
            // Arrange: Create context with WITH NO DATA option
            await using var context = new WithNoDataTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE');
            ");

            // Act: Query the continuous aggregate (should be empty because of WITH NO DATA)
            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify no data is materialized initially
            Assert.Empty(aggregates);

            // Now refresh and verify data appears
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_no_data', NULL, NULL);");

            aggregates = await context.TradeAggregates.ToListAsync();
            Assert.Single(aggregates);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_CustomChunkInterval()
        {
            // Arrange: Create context with custom chunk_interval = "1 day"
            await using var context = new CustomChunkIntervalTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await InsertTradeDataAsync(context);

            // Act: Refresh and query the aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_custom_chunk', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify the continuous aggregate works correctly
            Assert.NotEmpty(aggregates);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_CreateGroupIndexes()
        {
            // Arrange: Create context with create_group_indexes = true
            await using var context = new CreateGroupIndexesTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await InsertTradeDataAsync(context);

            // Act: Refresh and query the aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_with_indexes', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify aggregates were created (indexes are internal, hard to verify directly)
            Assert.NotEmpty(aggregates);
        }

        [Fact]
        public async Task Should_Create_ContinuousAggregate_With_MaterializedOnly_False()
        {
            // Arrange: Create context with materialized_only = false (allows real-time aggregation)
            await using var context = new MaterializedOnlyFalseTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE');
            ");

            // Act: Query without explicit refresh (should include real-time data)
            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify we can see data even without manual refresh
            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].AvgPrice);
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_ChunkInterval()
        {
            // Arrange: Create context with initial chunk_interval = "7 days"
            await using var context1 = new AlterChunkIntervalContext_Before(_connectionString!);
            await context1.Database.EnsureCreatedAsync();

            // Insert test data and refresh
            await InsertTradeDataAsync(context1);
            await context1.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_alterable', NULL, NULL);");

            var aggregatesBefore = await context1.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregatesBefore);

            // Act: Alter the chunk_interval to "14 days"
            await using var context2 = new AlterChunkIntervalContext_After(_connectionString!);

            await context2.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_alterable
                SET (timescaledb.chunk_interval = '14 days');
            ");

            // Assert: Verify we can still query the aggregate after altering chunk_interval
            var aggregatesAfter = await context2.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregatesAfter);
            Assert.Equal(aggregatesBefore.Count, aggregatesAfter.Count);
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_MaterializedOnly()
        {
            // Arrange: Create context with materialized_only = false
            await using var context = new AlterMaterializedOnlyTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data and refresh
            await InsertTradeDataAsync(context);
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_materialized_only', NULL, NULL);");

            // Act: Alter materialized_only to true
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER MATERIALIZED VIEW trade_aggregate_materialized_only
                SET (timescaledb.materialized_only = true);
            ");

            // Assert: Verify we can still query the aggregate after alteration
            var aggregates = await context.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregates);
        }

        [Fact]
        public async Task Should_Alter_ContinuousAggregate_CreateGroupIndexes()
        {
            // NOTE: TimescaleDB does not support altering create_group_indexes after creation
            // This test verifies that the option is set during creation but cannot be altered

            // Arrange: Create context with create_group_indexes = false
            await using var context = new AlterCreateGroupIndexesTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Act & Assert: Attempting to alter create_group_indexes should fail
            await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER MATERIALIZED VIEW trade_aggregate_group_indexes
                    SET (timescaledb.create_group_indexes = true);
                ");
            });
        }

        [Fact]
        public async Task Should_Drop_ContinuousAggregate_Successfully()
        {
            // Arrange: Create context with continuous aggregate
            await using var context = new DropTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert data and refresh to ensure aggregate has data
            await InsertTradeDataAsync(context);
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_to_drop', NULL, NULL);");

            // Verify we can query the aggregate before dropping
            var aggregatesBefore = await context.TradeAggregates.ToListAsync();
            Assert.NotEmpty(aggregatesBefore);

            // Act: Drop the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "DROP MATERIALIZED VIEW IF EXISTS trade_aggregate_to_drop;");

            // Assert: Verify we cannot query the aggregate after dropping (should throw)
            await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            {
                await context.TradeAggregates.ToListAsync();
            });
        }

        [Fact]
        public async Task Should_Generate_Correct_SQL_For_ContinuousAggregate()
        {
            // Arrange: Create context and ensure database is created
            await using var context = new SqlGenerationTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await InsertTradeDataAsync(context);

            // Act: Refresh and query the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_sql_gen', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify the continuous aggregate works correctly
            Assert.NotEmpty(aggregates);
            var firstAggregate = aggregates.First();
            Assert.True(firstAggregate.AvgPrice > 0);
        }

        [Fact]
        public async Task Should_Handle_SnakeCase_Naming_Convention()
        {
            // Arrange: Create context with snake_case naming convention
            await using var context = new SnakeCaseTestContext(_connectionString!);
            await context.Database.EnsureCreatedAsync();

            // Insert test data
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO trades (timestamp, ticker, price, size, exchange)
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 100.00, 100, 'NYSE');
            ");

            // Act: Refresh and query the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_snake_case', NULL, NULL);");

            var aggregates = await context.TradeAggregates.ToListAsync();

            // Assert: Verify snake_case columns work correctly
            Assert.Single(aggregates);
            Assert.Equal(100.00m, aggregates[0].avg_price);
        }

        #region Helper Methods

        private async Task InsertTradeDataAsync(DbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:00:00+00', 'AAPL', 150.50, 100, 'NYSE'),
                    ('2025-01-06 10:30:00+00', 'AAPL', 151.00, 200, 'NYSE'),
                    ('2025-01-06 10:45:00+00', 'AAPL', 149.75, 150, 'NYSE');
            ");
        }

        #endregion

        #region Test Models

        private class TestTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class BasicAggregatesTestAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
            public decimal MaxPrice { get; set; }
            public decimal MinPrice { get; set; }
            public decimal SumPrice { get; set; }
            public long CountPrice { get; set; }
        }

        private class FirstLastTestAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal FirstPrice { get; set; }
            public decimal LastPrice { get; set; }
        }

        private class GroupByTestAggregate
        {
            public DateTime TimeBucket { get; set; }
            public string Exchange { get; set; } = string.Empty;
            public decimal AvgPrice { get; set; }
        }

        private class WhereClauseTestAggregate
        {
            public DateTime TimeBucket { get; set; }
            public decimal AvgPrice { get; set; }
        }

        private class SnakeCaseTestAggregate
        {
            public DateTime time_bucket { get; set; }
            public decimal avg_price { get; set; }
        }

        #endregion

        #region Test Contexts

        private class BasicAggregatesTestContext : DbContext
        {
            private readonly string _connectionString;

            public BasicAggregatesTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<BasicAggregatesTestAggregate> TradeAggregates => Set<BasicAggregatesTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                // Configure Trade as a hypertable
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                // Configure continuous aggregate with all basic aggregate functions
                modelBuilder.Entity<BasicAggregatesTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<BasicAggregatesTestAggregate, TestTrade>(
                            "trade_aggregate_basic",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
                        .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
                        .AddAggregateFunction(x => x.SumPrice, x => x.Price, EAggregateFunction.Sum)
                        .AddAggregateFunction(x => x.CountPrice, x => x.Price, EAggregateFunction.Count);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                    entity.Property(x => x.MaxPrice).HasColumnName("MaxPrice");
                    entity.Property(x => x.MinPrice).HasColumnName("MinPrice");
                    entity.Property(x => x.SumPrice).HasColumnName("SumPrice");
                    entity.Property(x => x.CountPrice).HasColumnName("CountPrice");
                });
            }
        }

        private class FirstLastTestContext : DbContext
        {
            private readonly string _connectionString;

            public FirstLastTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<FirstLastTestAggregate> TradeAggregates => Set<FirstLastTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<FirstLastTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<FirstLastTestAggregate, TestTrade>(
                            "trade_aggregate_first_last",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.FirstPrice, x => x.Price, EAggregateFunction.First)
                        .AddAggregateFunction(x => x.LastPrice, x => x.Price, EAggregateFunction.Last);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.FirstPrice).HasColumnName("FirstPrice");
                    entity.Property(x => x.LastPrice).HasColumnName("LastPrice");
                });
            }
        }

        private class GroupByTestContext : DbContext
        {
            private readonly string _connectionString;

            public GroupByTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<GroupByTestAggregate> TradeAggregates => Set<GroupByTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<GroupByTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<GroupByTestAggregate, TestTrade>(
                            "trade_aggregate_grouped",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class WhereClauseTestContext : DbContext
        {
            private readonly string _connectionString;

            public WhereClauseTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_filtered",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .Where("\"Ticker\" = 'AAPL'");

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class WithNoDataTestContext : DbContext
        {
            private readonly string _connectionString;

            public WithNoDataTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_no_data",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .WithNoData(true)
                        .MaterializedOnly(true); // Disable real-time aggregation so WITH NO DATA takes effect

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class CustomChunkIntervalTestContext : DbContext
        {
            private readonly string _connectionString;

            public CustomChunkIntervalTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_custom_chunk",
                            "1 hour",
                            x => x.Timestamp,
                            chukInterval: "1 day")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class CreateGroupIndexesTestContext : DbContext
        {
            private readonly string _connectionString;

            public CreateGroupIndexesTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<GroupByTestAggregate> TradeAggregates => Set<GroupByTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<GroupByTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<GroupByTestAggregate, TestTrade>(
                            "trade_aggregate_with_indexes",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange)
                        .CreateGroupIndexes(true);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class MaterializedOnlyFalseTestContext : DbContext
        {
            private readonly string _connectionString;

            public MaterializedOnlyFalseTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_realtime",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .MaterializedOnly(false);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class AlterChunkIntervalContext_Before : DbContext
        {
            private readonly string _connectionString;

            public AlterChunkIntervalContext_Before(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_alterable",
                            "1 hour",
                            x => x.Timestamp,
                            chukInterval: "7 days")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class AlterChunkIntervalContext_After : DbContext
        {
            private readonly string _connectionString;

            public AlterChunkIntervalContext_After(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_alterable",
                            "1 hour",
                            x => x.Timestamp,
                            chukInterval: "14 days")
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class AlterMaterializedOnlyTestContext : DbContext
        {
            private readonly string _connectionString;

            public AlterMaterializedOnlyTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_materialized_only",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .MaterializedOnly(false);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class AlterCreateGroupIndexesTestContext : DbContext
        {
            private readonly string _connectionString;

            public AlterCreateGroupIndexesTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<GroupByTestAggregate> TradeAggregates => Set<GroupByTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<GroupByTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<GroupByTestAggregate, TestTrade>(
                            "trade_aggregate_group_indexes",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                        .AddGroupByColumn(x => x.Exchange)
                        .CreateGroupIndexes(false);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class DropTestContext : DbContext
        {
            private readonly string _connectionString;

            public DropTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_to_drop",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class SqlGenerationTestContext : DbContext
        {
            private readonly string _connectionString;

            public SqlGenerationTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<WhereClauseTestAggregate> TradeAggregates => Set<WhereClauseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("Trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<WhereClauseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<WhereClauseTestAggregate, TestTrade>(
                            "trade_aggregate_sql_gen",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.AvgPrice).HasColumnName("AvgPrice");
                });
            }
        }

        private class SnakeCaseTestContext : DbContext
        {
            private readonly string _connectionString;

            public SnakeCaseTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<SnakeCaseTestAggregate> TradeAggregates => Set<SnakeCaseTestAggregate>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(_connectionString)
                    .UseSnakeCaseNamingConvention()
                    .UseTimescaleDb();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestTrade>(entity =>
                {
                    entity.ToTable("trades");
                    entity.HasNoKey();
                    entity.IsHypertable(x => x.Timestamp);
                });

                modelBuilder.Entity<SnakeCaseTestAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<SnakeCaseTestAggregate, TestTrade>(
                            "trade_aggregate_snake_case",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.avg_price, x => x.Price, EAggregateFunction.Avg);

                    // Note: snake_case convention is applied automatically, so time_bucket and avg_price are already correct
                });
            }
        }

        #endregion
    }
}
