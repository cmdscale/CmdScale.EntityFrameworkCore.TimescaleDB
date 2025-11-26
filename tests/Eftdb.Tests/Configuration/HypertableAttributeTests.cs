using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify HypertableAttribute constructor validation and default values.
/// </summary>
public class HypertableAttributeTests
{
    #region Constructor Validation Tests

    [Fact]
    public void Constructor_With_Null_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute(null!));
        Assert.Contains("Time column name must be provided", ex.Message);
        Assert.Equal("timeColumnName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Empty_String_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute(""));
        Assert.Contains("Time column name must be provided", ex.Message);
        Assert.Equal("timeColumnName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Whitespace_Only_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute("   "));
        Assert.Contains("Time column name must be provided", ex.Message);
        Assert.Equal("timeColumnName", ex.ParamName);
    }

    [Fact]
    public void Constructor_With_Tabs_Only_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute("\t\t"));
        Assert.Contains("Time column name must be provided", ex.Message);
    }

    [Fact]
    public void Constructor_With_Newlines_Only_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute("\n\n"));
        Assert.Contains("Time column name must be provided", ex.Message);
    }

    [Fact]
    public void Constructor_With_MixedWhitespace_TimeColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new HypertableAttribute(" \t\n "));
        Assert.Contains("Time column name must be provided", ex.Message);
    }

    #endregion

    #region Valid Constructor Tests

    [Fact]
    public void Constructor_With_Valid_TimeColumnName_InitializesCorrectly()
    {
        // Arrange & Act
        HypertableAttribute attr = new("Timestamp");

        // Assert
        Assert.Equal("Timestamp", attr.TimeColumnName);
    }

    [Fact]
    public void Constructor_With_Valid_TimeColumnName_SetsDefaultValues()
    {
        // Arrange & Act
        HypertableAttribute attr = new("Timestamp");

        // Assert
        Assert.False(attr.EnableCompression);
        Assert.Equal(DefaultValues.ChunkTimeInterval, attr.ChunkTimeInterval);
        Assert.Null(attr.ChunkSkipColumns);
    }

    [Fact]
    public void Constructor_With_Underscore_TimeColumnName_AcceptsIt()
    {
        // Arrange & Act
        HypertableAttribute attr = new("created_at");

        // Assert
        Assert.Equal("created_at", attr.TimeColumnName);
    }

    [Fact]
    public void Constructor_With_PascalCase_TimeColumnName_AcceptsIt()
    {
        // Arrange & Act
        HypertableAttribute attr = new("CreatedAt");

        // Assert
        Assert.Equal("CreatedAt", attr.TimeColumnName);
    }

    #endregion

    #region Property Assignment Tests

    [Fact]
    public void EnableCompression_CanBeSetToTrue()
    {
        // Arrange
        HypertableAttribute attr = new("Timestamp")
        {
            // Act
            EnableCompression = true
        };

        // Assert
        Assert.True(attr.EnableCompression);
    }

    [Fact]
    public void ChunkTimeInterval_CanBeSetToCustomValue()
    {
        // Arrange
        HypertableAttribute attr = new("Timestamp")
        {
            // Act
            ChunkTimeInterval = "1 hour"
        };

        // Assert
        Assert.Equal("1 hour", attr.ChunkTimeInterval);
    }

    [Fact]
    public void ChunkSkipColumns_CanBeSetToArray()
    {
        // Arrange
        HypertableAttribute attr = new("Timestamp")
        {
            // Act
            ChunkSkipColumns = ["Value", "DeviceId"]
        };

        // Assert
        Assert.Equal(2, attr.ChunkSkipColumns.Length);
        Assert.Contains("Value", attr.ChunkSkipColumns);
        Assert.Contains("DeviceId", attr.ChunkSkipColumns);
    }

    [Fact]
    public void ChunkSkipColumns_CanBeSetToEmptyArray()
    {
        // Arrange
        HypertableAttribute attr = new("Timestamp")
        {
            // Act
            ChunkSkipColumns = []
        };

        // Assert
        Assert.NotNull(attr.ChunkSkipColumns);
        Assert.Empty(attr.ChunkSkipColumns);
    }

    #endregion
}
