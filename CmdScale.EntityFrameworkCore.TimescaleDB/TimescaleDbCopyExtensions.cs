using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Npgsql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    public static class TimescaleDbCopyExtensions
    {
        /// <summary>
        /// Performs a high-performance bulk copy of a data collection into a PostgreSQL table or TimescaleDB hypertable
        /// using PostgreSQL's binary COPY command with parallel workers.
        /// </summary>
        /// <typeparam name="T">The entity type of the data being inserted.</typeparam>
        /// <param name="data">The collection of data to be inserted.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="config">A <see cref="TimescaleCopyConfig{T}"/> object that configures the bulk copy operation, including table name, column mappings, and parallelism.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous completion of the entire bulk copy operation.</returns>
        public static async Task BulkCopyAsync<T>(
            this IEnumerable<T> data,
            string connectionString,
            TimescaleCopyConfig<T>? config = null)
        {
            config ??= new TimescaleCopyConfig<T>();

            // Generate the SQL COPY command from the configuration
            string copyCommand = $"COPY \"{config.TableName}\" (\"{string.Join("\", \"", config.ColumnMappings.Keys)}\") FROM STDIN (FORMAT BINARY)";

            // Create parallel workers to ingest the data
            List<Task> tasks = [];
            int totalRecords = data.Count();
            int workerChunkSize = (int)Math.Ceiling((double)totalRecords / config.NumberOfWorkers);

            for (int i = 0; i < config.NumberOfWorkers; i++)
            {
                int startIndex = i * workerChunkSize;
                IEnumerable<T> workerData = [.. data.Skip(startIndex).Take(workerChunkSize)];

                if (!workerData.Any())
                {
                    break;
                }

                tasks.Add(Task.Run(async () =>
                {
                    using NpgsqlConnection connection = new(connectionString);
                    await connection.OpenAsync();

                    for (int j = 0; j < workerData.Count(); j += config.MaxBatchSize)
                    {
                        IEnumerable<T> batch = workerData.Skip(j).Take(config.MaxBatchSize);

                        // Start a binary import stream
                        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copyCommand);
                        foreach (T? item in batch)
                        {
                            await writer.StartRowAsync();

                            // Write each configured column in the specified order
                            foreach (var (Getter, DbType) in config.ColumnMappings.Values)
                            {
                                object? value = Getter(item);
                                await writer.WriteAsync(value, DbType);
                            }
                        }
                        await writer.CompleteAsync();
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }
    }
}

