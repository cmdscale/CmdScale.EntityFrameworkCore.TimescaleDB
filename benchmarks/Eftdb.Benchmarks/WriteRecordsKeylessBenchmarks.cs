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
    public class WriteRecordsKeylessBenchmarks : WriteRecordsBenchmarkBase<Trade>
    {
        [Params(100_000, 500_000)]
        public new int NumberOfRecords;

        [Params(25_000, 50_000, 100_000)]
        public new int MaxBatchSize;

        [Params(8)]
        public new int NumberOfWorkers;

        private readonly List<Task> tasks = [];
        private int totalRecords;
        private int workerChunkSize;

        protected override Trade CreateTradeInstance(int index, DateTime baseTimestamp, string ticker, Random random)
        {
            return new Trade
            {
                Timestamp = baseTimestamp.AddMicroseconds(index),
                Ticker = ticker,
                Price = (decimal)(100 + random.NextDouble() * 400),
                Size = random.Next(1, 100)
            };
        }

        protected override string GetTableName() => "Trades";

        [Benchmark]
        public async Task BulkCopyAsync()
        {
            TimescaleCopyConfig<Trade> config = new TimescaleCopyConfig<Trade>()
                .ToTable(GetTableName())
                .WithWorkers(NumberOfWorkers)
                .WithBatchSize(MaxBatchSize);

            await Trades.BulkCopyAsync(ConnectionString, config);
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

                List<Trade> workerData = [.. Trades.Skip(startIndex).Take(currentWorkerDataSize)];
                tasks.Add(Task.Run(async () =>
                {
                    // Open new connection to DB
                    using NpgsqlConnection connection = new(ConnectionString);
                    await connection.OpenAsync();

                    // Command to copy data in a binary format from a client-application 
                    string copyCommand = "COPY \"Trades\" (\"Time\", \"Value\", \"SegmentId\", \"SignalId\") FROM STDIN (FORMAT BINARY)";

                    for (int j = 0; j < workerData.Count; j += MaxBatchSize)
                    {
                        List<Trade> currentBatch = [.. workerData.Skip(j).Take(MaxBatchSize)];

                        // Start a binary import stream
                        await using NpgsqlBinaryImporter writer = connection.BeginBinaryImport(copyCommand);
                        foreach (Trade item in currentBatch)
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
        public async Task BatchedBulkInsertOptimizedAsync()
        {
            foreach (Trade[] chunk in Trades.Chunk(MaxBatchSize))
            {
                await Context!.BulkInsertOptimizedAsync(chunk.ToList());
            }
        }
    }
}
