using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify Dimension factory methods and validation.
/// </summary>
public class DimensionTests
{
    #region CreateHash Validation Tests

    [Fact]
    public void CreateHash_With_Null_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash(null!, 4));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateHash_With_Empty_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("", 4));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateHash_With_Whitespace_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("   ", 4));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateHash_With_Zero_Partitions_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("DeviceId", 0));
        Assert.Contains("Number of partitions must be greater than zero", ex.Message);
        Assert.Equal("numberOfPartitions", ex.ParamName);
    }

    [Fact]
    public void CreateHash_With_Negative_Partitions_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("DeviceId", -5));
        Assert.Contains("Number of partitions must be greater than zero", ex.Message);
        Assert.Equal("numberOfPartitions", ex.ParamName);
    }

    [Fact]
    public void CreateHash_With_NegativeOne_Partitions_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("DeviceId", -1));
        Assert.Contains("Number of partitions must be greater than zero", ex.Message);
    }

    [Fact]
    public void CreateHash_With_Tabs_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateHash("\t\t", 4));
        Assert.Contains("Dimension column name must be provided", ex.Message);
    }

    #endregion

    #region CreateHash Valid Tests

    [Fact]
    public void CreateHash_With_Valid_Parameters_CreatesHashDimension()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateHash("DeviceId", 4);

        // Assert
        Assert.Equal("DeviceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
        Assert.Equal(4, dimension.NumberOfPartitions);
        Assert.Null(dimension.Interval);
    }

    [Fact]
    public void CreateHash_With_Single_Partition_CreatesCorrectly()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateHash("Id", 1);

        // Assert
        Assert.Equal(1, dimension.NumberOfPartitions);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
    }

    [Fact]
    public void CreateHash_With_Large_Partition_Count_CreatesCorrectly()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateHash("TenantId", 256);

        // Assert
        Assert.Equal(256, dimension.NumberOfPartitions);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
    }

    [Fact]
    public void CreateHash_With_Underscore_ColumnName_AcceptsIt()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateHash("device_id", 8);

        // Assert
        Assert.Equal("device_id", dimension.ColumnName);
    }

    #endregion

    #region CreateRange Validation Tests

    [Fact]
    public void CreateRange_With_Null_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange(null!, "1 day"));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Empty_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("", "1 day"));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Whitespace_ColumnName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("   ", "1 day"));
        Assert.Contains("Dimension column name must be provided", ex.Message);
        Assert.Equal("columnName", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Null_Interval_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("SensorId", null!));
        Assert.Contains("Interval must be provided for a range dimension", ex.Message);
        Assert.Equal("interval", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Empty_Interval_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("SensorId", ""));
        Assert.Contains("Interval must be provided for a range dimension", ex.Message);
        Assert.Equal("interval", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Whitespace_Interval_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("SensorId", "   "));
        Assert.Contains("Interval must be provided for a range dimension", ex.Message);
        Assert.Equal("interval", ex.ParamName);
    }

    [Fact]
    public void CreateRange_With_Tabs_Interval_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("SensorId", "\t\t"));
        Assert.Contains("Interval must be provided for a range dimension", ex.Message);
    }

    [Fact]
    public void CreateRange_With_Newlines_Interval_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Dimension.CreateRange("SensorId", "\n\n"));
        Assert.Contains("Interval must be provided for a range dimension", ex.Message);
    }

    #endregion

    #region CreateRange Valid Tests

    [Fact]
    public void CreateRange_With_Valid_Parameters_CreatesRangeDimension()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateRange("SensorId", "1000");

        // Assert
        Assert.Equal("SensorId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Equal("1000", dimension.Interval);
        Assert.Null(dimension.NumberOfPartitions);
    }

    [Fact]
    public void CreateRange_With_TimeInterval_CreatesCorrectly()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateRange("Timestamp", "1 day");

        // Assert
        Assert.Equal("1 day", dimension.Interval);
        Assert.Equal(EDimensionType.Range, dimension.Type);
    }

    [Fact]
    public void CreateRange_With_HourInterval_CreatesCorrectly()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateRange("EventTime", "6 hours");

        // Assert
        Assert.Equal("6 hours", dimension.Interval);
    }

    [Fact]
    public void CreateRange_With_NumericInterval_CreatesCorrectly()
    {
        // Arrange & Act
        Dimension dimension = Dimension.CreateRange("SequenceId", "10000");

        // Assert
        Assert.Equal("10000", dimension.Interval);
    }

    #endregion

    #region Parameterless Constructor Tests

    [Fact]
    public void ParameterlessConstructor_InitializesDefaultsCorrectly()
    {
        // Arrange & Act
        Dimension dimension = new();

        // Assert
        Assert.Equal(string.Empty, dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Null(dimension.NumberOfPartitions);
        Assert.Null(dimension.Interval);
    }

    [Fact]
    public void ParameterlessConstructor_AllowsPropertyMutation()
    {
        // Arrange
        Dimension dimension = new()
        {
            // Act
            ColumnName = "TestColumn",
            Type = EDimensionType.Hash,
            NumberOfPartitions = 16,
            Interval = "1 week"
        };

        // Assert
        Assert.Equal("TestColumn", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
        Assert.Equal(16, dimension.NumberOfPartitions);
        Assert.Equal("1 week", dimension.Interval);
    }

    #endregion
}
