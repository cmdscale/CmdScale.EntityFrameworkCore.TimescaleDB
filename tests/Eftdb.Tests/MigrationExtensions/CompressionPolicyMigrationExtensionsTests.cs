using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions;

public class CompressionPolicyMigrationExtensionsTests
{
    #region AddCompressionPolicy_WithAfter_MapsArguments

    [Fact]
    public void AddCompressionPolicy_WithAfter_MapsArguments()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);
        DateTime start = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        mb.AddCompressionPolicy(
            tableName: "sensor_data",
            schema: "public",
            after: "7 days",
            scheduleInterval: "12 hours",
            initialStart: start,
            timezone: "Europe/Berlin",
            ifNotExists: true);

        // Assert
        AddCompressionPolicyOperation op = Assert.IsType<AddCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal("sensor_data", op.TableName);
        Assert.Equal("public", op.Schema);
        Assert.Equal("7 days", op.After);
        Assert.Null(op.CreatedBefore);
        Assert.Equal("12 hours", op.ScheduleInterval);
        Assert.Equal(start, op.InitialStart);
        Assert.Equal("Europe/Berlin", op.Timezone);
        Assert.True(op.IfNotExists);
    }

    #endregion

    #region AddCompressionPolicy_WithCreatedBefore_MapsArguments

    [Fact]
    public void AddCompressionPolicy_WithCreatedBefore_MapsArguments()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.AddCompressionPolicy(tableName: "sensor_data", createdBefore: "30 days");

        // Assert
        AddCompressionPolicyOperation op = Assert.IsType<AddCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal("30 days", op.CreatedBefore);
        Assert.Null(op.After);
        Assert.Equal(string.Empty, op.Schema);
    }

    #endregion

    #region AddCompressionPolicy_NullSchema_CoalescesToEmpty

    [Fact]
    public void AddCompressionPolicy_NullSchema_CoalescesToEmpty()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.AddCompressionPolicy(tableName: "sensor_data", schema: null, after: "7 days");

        // Assert
        AddCompressionPolicyOperation op = Assert.IsType<AddCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal(string.Empty, op.Schema);
    }

    #endregion

    #region AlterCompressionPolicy_MapsCurrentAndOldArguments

    [Fact]
    public void AlterCompressionPolicy_MapsCurrentAndOldArguments()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);
        DateTime newStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime oldStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        mb.AlterCompressionPolicy(
            tableName: "sensor_data",
            schema: "public",
            after: "14 days",
            scheduleInterval: "6 hours",
            initialStart: newStart,
            timezone: "UTC",
            ifNotExists: true,
            oldAfter: "7 days",
            oldScheduleInterval: "12 hours",
            oldInitialStart: oldStart,
            oldTimezone: "Europe/Berlin",
            oldIfNotExists: false);

        // Assert
        AlterCompressionPolicyOperation op = Assert.IsType<AlterCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal("14 days", op.After);
        Assert.Null(op.CreatedBefore);
        Assert.Equal("6 hours", op.ScheduleInterval);
        Assert.Equal(newStart, op.InitialStart);
        Assert.Equal("UTC", op.Timezone);
        Assert.True(op.IfNotExists);
        Assert.Equal("7 days", op.OldAfter);
        Assert.Null(op.OldCreatedBefore);
        Assert.Equal("12 hours", op.OldScheduleInterval);
        Assert.Equal(oldStart, op.OldInitialStart);
        Assert.Equal("Europe/Berlin", op.OldTimezone);
        Assert.False(op.OldIfNotExists);
    }

    #endregion

    #region AlterCompressionPolicy_Switches_From_After_To_CreatedBefore

    [Fact]
    public void AlterCompressionPolicy_Switches_From_After_To_CreatedBefore()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.AlterCompressionPolicy(
            tableName: "sensor_data",
            createdBefore: "30 days",
            oldAfter: "7 days");

        // Assert
        AlterCompressionPolicyOperation op = Assert.IsType<AlterCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Null(op.After);
        Assert.Equal("30 days", op.CreatedBefore);
        Assert.Equal("7 days", op.OldAfter);
        Assert.Null(op.OldCreatedBefore);
    }

    #endregion

    #region AlterCompressionPolicy_NullSchema_CoalescesToEmpty

    [Fact]
    public void AlterCompressionPolicy_NullSchema_CoalescesToEmpty()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.AlterCompressionPolicy(tableName: "sensor_data", schema: null, after: "14 days");

        // Assert
        AlterCompressionPolicyOperation op = Assert.IsType<AlterCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal(string.Empty, op.Schema);
    }

    #endregion

    #region DropCompressionPolicy_MapsTableAndSchema

    [Fact]
    public void DropCompressionPolicy_MapsTableAndSchema()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.DropCompressionPolicy(tableName: "sensor_data", schema: "public");

        // Assert
        DropCompressionPolicyOperation op = Assert.IsType<DropCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal("sensor_data", op.TableName);
        Assert.Equal("public", op.Schema);
    }

    #endregion

    #region DropCompressionPolicy_NullSchema_CoalescesToEmpty

    [Fact]
    public void DropCompressionPolicy_NullSchema_CoalescesToEmpty()
    {
        // Arrange
        MigrationBuilder mb = new(activeProvider: null);

        // Act
        mb.DropCompressionPolicy(tableName: "sensor_data", schema: null);

        // Assert
        DropCompressionPolicyOperation op = Assert.IsType<DropCompressionPolicyOperation>(Assert.Single(mb.Operations));
        Assert.Equal(string.Empty, op.Schema);
    }

    #endregion
}
