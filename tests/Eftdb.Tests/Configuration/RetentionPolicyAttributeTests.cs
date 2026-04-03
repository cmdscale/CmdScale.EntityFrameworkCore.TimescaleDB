using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify RetentionPolicyAttribute constructor validation, mutual exclusivity, and default values.
/// </summary>
public class RetentionPolicyAttributeTests
{
    #region Constructor1 Validation Tests (string dropAfter)

    [Fact]
    public void Constructor1_With_Null_DropAfter_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RetentionPolicyAttribute(null!));
        Assert.Contains("DropAfter must be provided", ex.Message);
        Assert.Equal("dropAfter", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Empty_DropAfter_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RetentionPolicyAttribute(""));
        Assert.Contains("DropAfter must be provided", ex.Message);
        Assert.Equal("dropAfter", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Whitespace_DropAfter_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RetentionPolicyAttribute("   "));
        Assert.Contains("DropAfter must be provided", ex.Message);
        Assert.Equal("dropAfter", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Tabs_DropAfter_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RetentionPolicyAttribute("\t\t"));
        Assert.Contains("DropAfter must be provided", ex.Message);
    }

    [Fact]
    public void Constructor1_With_Valid_DropAfter_SetsDropAfterCorrectly()
    {
        // Arrange & Act
        RetentionPolicyAttribute attr = new("7 days");

        // Assert
        Assert.Equal("7 days", attr.DropAfter);
    }

    #endregion

    #region Constructor2 Mutual Exclusivity Tests (string? dropAfter, string? dropCreatedBefore)

    [Fact]
    public void Constructor2_With_DropAfterOnly_SetsDropAfterAndDropCreatedBeforeIsNull()
    {
        // Arrange & Act
        RetentionPolicyAttribute attr = new(dropAfter: "7 days");

        // Assert
        Assert.Equal("7 days", attr.DropAfter);
        Assert.Null(attr.DropCreatedBefore);
    }

    [Fact]
    public void Constructor2_With_DropCreatedBeforeOnly_SetsDropCreatedBeforeAndDropAfterIsNull()
    {
        // Arrange & Act
        RetentionPolicyAttribute attr = new(dropCreatedBefore: "30 days");

        // Assert
        Assert.Null(attr.DropAfter);
        Assert.Equal("30 days", attr.DropCreatedBefore);
    }

    [Fact]
    public void Constructor2_With_BothSpecified_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new RetentionPolicyAttribute(dropAfter: "7 days", dropCreatedBefore: "30 days"));
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Constructor2_With_NeitherSpecified_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new RetentionPolicyAttribute(dropAfter: null, dropCreatedBefore: null));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Constructor1_With_Valid_DropAfter_SetsDefaultValues()
    {
        // Arrange & Act
        RetentionPolicyAttribute attr = new("7 days");

        // Assert
        Assert.Equal(-1, attr.MaxRetries);
        Assert.Null(attr.DropCreatedBefore);
        Assert.Null(attr.InitialStart);
        Assert.Null(attr.ScheduleInterval);
        Assert.Null(attr.MaxRuntime);
        Assert.Null(attr.RetryPeriod);
    }

    #endregion

    #region Property Assignment Tests

    [Fact]
    public void InitialStart_CanBeSet()
    {
        // Arrange
        RetentionPolicyAttribute attr = new("7 days")
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
        RetentionPolicyAttribute attr = new("7 days")
        {
            // Act
            ScheduleInterval = "1 day"
        };

        // Assert
        Assert.Equal("1 day", attr.ScheduleInterval);
    }

    [Fact]
    public void MaxRuntime_CanBeSet()
    {
        // Arrange
        RetentionPolicyAttribute attr = new("7 days")
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
        RetentionPolicyAttribute attr = new("7 days")
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
        RetentionPolicyAttribute attr = new("7 days")
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
        RetentionPolicyAttribute attr = new("7 days")
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
        RetentionPolicyAttribute attr = new("7 days")
        {
            // Act
            InitialStart = "2025-01-01T00:00:00Z",
            ScheduleInterval = "1 day",
            MaxRuntime = "1 hour",
            MaxRetries = 3,
            RetryPeriod = "30 minutes"
        };

        // Assert
        Assert.Equal("7 days", attr.DropAfter);
        Assert.Equal("2025-01-01T00:00:00Z", attr.InitialStart);
        Assert.Equal("1 day", attr.ScheduleInterval);
        Assert.Equal("1 hour", attr.MaxRuntime);
        Assert.Equal(3, attr.MaxRetries);
        Assert.Equal("30 minutes", attr.RetryPeriod);
    }

    #endregion

    #region Constructor2 Empty and Whitespace String Tests

    [Fact]
    public void Constructor2_With_Both_Empty_Strings_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new RetentionPolicyAttribute(dropAfter: "", dropCreatedBefore: ""));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor2_With_Both_Whitespace_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new RetentionPolicyAttribute(dropAfter: "   ", dropCreatedBefore: "   "));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
