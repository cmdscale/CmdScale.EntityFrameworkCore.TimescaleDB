using BenchmarkDotNet.Attributes;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Npgsql;
using NpgsqlTypes;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks
{
    [Config(typeof(InProcessConfig))]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class WriteRecordsWithKeyBenchmarks : WriteRecordsBenchmarkBase<TradeWithId>
    {
        [Params(1_000, 5_000, 10_000)]
        public new int NumberOfRecords;

        [Params(500, 1_000, 5_000)]
        public new int MaxBatchSize;

        [Params(1, 4, 8)]
        public new int NumberOfWorkers;

        private readonly List<Task> tasks = [];
        private int totalRecords;
        private int workerChunkSize;

        protected override TradeWithId CreateTradeInstance(int index, DateTime baseTimestamp, string ticker, Random random)
        {
            return new TradeWithId
            {
                Timestamp = baseTimestamp.AddMicroseconds(index),
                Ticker = ticker,
                Price = (decimal)(100 + random.NextDouble() * 400),
                Size = random.Next(1, 100)
            };
        }

        protected override string GetTableName() => "TradesWithId";

        [Benchmark]
        public Task BulkCopyAsync()
        {
            TimescaleCopyConfig<TradeWithId> config = new TimescaleCopyConfig<TradeWithId>()
                .ToTable(GetTableName())
                .WithWorkers(NumberOfWorkers)
                .WithBatchSize(MaxBatchSize);
            return Trades.BulkCopyAsync(ConnectionString, config);
        }

        [Benchmark]
        public async Task HardcodedBulkCopyAsync()
        {
            totalRecords = Trades.Count;
            workerChunkSize = (int)Math.Ceiling((double)totalRecords / NumberOfWorkers);

            for (int i = 0; i < NumberOfWorkers; i++)
            {
                int startIndex = i * workerChunkSize;
                int currentWorkerDataSize = Math.Min(workerChunkSize, totalRecords - startIndex);

                if (currentWorkerDataSize <= 0)
                {
                    break;
                }

                List<TradeWithId> workerData = [.. Trades.Skip(startIndex).Take(currentWorkerDataSize)];
                tasks.Add(Task.Run(async () =>
                {
                    // Open new connection to DB
                    using NpgsqlConnection connection = new(ConnectionString);
                    await connection.OpenAsync();

                    // Command to copy data in a binary format from a client-application 
                    string copyCommand = "COPY \"TradesWithId\" (\"Time\", \"Value\", \"SegmentId\", \"SignalId\") FROM STDIN (FORMAT BINARY)";

                    for (int j = 0; j < workerData.Count; j += MaxBatchSize)
                    {
                        List<TradeWithId> currentBatch = [.. workerData.Skip(j).Take(MaxBatchSize)];

                        // Start a binary import stream
                        await using NpgsqlBinaryImporter writer = connection.BeginBinaryImport(copyCommand);
                        foreach (TradeWithId item in currentBatch)
                        {
                            // IMPORTANT: Columns must be inserted in the exact order
                            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.TimestampTz);
                            await writer.WriteAsync(item.Ticker, NpgsqlDbType.Text);
                            await writer.WriteAsync(item.Price, NpgsqlDbType.Numeric);
                            await writer.WriteAsync(item.Size, NpgsqlDbType.Integer);
                            await writer.WriteAsync(item.Exchange, NpgsqlDbType.Text);
                        }

                        await writer.CompleteAsync();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task BatchedSaveChangesAsync()
        {
            foreach (TradeWithId[] chunk in Trades.Chunk(MaxBatchSize))
            {
                Context!.AddRange(chunk);
                await Context!.SaveChangesAsync();
                Context.ChangeTracker.Clear();
            }
        }

        [Benchmark]
        public async Task BatchedBulkInsertOptimizedAsync()
        {
            foreach (TradeWithId[] chunk in Trades.Chunk(MaxBatchSize))
            {
                await Context!.BulkInsertOptimizedAsync(chunk.ToList());
            }
        }
    }
}
