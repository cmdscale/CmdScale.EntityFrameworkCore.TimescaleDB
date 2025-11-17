using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Text;

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

            List<string> statements = [];
            List<string> communityStatements = [];

            // Build create_hypertable with chunk_time_interval if provided
            var createHypertableCall = new StringBuilder();
            createHypertableCall.Append($"SELECT create_hypertable({qualifiedTableName}, '{operation.TimeColumnName}'");
            createHypertableCall.Append(operation.MigrateData ? ", migrate_data => true" : "");

            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
            {
                // Check if the interval is a plain number (e.g., for microseconds).
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    // If it's a number, don't wrap it in quotes.
                    createHypertableCall.Append($", chunk_time_interval => {operation.ChunkTimeInterval}::bigint");
                }
                else
                {
                    // If it's a string like '7 days', wrap it in quotes.
                    createHypertableCall.Append($", chunk_time_interval => INTERVAL '{operation.ChunkTimeInterval}'");
                }
            }

            createHypertableCall.Append(");");
            statements.Add(createHypertableCall.ToString());

            // EnableCompression (Community Edition only)
            if (operation.EnableCompression || operation.ChunkSkipColumns?.Count > 0)
            {
                bool enableCompression = operation.EnableCompression || operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0;
                communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} SET (timescaledb.compress = {enableCompression.ToString().ToLower()});");
            }

            // ChunkSkipColumns (Community Edition only)
            if (operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0)
            {
                communityStatements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in operation.ChunkSkipColumns)
                {
                    communityStatements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            // AdditionalDimensions (Available in both editions)
            if (operation.AdditionalDimensions != null && operation.AdditionalDimensions.Count > 0)
            {
                foreach (Dimension dimension in operation.AdditionalDimensions)
                {
                    if (dimension.Type == EDimensionType.Range)
                    {
                        // Detect if interval is numeric (integer range) or time-based (timestamp range)
                        bool isIntegerRange = long.TryParse(dimension.Interval, out _);
                        string intervalExpression = isIntegerRange
                            ? dimension.Interval!
                            : $"INTERVAL '{dimension.Interval}'";

                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_range('{dimension.ColumnName}', {intervalExpression}));");
                    }
                    else if (dimension.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_hash('{dimension.ColumnName}', {dimension.NumberOfPartitions}));");
                    }
                }
            }

            // Add wrapped community statements if any exist
            if (communityStatements.Count > 0)
            {
                statements.Add(WrapCommunityFeatures(communityStatements));
            }
            return statements;
        }

        public List<string> Generate(AlterHypertableOperation operation)
        {
            string qualifiedTableName = sqlHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];
            List<string> communityStatements = [];

            // Check for ChunkTimeInterval change (Available in both editions)
            if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
            {
                var setChunkTimeInterval = new StringBuilder();
                setChunkTimeInterval.Append($"SELECT set_chunk_time_interval({qualifiedTableName}, ");

                // Check if the interval is a plain number (e.g., for microseconds).
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    // If it's a number, don't wrap it in quotes.
                    setChunkTimeInterval.Append($"{operation.ChunkTimeInterval}::bigint");
                }
                else
                {
                    // If it's a string like '7 days', wrap it in quotes.
                    setChunkTimeInterval.Append($"INTERVAL '{operation.ChunkTimeInterval}'");
                }

                setChunkTimeInterval.Append(");");
                statements.Add(setChunkTimeInterval.ToString());
            }

            // Check for EnableCompression change (Community Edition only)
            bool newCompressionState = operation.EnableCompression || operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Any();
            bool oldCompressionState = operation.OldEnableCompression || operation.OldChunkSkipColumns != null && operation.OldChunkSkipColumns.Any();

            if (newCompressionState != oldCompressionState)
            {
                string compressionValue = newCompressionState.ToString().ToLower();
                communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} SET (timescaledb.compress = {compressionValue});");
            }

            // Handle ChunkSkipColumns (Community Edition only)
            IReadOnlyList<string> newColumns = operation.ChunkSkipColumns ?? [];
            IReadOnlyList<string> oldColumns = operation.OldChunkSkipColumns ?? [];
            List<string> addedColumns = [.. newColumns.Except(oldColumns)];

            if (addedColumns.Count != 0)
            {
                communityStatements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in addedColumns)
                {
                    communityStatements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            List<string> removedColumns = [.. oldColumns.Except(newColumns)];
            if (removedColumns.Count != 0)
            {
                foreach (string column in removedColumns)
                {
                    communityStatements.Add($"SELECT disable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            // Handle AdditionalDimensions - only add new dimensions
            // NOTE: TimescaleDB does NOT support removing dimensions from hypertables.
            // Once a dimension is added, it cannot be removed. Therefore, we only generate
            // SQL for adding new dimensions and ignore dimension removals.
            IReadOnlyList<Dimension> newDimensions = operation.AdditionalDimensions ?? [];
            IReadOnlyList<Dimension> oldDimensions = operation.OldAdditionalDimensions ?? [];

            // Find dimensions that are in new but not in old (added dimensions)
            foreach (Dimension newDim in newDimensions)
            {
                bool exists = oldDimensions.Any(oldDim =>
                    oldDim.ColumnName == newDim.ColumnName &&
                    oldDim.Type == newDim.Type &&
                    oldDim.Interval == newDim.Interval &&
                    oldDim.NumberOfPartitions == newDim.NumberOfPartitions);

                if (!exists)
                {
                    if (newDim.Type == EDimensionType.Range)
                    {
                        // Detect if interval is numeric (integer range) or time-based (timestamp range)
                        bool isIntegerRange = long.TryParse(newDim.Interval, out _);
                        string intervalExpression = isIntegerRange
                            ? newDim.Interval!
                            : $"INTERVAL '{newDim.Interval}'";

                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_range('{newDim.ColumnName}', {intervalExpression}));");
                    }
                    else if (newDim.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_hash('{newDim.ColumnName}', {newDim.NumberOfPartitions}));");
                    }
                }
            }

            // Warn if dimensions were removed (which cannot be reversed in TimescaleDB)
            List<Dimension> removedDimensions = [.. oldDimensions
                .Where(oldDim => !newDimensions.Any(newDim =>
                    oldDim.ColumnName == newDim.ColumnName &&
                    oldDim.Type == newDim.Type))];

            if (removedDimensions.Count > 0)
            {
                string dimensionList = string.Join(", ", removedDimensions.Select(d => $"'{d.ColumnName}'"));
                statements.Add($"-- WARNING: TimescaleDB does not support removing dimensions. The following dimensions cannot be removed: {dimensionList}");
            }


            // Add wrapped community statements if any exist
            if (communityStatements.Count > 0)
            {
                statements.Add(WrapCommunityFeatures(communityStatements));
            }
            return statements;
        }

        /// <summary>
        /// Wraps multiple SQL statements in a single license check block to ensure they only run on Community Edition
        /// </summary>
        private string WrapCommunityFeatures(List<string> sqlStatements)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DO $$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    license TEXT;");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    license := current_setting('timescaledb.license', true);");
            sb.AppendLine("    ");
            sb.AppendLine("    IF license IS NULL OR license != 'apache' THEN");

            foreach (string sql in sqlStatements)
            {
                sb.AppendLine($"        {sql}");
            }

            sb.AppendLine("    ELSE");
            sb.AppendLine("        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Enterprise Edition';");
            sb.AppendLine("    END IF;");
            sb.AppendLine("END $$;");

            return sb.ToString();
        }
    }
}