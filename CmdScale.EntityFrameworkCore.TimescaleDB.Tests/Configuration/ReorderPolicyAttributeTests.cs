using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify ReorderPolicyAttribute constructor validation and default values.
/// </summary>
public class ReorderPolicyAttributeTests
{
    #region Constructor Validation Tests

    [Fact]
    public void Constructor_With_Null_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute(null!));
        Assert.Contains("IndexName must be provided", ex.Message);
        Assert.Equal("indexName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Empty_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute(""));
        Assert.Contains("IndexName must be provided", ex.Message);
        Assert.Equal("indexName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Whitespace_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute("   "));
        Assert.Contains("IndexName must be provided", ex.Message);
        Assert.Equal("indexName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Tabs_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute("\t\t"));
        Assert.Contains("IndexName must be provided", ex.Message);
    }

    [Fact]
    public void Constructor_With_Newlines_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute("\n\n"));
        Assert.Contains("IndexName must be provided", ex.Message);
    }

    [Fact]
    public void Constructor_With_MixedWhitespace_IndexName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ReorderPolicyAttribute(" \t\n "));
        Assert.Contains("IndexName must be provided", ex.Message);
    }

    #endregion

    #region Valid Constructor Tests

    [Fact]
    public void Constructor_With_Valid_IndexName_InitializesCorrectly()
    {
        // Arrange & Act
        ReorderPolicyAttribute attr = new("IX_Metrics_DeviceId_Time");

        // Assert
        Assert.Equal("IX_Metrics_DeviceId_Time", attr.IndexName);
    }

    [Fact]
    public void Constructor_With_Valid_IndexName_SetsDefaultValues()
    {
        // Arrange & Act
        ReorderPolicyAttribute attr = new("IX_Test");

        // Assert
        Assert.Null(attr.InitialStart);
        Assert.Null(attr.ScheduleInterval);
        Assert.Null(attr.MaxRuntime);
        Assert.Equal(-1, attr.MaxRetries);
        Assert.Null(attr.RetryPeriod);
    }

    [Fact]
    public void Constructor_With_SimpleIndexName_AcceptsIt()
    {
        // Arrange & Act
        ReorderPolicyAttribute attr = new("simple_index");

        // Assert
        Assert.Equal("simple_index", attr.IndexName);
    }

    [Fact]
    public void Constructor_With_PascalCaseIndexName_AcceptsIt()
    {
        // Arrange & Act
        ReorderPolicyAttribute attr = new("IX_SensorReadings_SensorId_Timestamp");

        // Assert
        Assert.Equal("IX_SensorReadings_SensorId_Timestamp", attr.IndexName);
    }

    #endregion

    #region Property Assignment Tests

    [Fact]
    public void InitialStart_CanBeSet()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            InitialStart = "2025-01-01T00:00:00Z"
        };

        // Assert
        Assert.Equal("2025-01-01T00:00:00Z", attr.InitialStart);
    }

    [Fact]
    public void ScheduleInterval_CanBeSet()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            ScheduleInterval = "2 days"
        };

        // Assert
        Assert.Equal("2 days", attr.ScheduleInterval);
    }

    [Fact]
    public void MaxRuntime_CanBeSet()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            MaxRuntime = "1 hour"
        };

        // Assert
        Assert.Equal("1 hour", attr.MaxRuntime);
    }

    [Fact]
    public void MaxRetries_CanBeSetToPositiveValue()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            MaxRetries = 5
        };

        // Assert
        Assert.Equal(5, attr.MaxRetries);
    }

    [Fact]
    public void MaxRetries_CanBeSetToZero()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            MaxRetries = 0
        };

        // Assert
        Assert.Equal(0, attr.MaxRetries);
    }

    [Fact]
    public void RetryPeriod_CanBeSet()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            RetryPeriod = "30 minutes"
        };

        // Assert
        Assert.Equal("30 minutes", attr.RetryPeriod);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        ReorderPolicyAttribute attr = new("IX_Test")
        {
            // Act
            InitialStart = "2025-01-01T00:00:00Z",
            ScheduleInterval = "2 days",
            MaxRuntime = "1 hour",
            MaxRetries = 3,
            RetryPeriod = "30 minutes"
        };

        // Assert
        Assert.Equal("2025-01-01T00:00:00Z", attr.InitialStart);
        Assert.Equal("2 days", attr.ScheduleInterval);
        Assert.Equal("1 hour", attr.MaxRuntime);
        Assert.Equal(3, attr.MaxRetries);
        Assert.Equal("30 minutes", attr.RetryPeriod);
    }

    #endregion
}
