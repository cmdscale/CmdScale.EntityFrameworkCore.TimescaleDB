using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class HypertableOperationGenerator
    {
        private readonly string quoteString = "\"";
        private readonly SqlBuilderHelper sqlHelper;

        public HypertableOperationGenerator(bool isDesignTime = false)
        {
            if (isDesignTime)
            {
                quoteString = "\"\"";
            }

            sqlHelper = new SqlBuilderHelper(quoteString);

        }

        public List<string> Generate(CreateHypertableOperation operation)
        {
            string qualifiedTableName = sqlHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements =
            [
                $"SELECT create_hypertable({qualifiedTableName}, '{operation.TimeColumnName}');"
            ];

            // ChunkTimeInterval
            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
            {
                // Check if the interval is a plain number (e.g., for microseconds).
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    // If it's a number, don't wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval({qualifiedTableName}, {operation.ChunkTimeInterval}::bigint);");
                }
                else
                {
                    // If it's a string like '7 days', wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval({qualifiedTableName}, INTERVAL '{operation.ChunkTimeInterval}');");
                }
            }

            // EnableCompression
            if (operation.EnableCompression || operation.ChunkSkipColumns?.Count > 0)
            {
                bool enableCompression = operation.EnableCompression || operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0;
                statements.Add($"ALTER TABLE {qualifiedIdentifier} SET (timescaledb.compress = {enableCompression.ToString().ToLower()});");
            }

            // ChunkSkipColumns
            if (operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0)
            {
                statements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in operation.ChunkSkipColumns)
                {
                    statements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            // AdditionalDimensions
            if (operation.AdditionalDimensions != null && operation.AdditionalDimensions.Count > 0)
            {
                foreach (Dimension dimension in operation.AdditionalDimensions)
                {
                    if (dimension.Type == EDimensionType.Range)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_range('{dimension.ColumnName}', INTERVAL '{dimension.Interval}'));");
                    }
                    else if (dimension.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_hash('{dimension.ColumnName}', {dimension.NumberOfPartitions}));");
                    }
                }
            }

            return statements;
        }

        public List<string> Generate(AlterHypertableOperation operation)
        {
            string qualifiedTableName = sqlHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];

            // Check for ChunkTimeInterval change
            if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
            {
                // Check if the interval is a plain number (e.g., for microseconds).
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    // If it's a number, don't wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval({qualifiedTableName}, {operation.ChunkTimeInterval}::bigint);");
                }
                else
                {
                    // If it's a string like '7 days', wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval({qualifiedTableName}, INTERVAL '{operation.ChunkTimeInterval}');");
                }
            }

            // Check for EnableCompression change
            bool newCompressionState = operation.EnableCompression || operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Any();
            bool oldCompressionState = operation.OldEnableCompression || operation.OldChunkSkipColumns != null && operation.OldChunkSkipColumns.Any();

            if (newCompressionState != oldCompressionState)
            {
                string compressionValue = newCompressionState.ToString().ToLower();
                statements.Add($"ALTER TABLE {qualifiedIdentifier} SET (timescaledb.compress = {compressionValue});");
            }

            // Handle ChunkSkipColumns
            IReadOnlyList<string> newColumns = operation.ChunkSkipColumns ?? [];
            IReadOnlyList<string> oldColumns = operation.OldChunkSkipColumns ?? [];
            List<string> addedColumns = [.. newColumns.Except(oldColumns)];

            if (addedColumns.Count != 0)
            {
                statements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in addedColumns)
                {
                    statements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            List<string> removedColumns = [.. oldColumns.Except(newColumns)];
            if (removedColumns.Count != 0)
            {
                foreach (string column in removedColumns)
                {
                    statements.Add($"SELECT disable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            return statements;
        }
    }
}

