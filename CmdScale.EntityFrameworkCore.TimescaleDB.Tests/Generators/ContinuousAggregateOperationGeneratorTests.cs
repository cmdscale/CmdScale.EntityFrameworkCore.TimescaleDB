using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    /// <summary>
    /// Tests for ContinuousAggregateOperationGenerator ensuring correct SQL generation
    /// according to TimescaleDB requirements for continuous aggregates.
    /// </summary>
    public class ContinuousAggregateOperationGeneratorTests
    {
        /// <summary>
        /// Helper to run the generator and capture design-time C# code output.
        /// </summary>
        private static string GetDesignTimeCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();
            ContinuousAggregateOperationGenerator generator = new(isDesignTime: true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        /// <summary>
        /// Helper to run the generator and capture runtime SQL output.
        /// </summary>
        private static string GetRuntimeSql(dynamic operation)
        {
            ContinuousAggregateOperationGenerator generator = new(isDesignTime: false);
            List<string> statements = generator.Generate(operation);
            return string.Join("\n", statements);
        }

        #region CreateContinuousAggregateOperation Tests - Design Time

        [Fact]
        public void DesignTime_Create_MinimalAggregate_GeneratesCorrectCSharpCode()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_metrics",
                Schema = "public",
                ParentName = "metrics",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["avg_value:Avg:value"],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"public"""".""""hourly_metrics""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false) AS
                SELECT time_bucket('1 hour', """"timestamp"""") AS time_bucket, AVG(""""value"""") AS """"avg_value""""
                FROM """"public"""".""""metrics""""
                GROUP BY time_bucket;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithAllStandardAggregates_GeneratesCorrectCode()
        {
            // Arrange - Test all standard aggregate functions (AVG, MAX, MIN, SUM, COUNT)
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "daily_stats",
                Schema = "analytics",
                ParentName = "sensor_data",
                TimeBucketWidth = "1 day",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "avg_temp:Avg:temperature",
                    "max_temp:Max:temperature",
                    "min_temp:Min:temperature",
                    "total_readings:Count:id",
                    "sum_voltage:Sum:voltage"
                ],
                GroupByColumns = [],
                CreateGroupIndexes = true,
                MaterializedOnly = true,
                WithNoData = true
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"analytics"""".""""daily_stats""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = true, timescaledb.materialized_only = true) AS
                SELECT time_bucket('1 day', """"time"""") AS time_bucket, AVG(""""temperature"""") AS """"avg_temp"""", MAX(""""temperature"""") AS """"max_temp"""", MIN(""""temperature"""") AS """"min_temp"""", COUNT(""""id"""") AS """"total_readings"""", SUM(""""voltage"""") AS """"sum_voltage""""
                FROM """"analytics"""".""""sensor_data""""
                GROUP BY time_bucket
                WITH NO DATA;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithTimescaleDBFirstLastFunctions_GeneratesCorrectSyntax()
        {
            // Arrange - TimescaleDB first() and last() require (value, time) parameter ordering
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "price_aggregates",
                Schema = "public",
                ParentName = "trades",
                TimeBucketWidth = "5 minutes",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "first_price:First:price",
                    "last_price:Last:price"
                ],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"public"""".""""price_aggregates""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false) AS
                SELECT time_bucket('5 minutes', """"timestamp"""") AS time_bucket, first(""""price"""", """"timestamp"""") AS """"first_price"""", last(""""price"""", """"timestamp"""") AS """"last_price""""
                FROM """"public"""".""""trades""""
                GROUP BY time_bucket;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithGroupByColumns_GeneratesCorrectGrouping()
        {
            // Arrange - Test GROUP BY with multiple columns
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "sales_by_region",
                Schema = "public",
                ParentName = "sales",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "sale_time",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["total_amount:Sum:amount"],
                GroupByColumns = ["region", "store_id"],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"public"""".""""sales_by_region""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false) AS
                SELECT time_bucket('1 hour', """"sale_time"""") AS time_bucket, """"region"""", """"store_id"""", SUM(""""amount"""") AS """"total_amount""""
                FROM """"public"""".""""sales""""
                GROUP BY time_bucket, """"region"""", """"store_id"""";
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithWhereClause_GeneratesCorrectFiltering()
        {
            // Arrange - Test WHERE clause filtering
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "high_value_trades",
                Schema = "public",
                ParentName = "trades",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["avg_price:Avg:price"],
                GroupByColumns = [],
                WhereClause = "\"price\" > 100 AND \"volume\" > 1000",
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"public"""".""""high_value_trades""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false) AS
                SELECT time_bucket('1 hour', """"timestamp"""") AS time_bucket, AVG(""""price"""") AS """"avg_price""""
                FROM """"public"""".""""trades""""
                WHERE """"price"""" > 100 AND """"volume"""" > 1000
                GROUP BY time_bucket;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithChunkInterval_GeneratesCorrectOption()
        {
            // Arrange - Test custom chunk_interval
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "monthly_summary",
                Schema = "public",
                ParentName = "events",
                TimeBucketWidth = "1 month",
                TimeBucketSourceColumn = "event_time",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["event_count:Count:id"],
                GroupByColumns = [],
                ChunkInterval = "7 days",
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            string expected = @".Sql(@""
                CREATE MATERIALIZED VIEW """"public"""".""""monthly_summary""""
                WITH (timescaledb.continuous, timescaledb.create_group_indexes = false, timescaledb.materialized_only = false, timescaledb.chunk_interval = '7 days') AS
                SELECT time_bucket('1 month', """"event_time"""") AS time_bucket, COUNT(""""id"""") AS """"event_count""""
                FROM """"public"""".""""events""""
                GROUP BY time_bucket;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region CreateContinuousAggregateOperation Tests - Runtime

        [Fact]
        public void Runtime_Create_MinimalAggregate_GeneratesCorrectSQL()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_metrics",
                Schema = "public",
                ParentName = "metrics",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["avg_value:Avg:value"],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Runtime uses single quotes for SQL
            Assert.Contains("CREATE MATERIALIZED VIEW \"public\".\"hourly_metrics\"", result);
            Assert.Contains("WITH (timescaledb.continuous", result);
            Assert.Contains("time_bucket('1 hour', \"timestamp\")", result);
            Assert.Contains("AVG(\"value\") AS \"avg_value\"", result);
            Assert.Contains("FROM \"public\".\"metrics\"", result);
            Assert.Contains("GROUP BY time_bucket", result);
            Assert.DoesNotContain("WITH NO DATA", result);
        }

        [Fact]
        public void Runtime_Create_WithFirstLast_UsesCorrectParameterOrder()
        {
            // Arrange - Verify TimescaleDB first()/last() parameter ordering
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "price_extremes",
                Schema = "public",
                ParentName = "trades",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "opening_price:First:price",
                    "closing_price:Last:price"
                ],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - first() and last() must have (value, time) ordering
            Assert.Contains("first(\"price\", \"time\") AS \"opening_price\"", result);
            Assert.Contains("last(\"price\", \"time\") AS \"closing_price\"", result);
        }

        [Fact]
        public void Runtime_Create_WithAllOptions_GeneratesCompleteSQL()
        {
            // Arrange - Test comprehensive continuous aggregate
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "comprehensive_stats",
                Schema = "analytics",
                ParentName = "sensor_readings",
                TimeBucketWidth = "30 minutes",
                TimeBucketSourceColumn = "recorded_at",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "avg_temp:Avg:temperature",
                    "max_humidity:Max:humidity",
                    "min_pressure:Min:pressure",
                    "total_samples:Count:id",
                    "first_reading:First:temperature",
                    "last_reading:Last:temperature"
                ],
                GroupByColumns = ["sensor_id", "location"],
                WhereClause = "\"temperature\" IS NOT NULL",
                ChunkInterval = "1 day",
                CreateGroupIndexes = true,
                MaterializedOnly = true,
                WithNoData = true
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert all SQL components
            Assert.Contains("CREATE MATERIALIZED VIEW \"analytics\".\"comprehensive_stats\"", result);
            Assert.Contains("timescaledb.continuous", result);
            Assert.Contains("timescaledb.create_group_indexes = true", result);
            Assert.Contains("timescaledb.materialized_only = true", result);
            Assert.Contains("timescaledb.chunk_interval = '1 day'", result);
            Assert.Contains("time_bucket('30 minutes', \"recorded_at\") AS time_bucket", result);
            Assert.Contains("\"sensor_id\"", result);
            Assert.Contains("\"location\"", result);
            Assert.Contains("AVG(\"temperature\") AS \"avg_temp\"", result);
            Assert.Contains("MAX(\"humidity\") AS \"max_humidity\"", result);
            Assert.Contains("MIN(\"pressure\") AS \"min_pressure\"", result);
            Assert.Contains("COUNT(\"id\") AS \"total_samples\"", result);
            Assert.Contains("first(\"temperature\", \"recorded_at\") AS \"first_reading\"", result);
            Assert.Contains("last(\"temperature\", \"recorded_at\") AS \"last_reading\"", result);
            Assert.Contains("WHERE \"temperature\" IS NOT NULL", result);
            Assert.Contains("GROUP BY time_bucket, \"sensor_id\", \"location\"", result);
            Assert.Contains("WITH NO DATA", result);
        }

        #endregion

        #region AlterContinuousAggregateOperation Tests

        [Fact]
        public void DesignTime_Alter_ChunkInterval_GeneratesCorrectCode()
        {
            // Arrange
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ChunkInterval = "30 days",
                OldChunkInterval = "7 days"
            };

            string expected = @".Sql(@""
                ALTER MATERIALIZED VIEW """"public"""".""""hourly_stats"""" SET (timescaledb.chunk_interval = '30 days');
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Runtime_Alter_ChunkInterval_GeneratesCorrectSQL()
        {
            // Arrange
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "daily_aggregates",
                Schema = "analytics",
                ChunkInterval = "90 days",
                OldChunkInterval = "30 days"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("ALTER MATERIALIZED VIEW \"analytics\".\"daily_aggregates\"", result);
            Assert.Contains("SET (timescaledb.chunk_interval = '90 days')", result);
        }

        [Fact]
        public void DesignTime_Alter_CreateGroupIndexes_GeneratesCorrectCode()
        {
            // Arrange
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "metrics_view",
                Schema = "public",
                CreateGroupIndexes = true,
                OldCreateGroupIndexes = false
            };

            string expected = @".Sql(@""
                ALTER MATERIALIZED VIEW """"public"""".""""metrics_view"""" SET (timescaledb.create_group_indexes = true);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_MaterializedOnly_GeneratesCorrectCode()
        {
            // Arrange
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "stats_view",
                Schema = "public",
                MaterializedOnly = false,
                OldMaterializedOnly = true
            };

            string expected = @".Sql(@""
                ALTER MATERIALIZED VIEW """"public"""".""""stats_view"""" SET (timescaledb.materialized_only = false);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_MultipleProperties_GeneratesMultipleStatements()
        {
            // Arrange - Test altering multiple properties at once
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "complex_view",
                Schema = "analytics",
                ChunkInterval = "60 days",
                OldChunkInterval = "30 days",
                CreateGroupIndexes = true,
                OldCreateGroupIndexes = false,
                MaterializedOnly = false,
                OldMaterializedOnly = true
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should generate three separate ALTER statements
            Assert.Contains("timescaledb.chunk_interval = '60 days'", result);
            Assert.Contains("timescaledb.create_group_indexes = true", result);
            Assert.Contains("timescaledb.materialized_only = false", result);
        }

        [Fact]
        public void Alter_NoChanges_GeneratesNoSQL()
        {
            // Arrange - Nothing changed
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "unchanged_view",
                Schema = "public",
                ChunkInterval = "7 days",
                OldChunkInterval = "7 days",
                CreateGroupIndexes = false,
                OldCreateGroupIndexes = false,
                MaterializedOnly = false,
                OldMaterializedOnly = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Should generate empty result
            Assert.Empty(result.Trim());
        }

        #endregion

        #region DropContinuousAggregateOperation Tests

        [Fact]
        public void DesignTime_Drop_GeneratesCorrectCode()
        {
            // Arrange
            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "old_aggregate",
                Schema = "public"
            };

            string expected = @".Sql(@""
                DROP MATERIALIZED VIEW IF EXISTS """"public"""".""""old_aggregate"""";
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Runtime_Drop_GeneratesCorrectSQL()
        {
            // Arrange
            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "deprecated_view",
                Schema = "analytics"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("DROP MATERIALIZED VIEW IF EXISTS \"analytics\".\"deprecated_view\"", result);
            Assert.EndsWith(";", result.Trim());
        }

        [Fact]
        public void Runtime_Drop_UsesIfExists_ForSafety()
        {
            // Arrange - Verify IF EXISTS is always used for safety
            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "maybe_exists",
                Schema = "public"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - IF EXISTS prevents errors if view doesn't exist
            Assert.Contains("IF EXISTS", result);
        }

        #endregion

        #region TimescaleDB Constraint Validation Tests

        [Fact]
        public void Create_RequiresTimeBucket_InSelectClause()
        {
            // Arrange - TimescaleDB requires time_bucket in continuous aggregates
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "test_view",
                Schema = "public",
                ParentName = "test_table",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["count_all:Count:id"],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - time_bucket is required by TimescaleDB
            Assert.Contains("time_bucket(", result);
            Assert.Contains("AS time_bucket", result);
        }

        [Fact]
        public void Create_RequiresTimeBucket_InGroupByClause()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "test_view",
                Schema = "public",
                ParentName = "test_table",
                TimeBucketWidth = "1 day",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["avg_val:Avg:value"],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - GROUP BY must include time_bucket
            Assert.Contains("GROUP BY time_bucket", result);
        }

        [Fact]
        public void Create_FirstAndLast_RequireTimeParameter()
        {
            // Arrange - TimescaleDB first() and last() must have time column
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "first_last_test",
                Schema = "public",
                ParentName = "data",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "ts",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "first_val:First:value",
                    "last_val:Last:value"
                ],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - first() and last() MUST have (value, time) signature
            Assert.Contains("first(\"value\", \"ts\")", result);
            Assert.Contains("last(\"value\", \"ts\")", result);
            Assert.DoesNotContain("first(\"value\")", result);
            Assert.DoesNotContain("last(\"value\")", result);
        }

        [Fact]
        public void Create_StandardAggregates_DoNotRequireTimeParameter()
        {
            // Arrange - Standard SQL aggregates (AVG, MAX, MIN, SUM, COUNT) don't need time
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "standard_agg_test",
                Schema = "public",
                ParentName = "metrics",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions =
                [
                    "avg_temp:Avg:temperature",
                    "max_temp:Max:temperature",
                    "count_all:Count:id"
                ],
                GroupByColumns = [],
                CreateGroupIndexes = false,
                MaterializedOnly = false,
                WithNoData = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Standard aggregates use single parameter
            Assert.Contains("AVG(\"temperature\")", result);
            Assert.Contains("MAX(\"temperature\")", result);
            Assert.Contains("COUNT(\"id\")", result);
            Assert.DoesNotContain("AVG(\"temperature\", \"time\")", result);
        }

        #endregion
    }
}
