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

        // Assert
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

    // ── Sub-day chunk intervals: minute-level half-interval ──────────────────

    #region Should_Return_1_Minute_When_Chunk_Is_2_Minutes

    [Fact]
    public void Should_Return_1_Minute_When_Chunk_Is_2_Minutes()
    {
        // Arrange
        string chunkTimeInterval = "2 minutes";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 minute", result);
    }

    #endregion

    #region Should_Return_Minutes_When_Chunk_Is_4_Minutes

    [Fact]
    public void Should_Return_Minutes_When_Chunk_Is_4_Minutes()
    {
        // Arrange
        string chunkTimeInterval = "4 minutes";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("2 minutes", result);
    }

    #endregion

    #region Should_Return_1_Second_When_Chunk_Is_2_Seconds

    [Fact]
    public void Should_Return_1_Second_When_Chunk_Is_2_Seconds()
    {
        // Arrange
        string chunkTimeInterval = "2 seconds";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 second", result);
    }

    #endregion

    #region Should_Return_Seconds_When_Chunk_Is_4_Seconds

    [Fact]
    public void Should_Return_Seconds_When_Chunk_Is_4_Seconds()
    {
        // Arrange
        string chunkTimeInterval = "4 seconds";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("2 seconds", result);
    }

    #endregion

    #region Should_Return_Null_When_Half_Interval_Is_Not_Representable

    [Fact]
    public void Should_Return_Null_When_Half_Interval_Is_Not_Representable()
    {
        // Arrange
        string chunkTimeInterval = "1 us";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_When_Half_Interval_Is_Sub_Second_Non_Zero

    [Fact]
    public void Should_Return_Null_When_Half_Interval_Is_Sub_Second_Non_Zero()
    {
        // Arrange
        string chunkTimeInterval = "1 second";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_When_Half_Interval_Is_500ms

    [Fact]
    public void Should_Return_Null_When_Half_Interval_Is_500ms()
    {
        // Arrange
        string chunkTimeInterval = "500 ms";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    // ── Sub-day unit aliases ──────────────────────────────────────────────────

    #region Should_Parse_Millisecond_Unit_Aliases

    [Fact]
    public void Should_Parse_Millisecond_Unit_Aliases()
    {
        // Arrange
        string chunkTimeInterval = "2000 ms";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 second", result);
    }

    #endregion

    #region Should_Parse_Microsecond_Unit_Aliases

    [Fact]
    public void Should_Parse_Microsecond_Unit_Aliases()
    {
        // Arrange
        string chunkTimeInterval = "2000000 us";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 second", result);
    }

    #endregion

    // ── HH:MM:SS with fractional seconds ─────────────────────────────────────

    #region Should_Parse_HH_MM_SS_With_Fractional_Seconds

    [Fact]
    public void Should_Parse_HH_MM_SS_With_Fractional_Seconds()
    {
        // Arrange
        string chunkTimeInterval = "00:00:02.000000";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("1 second", result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Invalid_Fraction

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Invalid_Fraction()
    {
        // Arrange
        string chunkTimeInterval = "00:00:02.abc";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Empty_Fraction

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Empty_Fraction()
    {
        // Arrange
        string chunkTimeInterval = "00:00:02.";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    // ── HH:MM:SS form with D.HH prefix ───────────────────────────────────────

    #region Should_Parse_Day_Dot_HH_MM_SS_Form

    [Fact]
    public void Should_Parse_Day_Dot_HH_MM_SS_Form()
    {
        // Arrange
        string chunkTimeInterval = "1.00:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Equal("12 hours", result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Negative_Days

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Negative_Days()
    {
        // Arrange
        string chunkTimeInterval = "-1.00:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Hours_Out_Of_Range

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Hours_Out_Of_Range()
    {
        // Arrange
        string chunkTimeInterval = "1.24:00:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Minutes_Out_Of_Range

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Minutes_Out_Of_Range()
    {
        // Arrange
        string chunkTimeInterval = "00:60:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Seconds_Out_Of_Range

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Seconds_Out_Of_Range()
    {
        // Arrange
        string chunkTimeInterval = "00:00:60";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_HH_MM_SS_With_Wrong_Part_Count

    [Fact]
    public void Should_Return_Null_For_HH_MM_SS_With_Wrong_Part_Count()
    {
        // Arrange
        string chunkTimeInterval = "04:00";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    // ── TryGetTotalMicroseconds: parse failures ───────────────────────────────

    #region Should_Return_Null_For_Non_Numeric_Amount

    [Fact]
    public void Should_Return_Null_For_Non_Numeric_Amount()
    {
        // Arrange
        string chunkTimeInterval = "abc days";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Unknown_Unit

    [Fact]
    public void Should_Return_Null_For_Unknown_Unit()
    {
        // Arrange
        string chunkTimeInterval = "2 fortnights";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Missing_Space_In_Interval

    [Fact]
    public void Should_Return_Null_For_Missing_Space_In_Interval()
    {
        // Arrange
        string chunkTimeInterval = "7days";

        // Act
        string? result = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
