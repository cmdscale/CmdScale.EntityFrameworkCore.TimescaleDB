using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class HypertableSqlGenerator
    {
        private const string CommunityWarning = "Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition";

        public static List<string> Generate(CreateHypertableOperation operation, bool useLegacyCompressionNames = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];
            List<string> communityStatements = [];

            StringBuilder createHypertableCall = new();
            createHypertableCall.Append($"SELECT create_hypertable({qualifiedTableName}, '{SqlBuilderHelper.EscapeStringLiteral(operation.TimeColumnName)}'");
            createHypertableCall.Append(operation.MigrateData ? ", migrate_data => true" : "");

            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
            {
                createHypertableCall.Append($", chunk_time_interval => {SqlBuilderHelper.IntervalOrBigint(operation.ChunkTimeInterval)}");
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
                compressionSettings.Add($"{CompressionSettingsSqlHelper.CompressOptionName(useLegacyCompressionNames)} = true");
            }

            if (hasSegmentBy)
            {
                string segmentList = string.Join(", ", operation.CompressionSegmentBy!.Select(SqlBuilderHelper.QuoteIdentifier));
                compressionSettings.Add($"{CompressionSettingsSqlHelper.SegmentByOptionName(useLegacyCompressionNames)} = '{SqlBuilderHelper.EscapeStringLiteral(segmentList)}'");
            }

            if (hasOrderBy)
            {
                string orderList = CompressionSettingsSqlHelper.QuoteOrderByList(operation.CompressionOrderBy!);
                compressionSettings.Add($"{CompressionSettingsSqlHelper.OrderByOptionName(useLegacyCompressionNames)} = '{SqlBuilderHelper.EscapeStringLiteral(orderList)}'");
            }

            if (operation.CompressionSparseIndex != null)
            {
                compressionSettings.Add($"timescaledb.sparse_index = '{SqlBuilderHelper.EscapeStringLiteral(operation.CompressionSparseIndex)}'");
            }

            if (!string.IsNullOrEmpty(operation.CompressChunkTimeInterval))
            {
                compressionSettings.Add($"timescaledb.compress_chunk_time_interval = '{SqlBuilderHelper.EscapeStringLiteral(operation.CompressChunkTimeInterval)}'");
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
                    communityStatements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{SqlBuilderHelper.EscapeStringLiteral(column)}');");
                }
            }

            if (operation.AdditionalDimensions != null && operation.AdditionalDimensions.Count > 0)
            {
                foreach (Dimension dimension in operation.AdditionalDimensions)
                {
                    if (dimension.Type == EDimensionType.Range)
                    {
                        string intervalExpression = SqlBuilderHelper.IntervalOrBigint(dimension.Interval ?? string.Empty);

                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_range('{SqlBuilderHelper.EscapeStringLiteral(dimension.ColumnName)}', {intervalExpression}));");
                    }
                    else if (dimension.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_hash('{SqlBuilderHelper.EscapeStringLiteral(dimension.ColumnName)}', {dimension.NumberOfPartitions}));");
                    }
                }
            }

            if (communityStatements.Count > 0)
            {
                statements.Add(SqlBuilderHelper.WrapCommunityFeatures(communityStatements, CommunityWarning));
            }
            return statements;
        }

        public static List<string> Generate(AlterHypertableOperation operation, bool useLegacyCompressionNames = false)
        {
            string qualifiedTableName = SqlBuilderHelper.Regclass(operation.TableName, operation.Schema);
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.TableName, operation.Schema);

            List<string> statements = [];
            List<string> communityStatements = [];

            if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
            {
                StringBuilder setChunkTimeInterval = new();
                setChunkTimeInterval.Append($"SELECT set_chunk_time_interval({qualifiedTableName}, ");

                setChunkTimeInterval.Append(SqlBuilderHelper.IntervalOrBigint(operation.ChunkTimeInterval));

                setChunkTimeInterval.Append(");");
                statements.Add(setChunkTimeInterval.ToString());
            }

            ApplyCompressionChanges(operation, qualifiedIdentifier, communityStatements, useLegacyCompressionNames);
            ApplyChunkSkippingChanges(operation, qualifiedTableName, communityStatements);
            ApplyDimensionChanges(operation, qualifiedTableName, statements);

            if (communityStatements.Count > 0)
            {
                statements.Add(SqlBuilderHelper.WrapCommunityFeatures(communityStatements, CommunityWarning));
            }
            return statements;
        }

        private static void ApplyCompressionChanges(
            AlterHypertableOperation operation,
            string qualifiedIdentifier,
            List<string> communityStatements,
            bool useLegacyCompressionNames)
        {
            List<string> compressionSettings = [];

            bool newCompressionState = CompressionSettingsSqlHelper.IsCompressionEnabled(
                operation.EnableCompression, operation.CompressionSegmentBy, operation.CompressionOrderBy, operation.ChunkSkipColumns);

            bool oldCompressionState = CompressionSettingsSqlHelper.IsCompressionEnabled(
                operation.OldEnableCompression, operation.OldCompressionSegmentBy, operation.OldCompressionOrderBy, operation.OldChunkSkipColumns);

            if (newCompressionState != oldCompressionState)
            {
                compressionSettings.Add($"{CompressionSettingsSqlHelper.CompressOptionName(useLegacyCompressionNames)} = {newCompressionState.ToString().ToLower()}");
            }

            if (!CompressionDiffHelper.AreStringListsEqual(operation.OldCompressionSegmentBy, operation.CompressionSegmentBy))
            {
                string val = (operation.CompressionSegmentBy?.Count > 0)
                    ? $"'{SqlBuilderHelper.EscapeStringLiteral(string.Join(", ", operation.CompressionSegmentBy.Select(SqlBuilderHelper.QuoteIdentifier)))}'"
                    : "''";
                compressionSettings.Add($"{CompressionSettingsSqlHelper.SegmentByOptionName(useLegacyCompressionNames)} = {val}");
            }

            if (!CompressionDiffHelper.AreStringListsEqual(operation.OldCompressionOrderBy, operation.CompressionOrderBy))
            {
                string val = (operation.CompressionOrderBy?.Count > 0)
                    ? $"'{SqlBuilderHelper.EscapeStringLiteral(CompressionSettingsSqlHelper.QuoteOrderByList(operation.CompressionOrderBy))}'"
                    : "''";
                compressionSettings.Add($"{CompressionSettingsSqlHelper.OrderByOptionName(useLegacyCompressionNames)} = {val}");
            }

            if (operation.CompressionSparseIndex != operation.OldCompressionSparseIndex)
            {
                if (operation.CompressionSparseIndex != null)
                {
                    compressionSettings.Add($"timescaledb.sparse_index = '{SqlBuilderHelper.EscapeStringLiteral(operation.CompressionSparseIndex)}'");
                }
                else
                {
                    communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} RESET (timescaledb.sparse_index);");
                }
            }

            if (operation.CompressChunkTimeInterval != operation.OldCompressChunkTimeInterval)
            {
                if (operation.CompressChunkTimeInterval != null)
                {
                    compressionSettings.Add($"timescaledb.compress_chunk_time_interval = '{SqlBuilderHelper.EscapeStringLiteral(operation.CompressChunkTimeInterval)}'");
                }
                else
                {
                    // RESET is rejected for this option ("only columnstore options segmentby and
                    // orderby can be reset"); setting the interval to '0' clears it instead.
                    compressionSettings.Add("timescaledb.compress_chunk_time_interval = '0'");
                }
            }

            if (compressionSettings.Count > 0)
            {
                communityStatements.Add($"ALTER TABLE {qualifiedIdentifier} SET ({string.Join(", ", compressionSettings)});");
            }
        }

        private static void ApplyChunkSkippingChanges(
            AlterHypertableOperation operation,
            string qualifiedTableName,
            List<string> communityStatements)
        {
            IReadOnlyList<string> newColumns = operation.ChunkSkipColumns ?? [];
            IReadOnlyList<string> oldColumns = operation.OldChunkSkipColumns ?? [];
            List<string> addedColumns = [.. newColumns.Except(oldColumns)];

            if (addedColumns.Count != 0)
            {
                communityStatements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in addedColumns)
                {
                    communityStatements.Add($"SELECT enable_chunk_skipping({qualifiedTableName}, '{SqlBuilderHelper.EscapeStringLiteral(column)}');");
                }
            }

            List<string> removedColumns = [.. oldColumns.Except(newColumns)];
            if (removedColumns.Count != 0)
            {
                foreach (string column in removedColumns)
                {
                    communityStatements.Add($"SELECT disable_chunk_skipping({qualifiedTableName}, '{SqlBuilderHelper.EscapeStringLiteral(column)}');");
                }
            }
        }

        private static void ApplyDimensionChanges(
            AlterHypertableOperation operation,
            string qualifiedTableName,
            List<string> statements)
        {
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
                        string intervalExpression = SqlBuilderHelper.IntervalOrBigint(newDim.Interval ?? string.Empty);

                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_range('{SqlBuilderHelper.EscapeStringLiteral(newDim.ColumnName)}', {intervalExpression}));");
                    }
                    else if (newDim.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension({qualifiedTableName}, by_hash('{SqlBuilderHelper.EscapeStringLiteral(newDim.ColumnName)}', {newDim.NumberOfPartitions}));");
                    }
                }
            }

            List<Dimension> removedDimensions = [.. oldDimensions
                .Where(oldDim => !newDimensions.Any(newDim =>
                    oldDim.ColumnName == newDim.ColumnName &&
                    oldDim.Type == newDim.Type))];

            if (removedDimensions.Count > 0)
            {
                string dimensionList = string.Join(", ", removedDimensions.Select(d => $"'{SqlBuilderHelper.EscapeStringLiteral(d.ColumnName)}'"));
                statements.Add($"-- WARNING: TimescaleDB does not support removing dimensions. The following dimensions cannot be removed: {dimensionList}");
            }
        }
    }
}
