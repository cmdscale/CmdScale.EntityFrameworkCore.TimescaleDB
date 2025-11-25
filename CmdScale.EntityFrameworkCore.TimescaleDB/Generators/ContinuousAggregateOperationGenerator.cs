using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class ContinuousAggregateOperationGenerator
    {
        private readonly string quoteString = "\"";
        private readonly SqlBuilderHelper sqlHelper;

        public ContinuousAggregateOperationGenerator(bool isDesignTime = false)
        {
            if (isDesignTime)
            {
                quoteString = "\"\"";
            }

            sqlHelper = new SqlBuilderHelper(quoteString);

        }

        public List<string> Generate(CreateContinuousAggregateOperation operation)
        {
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);
            string parentQualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.ParentName, operation.Schema);

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
                withOptions.Add($"timescaledb.chunk_interval = '{operation.ChunkInterval}'");
            }

            // Build the SELECT list
            List<string> selectList = [];

            // Add time_bucket column
            string timeBucketColumn = $"{quoteString}{operation.TimeBucketSourceColumn}{quoteString}";
            string timeBucketWidthSql = $"'{operation.TimeBucketWidth}'";
            selectList.Add($"time_bucket({timeBucketWidthSql}, {timeBucketColumn}) AS time_bucket");

            // Add GROUP BY columns to SELECT (only actual columns, not SQL expressions)
            foreach (string groupByColumn in operation.GroupByColumns)
            {
                // Check if it's a raw SQL expression or a column name
                bool isRawSqlExpression = groupByColumn.Contains(',') || groupByColumn.Contains('(') || groupByColumn.Contains(' ');
                if (!isRawSqlExpression)
                {
                    selectList.Add($"{quoteString}{groupByColumn}{quoteString}");
                }
            }

            // Build aggregate functions
            foreach (string aggInfo in operation.AggregateFunctions)
            {
                string[] parts = aggInfo.Split(':');
                if (parts.Length != 3)
                {
                    // Skip malformed string
                    continue;
                }

                string alias = parts[0];
                string functionEnumString = parts[1];
                string sourceColumn = parts[2];

                string sqlFunction = GetSqlAggregateFunction(functionEnumString);
                string quotedSourceColumn = $"{quoteString}{sourceColumn}{quoteString}";
                string quotedAlias = $"{quoteString}{alias}{quoteString}";
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

            // Add group by columns
            foreach (string groupByColumn in operation.GroupByColumns)
            {
                if (groupByColumn.Contains(',') || groupByColumn.Contains('(') || groupByColumn.Contains(' '))
                {
                    // It's a raw SQL expression, use as-is
                    groupByList.Add(groupByColumn);
                }
                else
                {
                    // It's a column name, quote it
                    groupByList.Add($"{quoteString}{groupByColumn}{quoteString}");
                }
            }

            // Build the complete CREATE MATERIALIZED VIEW statement as a single string
            var sqlBuilder = new System.Text.StringBuilder();
            sqlBuilder.Append($"CREATE MATERIALIZED VIEW {qualifiedIdentifier}");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"WITH ({string.Join(", ", withOptions)}) AS");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"SELECT {string.Join(", ", selectList)}");
            sqlBuilder.AppendLine();
            sqlBuilder.Append($"FROM {parentQualifiedIdentifier}");

            // Add WHERE clause if specified
            if (!string.IsNullOrWhiteSpace(operation.WhereClause))
            {
                string whereClause = operation.WhereClause.Replace("\"", quoteString);
                sqlBuilder.AppendLine();
                sqlBuilder.Append($"WHERE {whereClause}");
            }

            // Add GROUP BY clause
            if (groupByList.Count > 0)
            {
                sqlBuilder.AppendLine();
                sqlBuilder.Append($"GROUP BY {string.Join(", ", groupByList)}");
            }

            // Add WITH [NO] DATA
            if (operation.WithNoData)
            {
                sqlBuilder.AppendLine();
                sqlBuilder.Append("WITH NO DATA");
            }

            sqlBuilder.Append(';');
            statements.Add(sqlBuilder.ToString());

            return statements;
        }

        public List<string> Generate(AlterContinuousAggregateOperation operation)
        {
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);
            List<string> statements = [];

            // Check for ChunkInterval change
            // Note: TimescaleDB continuous aggregates only support SET for chunk_interval, not RESET
            if (operation.ChunkInterval != operation.OldChunkInterval)
            {
                // Only generate SQL if we have a valid new value to set
                // We cannot RESET chunk_interval as TimescaleDB doesn't support it
                if (!string.IsNullOrEmpty(operation.ChunkInterval))
                {
                    string chunkIntervalSql = $"'{operation.ChunkInterval}'";
                    statements.Add($"ALTER MATERIALIZED VIEW {qualifiedIdentifier} SET (timescaledb.chunk_interval = {chunkIntervalSql});");
                }
                else if (!string.IsNullOrEmpty(operation.OldChunkInterval))
                {
                    // Special case: If new value is null/empty but old value exists,
                    // restore the old value instead of trying to RESET (which is unsupported)
                    string chunkIntervalSql = $"'{operation.OldChunkInterval}'";
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

            return statements;
        }

        public List<string> Generate(DropContinuousAggregateOperation operation)
        {
            string qualifiedIdentifier = sqlHelper.QualifiedIdentifier(operation.MaterializedViewName, operation.Schema);
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
