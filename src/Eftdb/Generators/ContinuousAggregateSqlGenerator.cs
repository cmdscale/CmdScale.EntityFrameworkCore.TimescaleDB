using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    internal class ContinuousAggregateSqlGenerator
    {
        private const string CommunityWarning = "Skipping Community Edition features (compression) - not available in Apache Edition";
        private const string AlterDdl = "ALTER MATERIALIZED VIEW";

        public static List<string> Generate(CreateContinuousAggregateOperation operation, bool useLegacyCompressionNames = false)
        {
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);

            List<string> statements = [];

            // Build WITH options
            List<string> withOptions =
            [
                "timescaledb.continuous",
                $"timescaledb.create_group_indexes = {operation.CreateGroupIndexes.ToString().ToLower()}",
                $"timescaledb.materialized_only = {operation.MaterializedOnly.ToString().ToLower()}"
            ];

            // Add optional chunk_interval if specified
            if (!string.IsNullOrEmpty(operation.ChunkInterval))
            {
                withOptions.Add($"timescaledb.chunk_interval = '{SqlBuilderHelper.EscapeStringLiteral(operation.ChunkInterval)}'");
            }

            // Raw-SQL path required for scaffolding round-trips
            if (!string.IsNullOrWhiteSpace(operation.ViewDefinition))
            {
                return GenerateFromRawViewDefinition(operation, qualifiedIdentifier, withOptions, useLegacyCompressionNames);
            }

            return GenerateFromStructuredQuery(operation, qualifiedIdentifier, withOptions, useLegacyCompressionNames);
        }

        private static List<string> GenerateFromRawViewDefinition(
            CreateContinuousAggregateOperation operation,
            string qualifiedIdentifier,
            List<string> withOptions,
            bool useLegacyCompressionNames)
        {
            List<string> statements = [];

            StringBuilder rawSqlBuilder = new();
            rawSqlBuilder.Append($"CREATE MATERIALIZED VIEW {qualifiedIdentifier}");
            rawSqlBuilder.AppendLine();
            rawSqlBuilder.Append($"WITH ({string.Join(", ", withOptions)}) AS");
            rawSqlBuilder.AppendLine();
            rawSqlBuilder.Append(operation.ViewDefinition!.Trim().TrimEnd(';'));
            if (operation.WithNoData)
            {
                rawSqlBuilder.AppendLine();
                rawSqlBuilder.Append("WITH NO DATA");
            }
            rawSqlBuilder.Append(';');
            statements.Add(rawSqlBuilder.ToString());

            CompressionSettingsSqlHelper.AppendCreateCompressionStatements(
                statements,
                operation.MaterializedViewName,
                operation.Schema,
                operation.EnableCompression,
                operation.CompressionSegmentBy,
                operation.CompressionOrderBy,
                AlterDdl,
                CommunityWarning,
                useLegacyCompressionNames);

            return statements;
        }

        private static List<string> GenerateFromStructuredQuery(
            CreateContinuousAggregateOperation operation,
            string qualifiedIdentifier,
            List<string> withOptions,
            bool useLegacyCompressionNames)
        {
            List<string> statements = [];
            string parentQualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.ParentName, operation.Schema);

            // Build the SELECT list
            List<string> selectList = [];

            // Add time_bucket column
            string timeBucketColumn = $"{SqlBuilderHelper.QuoteIdentifier(operation.TimeBucketSourceColumn)}";
            string timeBucketWidthSql = $"'{SqlBuilderHelper.EscapeStringLiteral(operation.TimeBucketWidth)}'";
            selectList.Add($"time_bucket({timeBucketWidthSql}, {timeBucketColumn}) AS time_bucket");

            // Add GROUP BY columns to SELECT (only actual columns, not SQL expressions)
            foreach (string groupByColumn in operation.GroupByColumns)
            {
                bool isRawSqlExpression = groupByColumn.Contains(',') || groupByColumn.Contains('(') || groupByColumn.Contains(' ');
                if (!isRawSqlExpression)
                {
                    selectList.Add($"{SqlBuilderHelper.QuoteIdentifier(groupByColumn)}");
                }
            }

            // Build aggregate functions
            foreach (string aggInfo in operation.AggregateFunctions)
            {
                string[] parts = aggInfo.Split(':');
                if (parts.Length != 3)
                {
                    continue;
                }

                string alias = parts[0];
                string functionEnumString = parts[1];
                string sourceColumn = parts[2];

                string sqlFunction = GetSqlAggregateFunction(functionEnumString);
                // The COUNT(*) wildcard is not an identifier and must stay unquoted.
                string quotedSourceColumn = sourceColumn == "*" ? "*" : SqlBuilderHelper.QuoteIdentifier(sourceColumn);
                string quotedAlias = $"{SqlBuilderHelper.QuoteIdentifier(alias)}";
                string aggregateExpression;

                // Handle special TimescaleDB aggregates 'first' and 'last'
                // which require (value_column, time_column)
                if (sqlFunction == "first" || sqlFunction == "last")
                {
                    aggregateExpression = $"{sqlFunction}({quotedSourceColumn}, {timeBucketColumn})";
                }
                else
                {
                    aggregateExpression = $"{sqlFunction}({quotedSourceColumn})";
                }

                selectList.Add($"{aggregateExpression} AS {quotedAlias}");
            }

            // Build the GROUP BY list
            List<string> groupByList = [];
            if (operation.TimeBucketGroupBy)
            {
                groupByList.Add("time_bucket");
            }

            foreach (string groupByColumn in operation.GroupByColumns)
            {
                if (groupByColumn.Contains(',') || groupByColumn.Contains('(') || groupByColumn.Contains(' '))
                {
                    groupByList.Add(groupByColumn);
                }
                else
                {
                    groupByList.Add($"{SqlBuilderHelper.QuoteIdentifier(groupByColumn)}");
                }
            }

            // Build the complete CREATE MATERIALIZED VIEW statement as a single string
            StringBuilder sqlBuilder = new();
            sqlBuilder.Append($"CREATE MATERIALIZED VIEW {qualifiedIdentifier}");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"WITH ({string.Join(", ", withOptions)}) AS");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"SELECT {string.Join(", ", selectList)}");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"FROM {parentQualifiedIdentifier}");

            if (!string.IsNullOrWhiteSpace(operation.WhereClause))
            {
                sqlBuilder.AppendLine();
                sqlBuilder.Append($"WHERE {operation.WhereClause}");
            }

            if (groupByList.Count > 0)
            {
                sqlBuilder.AppendLine();
                sqlBuilder.Append($"GROUP BY {string.Join(", ", groupByList)}");
            }

            if (operation.WithNoData)
            {
                sqlBuilder.AppendLine();
                sqlBuilder.Append("WITH NO DATA");
            }

            sqlBuilder.Append(';');
            statements.Add(sqlBuilder.ToString());

            CompressionSettingsSqlHelper.AppendCreateCompressionStatements(
                statements,
                operation.MaterializedViewName,
                operation.Schema,
                operation.EnableCompression,
                operation.CompressionSegmentBy,
                operation.CompressionOrderBy,
                AlterDdl,
                CommunityWarning,
                useLegacyCompressionNames);

            return statements;
        }

        public static List<string> Generate(AlterContinuousAggregateOperation operation, bool useLegacyCompressionNames = false)
        {
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);
            List<string> statements = [];

            // Check for ChunkInterval change
            // Note: TimescaleDB continuous aggregates only support SET for chunk_interval, not RESET
            if (operation.ChunkInterval != operation.OldChunkInterval)
            {
                // Only generate SQL if we have a valid new value to set
                // We cannot RESET chunk_interval as TimescaleDB doesn't support it
                if (!string.IsNullOrEmpty(operation.ChunkInterval))
                {
                    string chunkIntervalSql = $"'{SqlBuilderHelper.EscapeStringLiteral(operation.ChunkInterval)}'";
                    statements.Add($"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET (timescaledb.chunk_interval = {chunkIntervalSql});");
                }
                else if (!string.IsNullOrEmpty(operation.OldChunkInterval))
                {
                    // Special case: If new value is null/empty but old value exists,
                    // restore the old value instead of trying to RESET (which is unsupported)
                    string chunkIntervalSql = $"'{SqlBuilderHelper.EscapeStringLiteral(operation.OldChunkInterval)}'";
                    statements.Add($"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET (timescaledb.chunk_interval = {chunkIntervalSql});");
                }
            }

            // Check for CreateGroupIndexes change
            if (operation.CreateGroupIndexes != operation.OldCreateGroupIndexes)
            {
                string createGroupIndexesValue = operation.CreateGroupIndexes.ToString().ToLower();
                statements.Add($"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET (timescaledb.create_group_indexes = {createGroupIndexesValue});");
            }

            // Check for MaterializedOnly change
            if (operation.MaterializedOnly != operation.OldMaterializedOnly)
            {
                string materializedOnlyValue = operation.MaterializedOnly.ToString().ToLower();
                statements.Add($"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET (timescaledb.materialized_only = {materializedOnlyValue});");
            }

            List<string> compressionSettings = CompressionSettingsSqlHelper.BuildAlterCompressionSettings(
                operation.EnableCompression,
                operation.CompressionSegmentBy,
                operation.CompressionOrderBy,
                operation.OldEnableCompression,
                operation.OldCompressionSegmentBy,
                operation.OldCompressionOrderBy,
                useLegacyCompressionNames);

            if (compressionSettings.Count > 0)
            {
                string setClause = $"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET ({string.Join(", ", compressionSettings)});";
                statements.Add(SqlBuilderHelper.WrapCommunityFeatures([setClause], CommunityWarning));
            }

            return statements;
        }

        public static List<string> Generate(DropContinuousAggregateOperation operation)
        {
            string qualifiedIdentifier = SqlBuilderHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);
            List<string> statements = [];

            statements.Add($"DROP MATERIALIZED VIEW IF EXISTS {qualifiedIdentifier};");

            return statements;
        }

        /// <summary>
        /// Translates the string representation of EAggregateFunction into a SQL function.
        /// </summary>
        private static string GetSqlAggregateFunction(string functionEnumString)
        {
            switch (functionEnumString)
            {
                case "Avg":
                    return "AVG";
                case "Max":
                    return "MAX";
                case "Min":
                    return "MIN";
                case "Sum":
                    return "SUM";
                case "Count":
                    return "COUNT";
                case "First":
                    return "first";
                case "Last":
                    return "last";
                default:
                    throw new NotSupportedException($"The aggregate function '{functionEnumString}' is not supported by the generator.");
            }
        }
    }
}
