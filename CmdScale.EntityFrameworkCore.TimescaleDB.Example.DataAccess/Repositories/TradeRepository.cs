using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using NpgsqlTypes;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Repositories
{
    public class TradeRepository(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        /// <summary>
        /// Ingest data defining table, workers and batch size
        /// </summary>
        /// <param name="trades"></param>
        /// <returns></returns>
        public async Task IngestTradesAsync(List<Trade> trades)
        {
            TimescaleCopyConfig<Trade> config = new TimescaleCopyConfig<Trade>()
                .ToTable("Trades")
                .WithWorkers(8)
                .WithBatchSize(20_000);

            await trades.BulkCopyToAsync(_connectionString, config);
        }

        /// <summary>
        /// Ingest data using complex column mapping
        /// </summary>
        /// <param name="trades"></param>
        /// <returns></returns>
        public async Task IngestTradesAsyncWithColumnMapping(List<Trade> trades)
        {
            TimescaleCopyConfig<Trade> config = new TimescaleCopyConfig<Trade>()
                .ToTable("Trades")
                .WithWorkers(8)
                .WithBatchSize(20_000)

                // Use this method to override the default mappings discovered by
                // the constructor or to define a specific column order for the bulk copy.
                .MapColumn("Timestamp", t => t.Timestamp, NpgsqlDbType.TimestampTz)
                .MapColumn("Ticker", t => t.Ticker, NpgsqlDbType.Text)
                .MapColumn("Price", t => t.Price, NpgsqlDbType.Numeric)
                .MapColumn("Size", t => t.Size, NpgsqlDbType.Integer)
                .MapColumn("Exchange", t => t.Exchange, NpgsqlDbType.Text);

            await trades.BulkCopyToAsync(_connectionString, config);
        }

        /// <summary>
        /// Insert data with the default config for parallelism, batch size and column mapping.
        /// </summary>
        /// <param name="trades"></param>
        /// <returns></returns>
        public async Task IngestTradesAsyncWithDefaultConfig(List<Trade> trades)
        {
            await trades.BulkCopyToAsync(_connectionString);
        }
    }
}
