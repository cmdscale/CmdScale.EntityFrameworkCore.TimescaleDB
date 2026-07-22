using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions
{
    /// <summary>
    /// Unit tests for the typed retention policy migration builder extensions.
    /// </summary>
    public class RetentionPolicyMigrationExtensionsTests
    {
        #region AddRetentionPolicy_WithDropAfter_MapsArguments

        [Fact]
        public void AddRetentionPolicy_WithDropAfter_MapsArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            DateTime start = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act
            mb.AddRetentionPolicy(
                tableName: "sensor_data",
                schema: "public",
                dropAfter: "30 days",
                initialStart: start,
                scheduleInterval: "1 day",
                maxRuntime: "1 hour",
                maxRetries: 2,
                retryPeriod: "5 minutes");

            // Assert
            AddRetentionPolicyOperation op = Assert.IsType<AddRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("public", op.Schema);
            Assert.Equal("30 days", op.DropAfter);
            Assert.Null(op.DropCreatedBefore);
            Assert.Equal(start, op.InitialStart);
            Assert.Equal("1 day", op.ScheduleInterval);
            Assert.Equal("1 hour", op.MaxRuntime);
            Assert.Equal(2, op.MaxRetries);
            Assert.Equal("5 minutes", op.RetryPeriod);
        }

        #endregion

        #region AddRetentionPolicy_WithDropCreatedBefore_MapsArguments

        [Fact]
        public void AddRetentionPolicy_WithDropCreatedBefore_MapsArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AddRetentionPolicy(tableName: "sensor_data", dropCreatedBefore: "60 days");

            // Assert
            AddRetentionPolicyOperation op = Assert.IsType<AddRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("60 days", op.DropCreatedBefore);
            Assert.Null(op.DropAfter);
            Assert.Equal(string.Empty, op.Schema);
        }

        #endregion

        #region AlterRetentionPolicy_MapsCurrentAndOldArguments

        [Fact]
        public void AlterRetentionPolicy_MapsCurrentAndOldArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterRetentionPolicy(
                tableName: "sensor_data",
                schema: "public",
                dropAfter: "60 days",
                dropCreatedBefore: "90 days",
                scheduleInterval: "1 day",
                maxRetries: 4,
                oldDropAfter: "30 days",
                oldDropCreatedBefore: "45 days",
                oldScheduleInterval: "4 days",
                oldMaxRetries: 1);

            // Assert
            AlterRetentionPolicyOperation op = Assert.IsType<AlterRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("60 days", op.DropAfter);
            Assert.Equal("90 days", op.DropCreatedBefore);
            Assert.Equal("1 day", op.ScheduleInterval);
            Assert.Equal(4, op.MaxRetries);
            Assert.Equal("30 days", op.OldDropAfter);
            Assert.Equal("45 days", op.OldDropCreatedBefore);
            Assert.Equal("4 days", op.OldScheduleInterval);
            Assert.Equal(1, op.OldMaxRetries);
        }

        #endregion

        #region DropRetentionPolicy_MapsTableAndSchema

        [Fact]
        public void DropRetentionPolicy_MapsTableAndSchema()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.DropRetentionPolicy(tableName: "sensor_data", schema: "public");

            // Assert
            DropRetentionPolicyOperation op = Assert.IsType<DropRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("public", op.Schema);
        }

        #endregion

        #region DropRetentionPolicy_NullSchema_CoalescesToEmpty

        [Fact]
        public void DropRetentionPolicy_NullSchema_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.DropRetentionPolicy(tableName: "sensor_data", schema: null);

            // Assert
            DropRetentionPolicyOperation op = Assert.IsType<DropRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
        }

        #endregion

        #region AlterRetentionPolicy_NullSchema_CoalescesToEmpty

        [Fact]
        public void AlterRetentionPolicy_NullSchema_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterRetentionPolicy(tableName: "sensor_data", schema: null, dropAfter: "30 days");

            // Assert
            AlterRetentionPolicyOperation op = Assert.IsType<AlterRetentionPolicyOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
            Assert.Equal("30 days", op.DropAfter);
        }

        #endregion
    }
}
