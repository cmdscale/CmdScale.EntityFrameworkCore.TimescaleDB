using BenchmarkDotNet.Attributes;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks
{
    public abstract class WriteRecordsBenchmarkBase<T> where T : class
    {
        public int NumberOfRecords;
        public int MaxBatchSize;
        public int NumberOfWorkers;

        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg17")
            .WithDatabase("benchmark_tests_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        protected string ConnectionString = "";
        protected readonly List<T> Trades = [];
        protected TimescaleContext? Context;

        [GlobalSetup]
        public async Task Setup()
        {
            await _dbContainer.StartAsync();
            ConnectionString = _dbContainer.GetConnectionString();

            DbContextOptionsBuilder<TimescaleContext> optionsBuilder = new();
            optionsBuilder.UseNpgsql(ConnectionString).UseTimescaleDb();
            Context = new TimescaleContext(optionsBuilder.Options);

            await Context.Database.MigrateAsync();
            Console.WriteLine("Migration applied successfully.");
        }

        [GlobalCleanup]
        public async Task GlobalCleanup()
        {
            await _dbContainer.DisposeAsync();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            Trades.Clear();
            var random = new Random();
            string[] tickers = ["AAPL", "GOOGL", "MSFT", "TSLA", "AMZN"];
            var baseTimestamp = DateTime.UtcNow.AddMinutes(-30);

            for (int i = 0; i < NumberOfRecords; i++)
            {
                var trade = CreateTradeInstance(i, baseTimestamp, tickers[random.Next(tickers.Length)], random);
                Trades.Add(trade);
            }

            // Truncate the table before each iteration for a clean slate
            string tableName = GetTableName();
            string sql = $"TRUNCATE TABLE \"{tableName}\"";
            Context!.Database.ExecuteSqlRaw(sql);
        }

        protected abstract T CreateTradeInstance(int index, DateTime baseTimestamp, string ticker, Random random);
        protected abstract string GetTableName();
    }
}
