using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions
{
    /// <summary>
    /// Unit tests for the typed continuous aggregate migration builder extensions.
    /// </summary>
    public class ContinuousAggregateMigrationExtensionsTests
    {
        #region CreateContinuousAggregate_MapsFunctionsToAnnotationStrings

        [Fact]
        public void CreateContinuousAggregate_MapsFunctionsToAnnotationStrings()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            List<ContinuousAggregateFunction> functions =
            [
                new("avg_t", EAggregateFunction.Avg, "temp"),
                new("max_t", EAggregateFunction.Max, "temp"),
            ];

            // Act
            mb.CreateContinuousAggregate(
                materializedViewName: "hourly",
                parentName: "sensor_data",
                schema: "public",
                chunkInterval: "7 days",
                withNoData: true,
                createGroupIndexes: true,
                materializedOnly: true,
                timeBucketWidth: "1 hour",
                timeBucketSourceColumn: "ts",
                timeBucketGroupBy: false,
                aggregateFunctions: functions,
                groupByColumns: ["device_id"],
                whereClause: "temp > 0",
                viewDefinition: "SELECT 1");

            // Assert
            CreateContinuousAggregateOperation op = Assert.IsType<CreateContinuousAggregateOperation>(Assert.Single(mb.Operations));
            Assert.Equal("hourly", op.MaterializedViewName);
            Assert.Equal("sensor_data", op.ParentName);
            Assert.Equal("public", op.Schema);
            Assert.Equal("7 days", op.ChunkInterval);
            Assert.True(op.WithNoData);
            Assert.True(op.CreateGroupIndexes);
            Assert.True(op.MaterializedOnly);
            Assert.Equal("1 hour", op.TimeBucketWidth);
            Assert.Equal("ts", op.TimeBucketSourceColumn);
            Assert.False(op.TimeBucketGroupBy);
            Assert.Equal(["avg_t:Avg:temp", "max_t:Max:temp"], op.AggregateFunctions);
            Assert.Equal(["device_id"], op.GroupByColumns);
            Assert.Equal("temp > 0", op.WhereClause);
            Assert.Equal("SELECT 1", op.ViewDefinition);
        }

        #endregion

        #region CreateContinuousAggregate_NullCollections_CoalesceToEmptyLists

        [Fact]
        public void CreateContinuousAggregate_NullCollections_CoalesceToEmptyLists()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.CreateContinuousAggregate(
                materializedViewName: "hourly",
                parentName: "sensor_data",
                aggregateFunctions: null,
                groupByColumns: null);

            // Assert
            CreateContinuousAggregateOperation op = Assert.IsType<CreateContinuousAggregateOperation>(Assert.Single(mb.Operations));
            Assert.Empty(op.AggregateFunctions);
            Assert.Empty(op.GroupByColumns);
            Assert.Equal(string.Empty, op.Schema);
            Assert.Equal(string.Empty, op.TimeBucketWidth);
            Assert.Equal(string.Empty, op.TimeBucketSourceColumn);
            // timeBucketGroupBy defaults to true.
            Assert.True(op.TimeBucketGroupBy);
        }

        #endregion

        #region AlterContinuousAggregate_MapsCurrentAndOldArguments

        [Fact]
        public void AlterContinuousAggregate_MapsCurrentAndOldArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterContinuousAggregate(
                materializedViewName: "hourly",
                schema: "public",
                chunkInterval: "7 days",
                createGroupIndexes: true,
                materializedOnly: true,
                oldChunkInterval: "1 day",
                oldCreateGroupIndexes: true,
                oldMaterializedOnly: false);

            // Assert
            AlterContinuousAggregateOperation op = Assert.IsType<AlterContinuousAggregateOperation>(Assert.Single(mb.Operations));
            Assert.Equal("hourly", op.MaterializedViewName);
            Assert.Equal("7 days", op.ChunkInterval);
            Assert.True(op.CreateGroupIndexes);
            Assert.True(op.MaterializedOnly);
            Assert.Equal("1 day", op.OldChunkInterval);
            Assert.True(op.OldCreateGroupIndexes);
            Assert.False(op.OldMaterializedOnly);
        }

        #endregion

        #region DropContinuousAggregate_MapsArguments

        [Fact]
        public void DropContinuousAggregate_MapsArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.DropContinuousAggregate(materializedViewName: "hourly", schema: "public");

            // Assert
            DropContinuousAggregateOperation op = Assert.IsType<DropContinuousAggregateOperation>(Assert.Single(mb.Operations));
            Assert.Equal("hourly", op.MaterializedViewName);
            Assert.Equal("public", op.Schema);
        }

        #endregion
    }
}
