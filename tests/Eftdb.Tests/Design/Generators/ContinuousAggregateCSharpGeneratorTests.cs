using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="ContinuousAggregateCSharpGenerator"/>
    /// using a real <see cref="ICSharpHelper"/>.
    /// </summary>
    public class ContinuousAggregateCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(CreateContinuousAggregateOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ContinuousAggregateCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(AlterContinuousAggregateOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ContinuousAggregateCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region CreateContinuousAggregate_AggregateFunction_FullyQualifiedTypedEntry

        [Fact]
        public void CreateContinuousAggregate_AggregateFunction_FullyQualifiedTypedEntry()
        {
            // Arrange
            CreateContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                AggregateFunctions = ["avg_t:Avg:temp"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains(
                "new CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.ContinuousAggregateFunction(\"avg_t\", CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.EAggregateFunction.Avg, \"temp\")",
                result);
        }

        #endregion

        #region CreateContinuousAggregate_MalformedAggregateString_IsSkipped

        [Fact]
        public void CreateContinuousAggregate_MalformedAggregateString_IsSkipped()
        {
            // Arrange
            CreateContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                AggregateFunctions = ["avg_t:Avg:temp", "malformed:Sum"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("EAggregateFunction.Avg, \"temp\")", result);
            Assert.DoesNotContain("malformed", result);
        }

        #endregion

        #region CreateContinuousAggregate_TimeBucketGroupBy_OnlyEmittedWhenDisabled

        [Fact]
        public void CreateContinuousAggregate_TimeBucketGroupBy_OnlyEmittedWhenDisabled()
        {
            // Arrange
            CreateContinuousAggregateOperation enabled = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                TimeBucketGroupBy = true,
            };

            // Act
            string enabledResult = Generate(enabled);

            // Assert
            Assert.DoesNotContain("timeBucketGroupBy:", enabledResult);

            // Arrange
            CreateContinuousAggregateOperation disabled = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                TimeBucketGroupBy = false,
            };

            // Act
            string disabledResult = Generate(disabled);

            // Assert
            Assert.Contains("timeBucketGroupBy: false", disabledResult);
        }

        #endregion

        #region CreateContinuousAggregate_GroupByColumns_EmitCollectionExpression

        [Fact]
        public void CreateContinuousAggregate_GroupByColumns_EmitCollectionExpression()
        {
            // Arrange
            CreateContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                GroupByColumns = ["device_id", "region"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("groupByColumns: [\"device_id\", \"region\"]", result);
        }

        #endregion

        #region CreateContinuousAggregate_FullyPopulated_EmitsAllOptionalArgs

        [Fact]
        public void CreateContinuousAggregate_FullyPopulated_EmitsAllOptionalArgs()
        {
            // Arrange
            CreateContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                Schema = "metrics",
                ChunkInterval = "7 days",
                WithNoData = true,
                CreateGroupIndexes = true,
                MaterializedOnly = true,
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "ts",
                TimeBucketGroupBy = false,
                AggregateFunctions = ["avg_t:Avg:temp"],
                GroupByColumns = ["device_id"],
                WhereClause = "temp > 0",
                ViewDefinition = "SELECT 1",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("chunkInterval: \"7 days\"", result);
            Assert.Contains("withNoData: true", result);
            Assert.Contains("createGroupIndexes: true", result);
            Assert.Contains("materializedOnly: true", result);
            Assert.Contains("timeBucketWidth: \"1 hour\"", result);
            Assert.Contains("timeBucketSourceColumn: \"ts\"", result);
            Assert.Contains("timeBucketGroupBy: false", result);
            Assert.Contains("aggregateFunctions:", result);
            Assert.Contains("groupByColumns: [\"device_id\"]", result);
            Assert.Contains("whereClause: \"temp > 0\"", result);
            Assert.Contains("viewDefinition: \"SELECT 1\"", result);
        }

        #endregion

        #region AlterContinuousAggregate_FullyPopulated_EmitsAllNewAndOldArgs

        [Fact]
        public void AlterContinuousAggregate_FullyPopulated_EmitsAllNewAndOldArgs()
        {
            // Arrange
            AlterContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                Schema = "metrics",
                ChunkInterval = "7 days",
                CreateGroupIndexes = true,
                MaterializedOnly = true,
                OldChunkInterval = "1 day",
                OldCreateGroupIndexes = true,
                OldMaterializedOnly = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("chunkInterval: \"7 days\"", result);
            Assert.Contains("createGroupIndexes: true", result);
            Assert.Contains("materializedOnly: true", result);

            // Assert
            Assert.Contains("oldChunkInterval: \"1 day\"", result);
            Assert.Contains("oldCreateGroupIndexes: true", result);
            Assert.Contains("oldMaterializedOnly: true", result);
        }

        #endregion

        #region AlterContinuousAggregate_EmitsOldArgsOnlyWhenNonDefault

        [Fact]
        public void AlterContinuousAggregate_EmitsOldArgsOnlyWhenNonDefault()
        {
            // Arrange
            AlterContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ChunkInterval = "7 days",
                OldChunkInterval = "1 day",
                OldCreateGroupIndexes = true,
                OldMaterializedOnly = false,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("chunkInterval: \"7 days\"", result);
            Assert.Contains("oldChunkInterval: \"1 day\"", result);
            Assert.Contains("oldCreateGroupIndexes: true", result);
            Assert.DoesNotContain("oldMaterializedOnly:", result);
        }

        #endregion

        #region CreateContinuousAggregate_NullAggregateFunctions_Omits_AggregateFunctionsArg

        [Fact]
        public void CreateContinuousAggregate_NullAggregateFunctions_Omits_AggregateFunctionsArg()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "null_agg_ca",
                ParentName = "src_null_agg",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "ts",
                AggregateFunctions = null!,
                GroupByColumns = []
            };

            // Act
            string result = Generate(operation);

            // Assert
            Assert.DoesNotContain("aggregateFunctions", result);
        }

        #endregion

        #region CreateContinuousAggregate_NullGroupByColumns_Omits_GroupByColumnsArg

        [Fact]
        public void CreateContinuousAggregate_NullGroupByColumns_Omits_GroupByColumnsArg()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "null_gbc_ca",
                ParentName = "src_null_gbc",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "ts",
                AggregateFunctions = [],
                GroupByColumns = null!
            };

            // Act
            string result = Generate(operation);

            // Assert
            Assert.DoesNotContain("groupByColumns", result);
        }

        #endregion
    }
}
