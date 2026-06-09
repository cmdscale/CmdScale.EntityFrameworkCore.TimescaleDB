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

            // Assert — fully qualified type and enum reference.
            Assert.Contains(
                "new CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.ContinuousAggregateFunction(\"avg_t\", CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.EAggregateFunction.Avg, \"temp\")",
                result);
        }

        #endregion

        #region CreateContinuousAggregate_MalformedAggregateString_IsSkipped

        [Fact]
        public void CreateContinuousAggregate_MalformedAggregateString_IsSkipped()
        {
            // Arrange — second entry has only two parts and must be silently skipped.
            CreateContinuousAggregateOperation op = new()
            {
                MaterializedViewName = "hourly",
                ParentName = "sensor_data",
                AggregateFunctions = ["avg_t:Avg:temp", "malformed:Sum"],
            };

            // Act
            string result = Generate(op);

            // Assert — only the well-formed entry is emitted.
            Assert.Contains("EAggregateFunction.Avg, \"temp\")", result);
            Assert.DoesNotContain("malformed", result);
        }

        #endregion

        #region CreateContinuousAggregate_TimeBucketGroupBy_OnlyEmittedWhenDisabled

        [Fact]
        public void CreateContinuousAggregate_TimeBucketGroupBy_OnlyEmittedWhenDisabled()
        {
            // Arrange — default true must NOT be emitted.
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

            // Arrange — non-default false must be emitted as false.
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
    }
}
