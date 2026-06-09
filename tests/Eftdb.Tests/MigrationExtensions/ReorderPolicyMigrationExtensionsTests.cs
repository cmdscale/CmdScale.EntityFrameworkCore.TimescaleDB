using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions
{
    /// <summary>
    /// Unit tests for the typed reorder policy migration builder extensions.
    /// </summary>
    public class ReorderPolicyMigrationExtensionsTests
    {
        #region AddReorderPolicy_MapsJobTuningArguments

        [Fact]
        public void AddReorderPolicy_MapsJobTuningArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            DateTime start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act
            mb.AddReorderPolicy(
                tableName: "sensor_data",
                indexName: "ix_ts",
                schema: "public",
                initialStart: start,
                scheduleInterval: "1 day",
                maxRuntime: "1 hour",
                maxRetries: 3,
                retryPeriod: "10 minutes");

            // Assert
            AddReorderPolicyOperation op = Assert.IsType<AddReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("ix_ts", op.IndexName);
            Assert.Equal("public", op.Schema);
            Assert.Equal(start, op.InitialStart);
            Assert.Equal("1 day", op.ScheduleInterval);
            Assert.Equal("1 hour", op.MaxRuntime);
            Assert.Equal(3, op.MaxRetries);
            Assert.Equal("10 minutes", op.RetryPeriod);
        }

        #endregion

        #region AddReorderPolicy_NullSchema_CoalescesToEmpty

        [Fact]
        public void AddReorderPolicy_NullSchema_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AddReorderPolicy(tableName: "t", indexName: "ix", schema: null);

            // Assert
            AddReorderPolicyOperation op = Assert.IsType<AddReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
            Assert.Null(op.InitialStart);
            Assert.Null(op.ScheduleInterval);
            Assert.Null(op.MaxRetries);
        }

        #endregion

        #region AlterReorderPolicy_NullOldIndexName_CoalescesToEmpty

        [Fact]
        public void AlterReorderPolicy_NullOldIndexName_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterReorderPolicy(tableName: "t", indexName: "new_ix", oldIndexName: null);

            // Assert
            AlterReorderPolicyOperation op = Assert.IsType<AlterReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("new_ix", op.IndexName);
            Assert.Equal(string.Empty, op.OldIndexName);
        }

        #endregion

        #region AlterReorderPolicy_MapsOldArguments

        [Fact]
        public void AlterReorderPolicy_MapsOldArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            DateTime oldStart = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act
            mb.AlterReorderPolicy(
                tableName: "t",
                indexName: "new_ix",
                oldIndexName: "old_ix",
                oldInitialStart: oldStart,
                oldScheduleInterval: "4 days",
                oldMaxRuntime: "2 hours",
                oldMaxRetries: 7,
                oldRetryPeriod: "5 minutes");

            // Assert
            AlterReorderPolicyOperation op = Assert.IsType<AlterReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("old_ix", op.OldIndexName);
            Assert.Equal(oldStart, op.OldInitialStart);
            Assert.Equal("4 days", op.OldScheduleInterval);
            Assert.Equal("2 hours", op.OldMaxRuntime);
            Assert.Equal(7, op.OldMaxRetries);
            Assert.Equal("5 minutes", op.OldRetryPeriod);
        }

        #endregion

        #region DropReorderPolicy_MapsTableAndSchema

        [Fact]
        public void DropReorderPolicy_MapsTableAndSchema()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.DropReorderPolicy(tableName: "sensor_data", schema: "public");

            // Assert
            DropReorderPolicyOperation op = Assert.IsType<DropReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("public", op.Schema);
        }

        #endregion

        #region DropReorderPolicy_NullSchema_CoalescesToEmpty

        [Fact]
        public void DropReorderPolicy_NullSchema_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.DropReorderPolicy(tableName: "sensor_data", schema: null);

            // Assert
            DropReorderPolicyOperation op = Assert.IsType<DropReorderPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
        }

        #endregion
    }
}
