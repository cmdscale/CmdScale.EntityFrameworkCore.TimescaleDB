using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

public class CompressionPolicyAttributeTests
{
    #region After_CanBeSet

    [Fact]
    public void After_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days" };

        // Assert
        Assert.Equal("7 days", attr.After);
        Assert.Null(attr.CreatedBefore);
    }

    #endregion

    #region CreatedBefore_CanBeSet

    [Fact]
    public void CreatedBefore_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { CreatedBefore = "30 days" };

        // Assert
        Assert.Null(attr.After);
        Assert.Equal("30 days", attr.CreatedBefore);
    }

    #endregion

    #region ScheduleInterval_CanBeSet

    [Fact]
    public void ScheduleInterval_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days", ScheduleInterval = "12 hours" };

        // Assert
        Assert.Equal("12 hours", attr.ScheduleInterval);
    }

    #endregion

    #region InitialStart_CanBeSet

    [Fact]
    public void InitialStart_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days", InitialStart = "2025-10-01T03:00:00Z" };

        // Assert
        Assert.Equal("2025-10-01T03:00:00Z", attr.InitialStart);
    }

    #endregion

    #region Timezone_CanBeSet

    [Fact]
    public void Timezone_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days", Timezone = "Europe/Berlin" };

        // Assert
        Assert.Equal("Europe/Berlin", attr.Timezone);
    }

    #endregion

    #region IfNotExists_DefaultIsFalse

    [Fact]
    public void IfNotExists_DefaultIsFalse()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days" };

        // Assert
        Assert.False(attr.IfNotExists);
    }

    #endregion

    #region IfNotExists_CanBeSetToTrue

    [Fact]
    public void IfNotExists_CanBeSetToTrue()
    {
        // Arrange
        CompressionPolicyAttribute attr = new() { After = "7 days", IfNotExists = true };

        // Assert
        Assert.True(attr.IfNotExists);
    }

    #endregion

    #region AllProperties_CanBeSetTogether

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        CompressionPolicyAttribute attr = new()
        {
            After = "7 days",
            ScheduleInterval = "12 hours",
            InitialStart = "2025-01-01T00:00:00Z",
            Timezone = "UTC",
            IfNotExists = true
        };

        // Assert
        Assert.Equal("7 days", attr.After);
        Assert.Null(attr.CreatedBefore);
        Assert.Equal("12 hours", attr.ScheduleInterval);
        Assert.Equal("2025-01-01T00:00:00Z", attr.InitialStart);
        Assert.Equal("UTC", attr.Timezone);
        Assert.True(attr.IfNotExists);
    }

    #endregion
}
