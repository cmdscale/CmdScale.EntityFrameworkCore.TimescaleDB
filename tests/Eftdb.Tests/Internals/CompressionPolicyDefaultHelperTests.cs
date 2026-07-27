using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

public class CompressionPolicyDefaultHelperTests
{
    #region Should_Return_12_Hours_When_Chunk_Interval_Is_Exactly_1_Day

    [Fact]
    public void Should_Return_12_Hours_When_Chunk_Interval_Is_Exactly_1_Day()
    {
        // Arrange
        string chunkTimeInterval = "1 day";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_12_Hours_When_Chunk_Interval_Is_7_Days

    [Fact]
    public void Should_Return_12_Hours_When_Chunk_Interval_Is_7_Days()
    {
        // Arrange
        string chunkTimeInterval = "7 days";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_12_Hours_When_Chunk_Interval_Is_1_Week

    [Fact]
    public void Should_Return_12_Hours_When_Chunk_Interval_Is_1_Week()
    {
        // Arrange
        string chunkTimeInterval = "1 week";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_12_Hours_When_Chunk_Interval_Is_Null

    [Fact]
    public void Should_Return_12_Hours_When_Chunk_Interval_Is_Null()
    {
        // Arrange
        string? chunkTimeInterval = null;

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_12_Hours_When_Chunk_Interval_Is_Empty

    [Fact]
    public void Should_Return_12_Hours_When_Chunk_Interval_Is_Empty()
    {
        // Arrange
        string chunkTimeInterval = "";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_Half_Interval_When_Chunk_Is_4_Hours

    [Fact]
    public void Should_Return_Half_Interval_When_Chunk_Is_4_Hours()
    {
        // Arrange
        string chunkTimeInterval = "4 hours";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("2 hours", result);
    }

    #endregion

    #region Should_Return_Half_Interval_When_Chunk_Is_1_Hour

    [Fact]
    public void Should_Return_Half_Interval_When_Chunk_Is_1_Hour()
    {
        // Arrange
        string chunkTimeInterval = "1 hour";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("30 minutes", result);
    }

    #endregion

    #region Should_Return_Half_Interval_When_Chunk_Is_2_Hours

    [Fact]
    public void Should_Return_Half_Interval_When_Chunk_Is_2_Hours()
    {
        // Arrange
        string chunkTimeInterval = "2 hours";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 hour", result);
    }

    #endregion

    #region Should_Return_Half_Interval_When_Chunk_Is_30_Minutes

    [Fact]
    public void Should_Return_Half_Interval_When_Chunk_Is_30_Minutes()
    {
        // Arrange
        string chunkTimeInterval = "30 minutes";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("15 minutes", result);
    }

    #endregion

    #region Should_Return_Null_For_Integer_Time_Input

    [Fact]
    public void Should_Return_Null_For_Integer_Time_Input()
    {
        // Arrange
        string chunkTimeInterval = "604800000000";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert — integer-time hypertables cannot be halved meaningfully; treat as explicitly configured
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Calendar_Unit_Months

    [Fact]
    public void Should_Return_Null_For_Calendar_Unit_Months()
    {
        // Arrange
        string chunkTimeInterval = "1 month";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Composite_Interval

    [Fact]
    public void Should_Return_Null_For_Composite_Interval()
    {
        // Arrange
        string chunkTimeInterval = "2 days 03:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_12_Hours_For_HH_MM_SS_Form_Of_24_Hours

    [Fact]
    public void Should_Return_12_Hours_For_HH_MM_SS_Form_Of_24_Hours()
    {
        // Arrange
        string chunkTimeInterval = "24:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_Half_Interval_For_HH_MM_SS_Form_Of_4_Hours

    [Fact]
    public void Should_Return_Half_Interval_For_HH_MM_SS_Form_Of_4_Hours()
    {
        // Arrange
        string chunkTimeInterval = "4:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("2 hours", result);
    }

    #endregion
}
