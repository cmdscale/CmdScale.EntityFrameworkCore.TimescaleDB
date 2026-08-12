using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify CompressionPolicyAttribute constructor validation, mutual exclusivity, and default values.
/// </summary>
public class CompressionPolicyAttributeTests
{
    #region Constructor1 Validation Tests (string after)

    [Fact]
    public void Constructor1_With_Null_After_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new CompressionPolicyAttribute(null!));
        Assert.Contains("After must be provided", ex.Message);
        Assert.Equal("after", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Empty_After_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new CompressionPolicyAttribute(""));
        Assert.Contains("After must be provided", ex.Message);
        Assert.Equal("after", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Whitespace_After_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new CompressionPolicyAttribute("   "));
        Assert.Contains("After must be provided", ex.Message);
        Assert.Equal("after", ex.ParamName);
    }

    [Fact]
    public void Constructor1_With_Valid_After_SetsAfterCorrectly()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new("7 days");

        // Assert
        Assert.Equal("7 days", attr.After);
        Assert.Null(attr.CreatedBefore);
    }

    #endregion

    #region Constructor2 Mutual Exclusivity Tests (string? after, string? createdBefore)

    [Fact]
    public void Constructor2_With_AfterOnly_SetsAfterAndCreatedBeforeIsNull()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new(after: "7 days");

        // Assert
        Assert.Equal("7 days", attr.After);
        Assert.Null(attr.CreatedBefore);
    }

    [Fact]
    public void Constructor2_With_CreatedBeforeOnly_SetsCreatedBeforeAndAfterIsNull()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new(createdBefore: "30 days");

        // Assert
        Assert.Null(attr.After);
        Assert.Equal("30 days", attr.CreatedBefore);
    }

    [Fact]
    public void Constructor2_With_BothSpecified_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new CompressionPolicyAttribute(after: "7 days", createdBefore: "30 days"));
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Constructor2_With_NeitherSpecified_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new CompressionPolicyAttribute(after: null, createdBefore: null));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor2_With_Both_Whitespace_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => new CompressionPolicyAttribute(after: "   ", createdBefore: "   "));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Constructor1_With_Valid_After_SetsDefaultValues()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new("7 days");

        // Assert
        Assert.Null(attr.CreatedBefore);
        Assert.Null(attr.ScheduleInterval);
        Assert.Null(attr.InitialStart);
        Assert.Null(attr.Timezone);
        Assert.False(attr.IfNotExists);
    }

    #endregion

    #region Property Initializer Style Tests

    [Fact]
    public void PropertyInitializer_With_After_SetsAfterWithoutThrowing()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new() { After = "7 days" };

        // Assert
        Assert.Equal("7 days", attr.After);
        Assert.Null(attr.CreatedBefore);
    }

    [Fact]
    public void PropertyInitializer_With_CreatedBefore_SetsCreatedBeforeWithoutThrowing()
    {
        // Arrange & Act
        CompressionPolicyAttribute attr = new() { CreatedBefore = "30 days" };

        // Assert
        Assert.Null(attr.After);
        Assert.Equal("30 days", attr.CreatedBefore);
    }

    #endregion

    #region Property Assignment Tests

    [Fact]
    public void ScheduleInterval_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new("7 days")
        {
            // Act
            ScheduleInterval = "12 hours"
        };

        // Assert
        Assert.Equal("12 hours", attr.ScheduleInterval);
    }

    [Fact]
    public void InitialStart_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new("7 days")
        {
            // Act
            InitialStart = "2025-10-01T03:00:00Z"
        };

        // Assert
        Assert.Equal("2025-10-01T03:00:00Z", attr.InitialStart);
    }

    [Fact]
    public void Timezone_CanBeSet()
    {
        // Arrange
        CompressionPolicyAttribute attr = new("7 days")
        {
            // Act
            Timezone = "Europe/Berlin"
        };

        // Assert
        Assert.Equal("Europe/Berlin", attr.Timezone);
    }

    [Fact]
    public void IfNotExists_CanBeSetToTrue()
    {
        // Arrange
        CompressionPolicyAttribute attr = new("7 days")
        {
            // Act
            IfNotExists = true
        };

        // Assert
        Assert.True(attr.IfNotExists);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        CompressionPolicyAttribute attr = new("7 days")
        {
            // Act
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
