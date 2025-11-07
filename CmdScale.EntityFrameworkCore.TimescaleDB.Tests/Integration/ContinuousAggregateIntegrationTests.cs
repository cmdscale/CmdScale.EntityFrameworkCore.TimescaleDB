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
            // Start TimescaleDB container
            _container = new PostgreSqlBuilder()
                .WithImage("timescale/timescaledb:latest-pg16")
                .WithDatabase("test_db")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();

            await using var context = new TestDbContext(_connectionString);
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
        }

        [Fact]
        public async Task ContinuousAggregate_ShouldAggregateTradeData_Successfully()
        {
            // Arrange
            await using var context = new TestDbContext(_connectionString!);

            // Create the database schema (tables, hypertables, and continuous aggregates)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Trades"" (
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""Ticker"" text NOT NULL,
                    ""Price"" numeric NOT NULL,
                    ""Size"" integer NOT NULL,
                    ""Exchange"" text NOT NULL
                );
            ");

            // Create hypertable
            await context.Database.ExecuteSqlRawAsync(@"
                SELECT create_hypertable('""Trades""', 'Timestamp', if_not_exists => TRUE);
                SELECT set_chunk_time_interval('public.""Trades""', INTERVAL '1 day');
            ");

            // Create continuous aggregate (must be separate due to transaction requirements)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE MATERIALIZED VIEW IF NOT EXISTS trade_aggregate_view
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false) AS
                SELECT time_bucket('1 hour', ""Timestamp"") AS time_bucket, ""Exchange"", AVG(""Price"") AS ""AveragePrice"", MAX(""Price"") AS ""MaxPrice"", MIN(""Price"") AS ""MinPrice""
                FROM ""Trades""
                WHERE ""Ticker"" = 'MCRS'
                GROUP BY time_bucket, ""Exchange"";
            ");

            // Insert test data using raw SQL (since TestTrade is keyless and can't be tracked)
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Trades"" (""Timestamp"", ""Ticker"", ""Price"", ""Size"", ""Exchange"")
                VALUES
                    ('2025-01-06 10:15:00+00', 'MCRS', 150.50, 100, 'NYSE'),
                    ('2025-01-06 10:30:00+00', 'MCRS', 151.00, 200, 'NYSE'),
                    ('2025-01-06 10:45:00+00', 'MCRS', 149.75, 150, 'NYSE'),
                    ('2025-01-06 10:20:00+00', 'MCRS', 150.00, 180, 'NASDAQ'),
                    ('2025-01-06 10:50:00+00', 'MCRS', 151.50, 220, 'NASDAQ'),
                    ('2025-01-06 11:10:00+00', 'MCRS', 152.00, 300, 'NYSE'),
                    ('2025-01-06 11:25:00+00', 'MCRS', 153.25, 250, 'NYSE'),
                    ('2025-01-06 11:40:00+00', 'MCRS', 151.75, 180, 'NYSE'),
                    ('2025-01-06 10:15:00+00', 'AAPL', 180.00, 100, 'NYSE'),
                    ('2025-01-06 10:30:00+00', 'TSLA', 250.00, 50, 'NASDAQ');
            ");

            // Act - Manually refresh the continuous aggregate
            await context.Database.ExecuteSqlRawAsync(
                "CALL refresh_continuous_aggregate('public.trade_aggregate_view', NULL, NULL);");

            // Query the continuous aggregate
            var aggregates = await context.TradeAggregates
                .OrderBy(a => a.TimeBucket)
                .ThenBy(a => a.Exchange)
                .ToListAsync();

            // Assert
            // We expect 3 aggregates: 2 for hour 10:00 (NYSE + NASDAQ) and 1 for hour 11:00 (NYSE only)
            Assert.Equal(3, aggregates.Count);

            // Verify Hour 1 - NYSE aggregate
            var hour1Nyse = aggregates.First(a =>
                a.TimeBucket == new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc) &&
                a.Exchange == "NYSE");
            Assert.Equal(150.4166666666666667m, hour1Nyse.AveragePrice); // (150.50 + 151.00 + 149.75) / 3
            Assert.Equal(151.00m, hour1Nyse.MaxPrice);
            Assert.Equal(149.75m, hour1Nyse.MinPrice);

            // Verify Hour 1 - NASDAQ aggregate
            var hour1Nasdaq = aggregates.First(a =>
                a.TimeBucket == new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc) &&
                a.Exchange == "NASDAQ");
            Assert.Equal(150.75m, hour1Nasdaq.AveragePrice); // (150.00 + 151.50) / 2
            Assert.Equal(151.50m, hour1Nasdaq.MaxPrice);
            Assert.Equal(150.00m, hour1Nasdaq.MinPrice);

            // Verify Hour 2 - NYSE aggregate
            var hour2Nyse = aggregates.First(a =>
                a.TimeBucket == new DateTime(2025, 1, 6, 11, 0, 0, DateTimeKind.Utc) &&
                a.Exchange == "NYSE");
            Assert.Equal(152.3333333333333333m, hour2Nyse.AveragePrice); // (152.00 + 153.25 + 151.75) / 3
            Assert.Equal(153.25m, hour2Nyse.MaxPrice);
            Assert.Equal(151.75m, hour2Nyse.MinPrice);

            // Verify that other tickers (AAPL, TSLA) are NOT in the aggregates
            Assert.DoesNotContain(aggregates, a => a.Exchange != "NYSE" && a.Exchange != "NASDAQ");
        }

        #region Test Models and DbContext

        private class TestTrade
        {
            public DateTime Timestamp { get; set; }
            public string Ticker { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Size { get; set; }
            public string Exchange { get; set; } = string.Empty;
        }

        private class TestTradeAggregate
        {
            public DateTime TimeBucket { get; set; }
            public string Exchange { get; set; } = string.Empty;
            public decimal AveragePrice { get; set; }
            public decimal MaxPrice { get; set; }
            public decimal MinPrice { get; set; }
        }

        private class TestDbContext : DbContext
        {
            private readonly string _connectionString;

            public TestDbContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<TestTrade> Trades => Set<TestTrade>();
            public DbSet<TestTradeAggregate> TradeAggregates => Set<TestTradeAggregate>();

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
                    entity.IsHypertable(x => x.Timestamp)
                        .WithChunkTimeInterval("1 day");
                });

                // Configure TradeAggregate as a continuous aggregate
                modelBuilder.Entity<TestTradeAggregate>(entity =>
                {
                    entity.HasNoKey();
                    entity.IsContinuousAggregate<TestTradeAggregate, TestTrade>(
                            "trade_aggregate_view",
                            "1 hour",
                            x => x.Timestamp)
                        .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
                        .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
                        .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
                        .AddGroupByColumn(x => x.Exchange)
                        .Where("\"Ticker\" = 'MCRS'");

                    // Map properties to view columns
                    entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                    entity.Property(x => x.Exchange).HasColumnName("Exchange");
                    entity.Property(x => x.AveragePrice).HasColumnName("AveragePrice");
                    entity.Property(x => x.MaxPrice).HasColumnName("MaxPrice");
                    entity.Property(x => x.MinPrice).HasColumnName("MinPrice");
                });
            }
        }

        #endregion
    }
}
