using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class HypertableSqlGenerator
    {
        public static List<string> Generate(CreateHypertableOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];
            List<string> communityStatements = [];

            StringBuilder createHypertableCall = new();
            createHypertableCall.Append($"SELECT create_hypertable({qualifiedTableName}, '{operation.TimeColumnName}'");
            createHypertableCall.Append(operation.MigrateData ? ", migrate_data => true" : "");

            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
            {
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    createHypertableCall.Append($", chunk_time_interval => {operation.ChunkTimeInterval}::bigint");
                }
                else
                {
                    createHypertableCall.Append($", chunk_time_interval => INTERVAL '{operation.ChunkTimeInterval}'");
                }
            }

            createHypertableCall.Append(");");
            statements.Add(createHypertableCall.ToString());

            List<string> compressionSettings = [];

            bool hasSegmentBy = operation.CompressionSegmentBy != null && operation.CompressionSegmentBy.Count > 0;
            bool hasOrderBy = operation.CompressionOrderBy != null && operation.CompressionOrderBy.Count > 0;
            bool hasChunkSkipping = operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0;

            bool shouldEnableCompression = operation.EnableCompression || hasChunkSkipping || hasSegmentBy || hasOrderBy;

            if (shouldEnableCompression)
            {
                compressionSettings.Add("timescaledb.compress = true");
            }

            if (hasSegmentBy)
            {
                string segmentList = string.Join(", ", operation.CompressionSegmentBy!.Select(SqlBuilderHelper.QuoteIdentifier));
                compressionSettings.Add($"timescaledb.compress_segmentby = '{segmentList}'");
            }

            if (hasOrderBy)
            {
                string orderList = QuoteOrderByList(operation.CompressionOrderBy!);
                compressionSettings.Add($"timescaledb.compress_orderby = '{orderList}'");
            }

            if (compressionSettings.Count > 0)
            {
                communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} SET ({string.Join(", ", compressionSettings)});");
            }

            if (operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0)
            {
                communityStatements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in operation.ChunkSkipColumns)
                {
                    communityStatements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{column}');");
                }
            }

            if (operation.AdditionalDimensions != null && operation.AdditionalDimensions.Count > 0)
            {
                foreach (Dimension dimension in operation.AdditionalDimensions)
                {
                    if (dimension.Type == EDimensionType.Range)
                    {
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

            if (communityStatements.Count > 0)
            {
                statements.Add(WrapCommunityFeatures(communityStatements));
            }
            return statements;
        }

        public static List<string> Generate(AlterHypertableOperation operation)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];
            List<string> communityStatements = [];

            if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
            {
                StringBuilder setChunkTimeInterval = new();
                setChunkTimeInterval.Append($"SELECT set_chunk_time_interval({qualifiedTableName}, ");

                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    setChunkTimeInterval.Append($"{operation.ChunkTimeInterval}::bigint");
                }
                else
                {
                    setChunkTimeInterval.Append($"INTERVAL '{operation.ChunkTimeInterval}'");
                }

                setChunkTimeInterval.Append(");");
                statements.Add(setChunkTimeInterval.ToString());
            }

            List<string> compressionSettings = [];

            static bool ListsChanged(IReadOnlyList<string>? oldList, IReadOnlyList<string>? newList)
            {
                return !(oldList ?? []).SequenceEqual(newList ?? []);
            }

            bool newCompressionState = operation.EnableCompression
                                    || (operation.ChunkSkipColumns?.Count > 0)
                                    || (operation.CompressionSegmentBy?.Count > 0)
                                    || (operation.CompressionOrderBy?.Count > 0);

            bool oldCompressionState = operation.OldEnableCompression
                                    || (operation.OldChunkSkipColumns?.Count > 0)
                                    || (operation.OldCompressionSegmentBy?.Count > 0)
                                    || (operation.OldCompressionOrderBy?.Count > 0);

            if (newCompressionState != oldCompressionState)
            {
                compressionSettings.Add($"timescaledb.compress = {newCompressionState.ToString().ToLower()}");
            }

            if (ListsChanged(operation.OldCompressionSegmentBy, operation.CompressionSegmentBy))
            {
                string val = (operation.CompressionSegmentBy?.Count > 0)
                    ? $"'{string.Join(", ", operation.CompressionSegmentBy.Select(SqlBuilderHelper.QuoteIdentifier))}'"
                    : "''";
                compressionSettings.Add($"timescaledb.compress_segmentby = {val}");
            }

            if (ListsChanged(operation.OldCompressionOrderBy, operation.CompressionOrderBy))
            {
                string val = (operation.CompressionOrderBy?.Count > 0)
                    ? $"'{QuoteOrderByList(operation.CompressionOrderBy)}'"
                    : "''";
                compressionSettings.Add($"timescaledb.compress_orderby = {val}");
            }

            if (compressionSettings.Count > 0)
            {
                communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} SET ({string.Join(", ", compressionSettings)});");
            }

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

            // TimescaleDB does NOT support removing dimensions from hypertables.
            // Once added, a dimension cannot be removed, so only additions are generated.
            IReadOnlyList<Dimension> newDimensions = operation.AdditionalDimensions ?? [];
            IReadOnlyList<Dimension> oldDimensions = operation.OldAdditionalDimensions ?? [];

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

            List<Dimension> removedDimensions = [.. oldDimensions
                .Where(oldDim => !newDimensions.Any(newDim =>
                    oldDim.ColumnName == newDim.ColumnName &&
                    oldDim.Type == newDim.Type))];

            if (removedDimensions.Count > 0)
            {
                string dimensionList = string.Join(", ", removedDimensions.Select(d => $"'{d.ColumnName}'"));
                statements.Add($"-- WARNING: TimescaleDB does not support removing dimensions. The following dimensions cannot be removed: {dimensionList}");
            }

            if (communityStatements.Count > 0)
            {
                statements.Add(WrapCommunityFeatures(communityStatements));
            }
            return statements;
        }

        /// <summary>
        /// Wraps multiple SQL statements in a single license check block to ensure they only run on Community Edition.
        /// </summary>
        private static string WrapCommunityFeatures(List<string> sqlStatements)
        {
            StringBuilder sb = new();
            sb.AppendLine("DO $$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    license TEXT;");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    license := current_setting('timescaledb.license', true);");
            sb.AppendLine("    ");
            sb.AppendLine("    IF license IS NULL OR license != 'apache' THEN");

            foreach (string sql in sqlStatements)
            {
                string cleanSql = sql.TrimEnd(';').Replace("'", "''");
                sb.AppendLine($"        EXECUTE '{cleanSql}';");
            }

            sb.AppendLine("    ELSE");
            sb.AppendLine("        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';");
            sb.AppendLine("    END IF;");
            sb.AppendLine("END $$;");

            return sb.ToString();
        }

        /// <summary>
        /// Quotes the column name within an ORDER BY clause while preserving direction/nulls.
        /// Example: Timestamp DESC -> "Timestamp" DESC
        /// </summary>
        private static string QuoteOrderByList(IEnumerable<string> orderByClauses)
        {
            return string.Join(", ", orderByClauses.Select(clause =>
            {
                string[] parts = clause.Split(' ', 2);
                string col = parts[0];
                string suffix = parts.Length > 1 ? " " + parts[1] : "";

                return SqlBuilderHelper.QuoteIdentifier(col) + suffix;
            }));
        }
    }
}
