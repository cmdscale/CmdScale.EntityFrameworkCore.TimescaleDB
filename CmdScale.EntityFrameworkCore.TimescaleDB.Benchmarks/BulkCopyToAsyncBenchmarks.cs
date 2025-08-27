using BenchmarkDotNet.Attributes;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks
{
    [Config(typeof(InProcessConfig))]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class BulkCopyToAsyncBenchmarks
    {
        [Params(50_000)]
        public int NumberOfRecords;

        [Params(5000, 10_000, 20_000, 30_000, 50_000)]
        public int MaxBatchSize;

        [Params(1, 2, 4, 8)]
        public int NumberOfWorkers;

        private readonly string _connectionString = "Host=localhost;Database=cmdscale-ef-timescaledb;Username=timescale_admin;Password=R#!kro#GP43ra8Ae;Include Error Detail=True";
        private readonly List<Trade> trades = [];

        [IterationSetup]
        public void IterationSetup()
        {
            trades.Clear();

            // --- Data Variety Setup ---
            Random random = new();
            string[] tickers = ["AAPL", "GOOGL", "MSFT", "TSLA", "AMZN", "NVDA", "JPM", "V"];
            string[] exchanges = ["NASDAQ", "NYSE", "ARCA"];
            DateTime baseTimestamp = DateTime.UtcNow.AddMinutes(-30);
            Dictionary<string, decimal> basePrices = tickers.ToDictionary(t => t, t => (decimal)(100 + random.NextDouble() * 400));

            // --- Data Generation Loop ---
            for (int i = 0; i < NumberOfRecords; i++)
            {
                string currentTicker = tickers[random.Next(tickers.Length)];
                decimal priceJitter = (decimal)(random.NextDouble() * 2 - 1);
                decimal currentPrice = basePrices[currentTicker] + priceJitter;

                trades.Add(new Trade
                {
                    Timestamp = baseTimestamp.AddMicroseconds(i),
                    Ticker = currentTicker,
                    Price = Math.Round(currentPrice, 2),
                    Size = random.Next(1, 2500),
                    Exchange = exchanges[random.Next(exchanges.Length)],
                });
            }

            Console.WriteLine($"Generated {trades.Count} records.");
        }

        [Benchmark]
        public async Task BulkCopyAsyncPerformance()
        {
            TimescaleCopyConfig<Trade> config = new TimescaleCopyConfig<Trade>()
                .ToTable("Trades")
                .WithWorkers(NumberOfWorkers)
                .WithBatchSize(MaxBatchSize);

            await trades.BulkCopyToAsync(_connectionString, config);
        }
    }
}
