using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions
{
    /// <summary>
    /// Unit tests for the typed continuous aggregate policy migration builder extensions.
    /// </summary>
    public class ContinuousAggregatePolicyMigrationExtensionsTests
    {
        #region AddContinuousAggregatePolicy_DefaultValues_MapExactly

        [Fact]
        public void AddContinuousAggregatePolicy_DefaultValues_MapExactly()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AddContinuousAggregatePolicy(materializedViewName: "hourly");

            // Assert
            AddContinuousAggregatePolicyOperation op = Assert.IsType<AddContinuousAggregatePolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("hourly", op.MaterializedViewName);
            Assert.Equal(string.Empty, op.Schema);
            Assert.Null(op.StartOffset);
            Assert.Null(op.EndOffset);
            Assert.Null(op.ScheduleInterval);
            Assert.Null(op.InitialStart);
            Assert.False(op.IfNotExists);
            Assert.Null(op.IncludeTieredData);
            Assert.Equal(1, op.BucketsPerBatch);
            Assert.Equal(0, op.MaxBatchesPerExecution);
            Assert.True(op.RefreshNewestFirst);
        }

        #endregion

        #region AddContinuousAggregatePolicy_MapsAllArguments

        [Fact]
        public void AddContinuousAggregatePolicy_MapsAllArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            DateTime start = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

            // Act
            mb.AddContinuousAggregatePolicy(
                materializedViewName: "hourly",
                schema: "public",
                startOffset: "1 month",
                endOffset: "1 hour",
                scheduleInterval: "1 hour",
                initialStart: start,
                ifNotExists: true,
                includeTieredData: true,
                bucketsPerBatch: 5,
                maxBatchesPerExecution: 10,
                refreshNewestFirst: false);

            // Assert
            AddContinuousAggregatePolicyOperation op = Assert.IsType<AddContinuousAggregatePolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("public", op.Schema);
            Assert.Equal("1 month", op.StartOffset);
            Assert.Equal("1 hour", op.EndOffset);
            Assert.Equal("1 hour", op.ScheduleInterval);
            Assert.Equal(start, op.InitialStart);
            Assert.True(op.IfNotExists);
            Assert.Equal(true, op.IncludeTieredData);
            Assert.Equal(5, op.BucketsPerBatch);
            Assert.Equal(10, op.MaxBatchesPerExecution);
            Assert.False(op.RefreshNewestFirst);
        }

        #endregion

        #region RemoveContinuousAggregatePolicy_MapsViewSchemaAndIfExists

        [Fact]
        public void RemoveContinuousAggregatePolicy_MapsViewSchemaAndIfExists()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.RemoveContinuousAggregatePolicy(materializedViewName: "hourly", schema: "public", ifExists: true);

            // Assert
            RemoveContinuousAggregatePolicyOperation op = Assert.IsType<RemoveContinuousAggregatePolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("hourly", op.MaterializedViewName);
            Assert.Equal("public", op.Schema);
            Assert.True(op.IfExists);
        }

        #endregion

        #region RemoveContinuousAggregatePolicy_Defaults

        [Fact]
        public void RemoveContinuousAggregatePolicy_Defaults()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.RemoveContinuousAggregatePolicy(materializedViewName: "hourly");

            // Assert
            RemoveContinuousAggregatePolicyOperation op = Assert.IsType<RemoveContinuousAggregatePolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
            Assert.False(op.IfExists);
        }

        #endregion
    }
}
