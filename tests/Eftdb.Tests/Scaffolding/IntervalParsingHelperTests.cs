using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class IntervalParsingHelperTests
{
    #region ParseIntervalOrInteger Tests

    #region Should_Return_Null_For_Null_JsonElement

    [Fact]
    public void Should_Return_Null_For_Null_JsonElement()
    {
        // Arrange
        string json = "null";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Normalized_Interval_For_String_JsonElement

    [Fact]
    public void Should_Return_Normalized_Interval_For_String_JsonElement()
    {
        // Arrange
        string json = "\"7 days\"";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("7 days", result);
    }

    #endregion

    #region Should_Return_Empty_String_For_Empty_String_JsonElement

    [Fact]
    public void Should_Return_Empty_String_For_Empty_String_JsonElement()
    {
        // Arrange
        string json = "\"\"";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region Should_Normalize_TimeSpan_Format_For_String_JsonElement

    [Fact]
    public void Should_Normalize_TimeSpan_Format_For_String_JsonElement()
    {
        // Arrange
        string json = "\"01:00:00\"";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("1 hour", result);
    }

    #endregion

    #region Should_Normalize_PostgreSQL_Month_Format

    [Fact]
    public void Should_Normalize_PostgreSQL_Month_Format()
    {
        // Arrange
        string json = "\"1 mon\"";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("1 month", result);
    }

    #endregion

    #region Should_Return_Integer_String_For_Number_JsonElement

    [Fact]
    public void Should_Return_Integer_String_For_Number_JsonElement()
    {
        // Arrange
        string json = "604800000000";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("604800000000", result);
    }

    #endregion

    #region Should_Return_Negative_Integer_String_For_Negative_Number_JsonElement

    [Fact]
    public void Should_Return_Negative_Integer_String_For_Negative_Number_JsonElement()
    {
        // Arrange
        string json = "-123456";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("-123456", result);
    }

    #endregion

    #region Should_Return_Zero_String_For_Zero_Number_JsonElement

    [Fact]
    public void Should_Return_Zero_String_For_Zero_Number_JsonElement()
    {
        // Arrange
        string json = "0";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Equal("0", result);
    }

    #endregion

    #region Should_Return_Null_For_Boolean_JsonElement

    [Fact]
    public void Should_Return_Null_For_Boolean_JsonElement()
    {
        // Arrange
        string json = "true";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Array_JsonElement

    [Fact]
    public void Should_Return_Null_For_Array_JsonElement()
    {
        // Arrange
        string json = "[\"item1\", \"item2\"]";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Object_JsonElement

    [Fact]
    public void Should_Return_Null_For_Object_JsonElement()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement element = doc.RootElement;

        // Act
        string? result = IntervalParsingHelper.ParseIntervalOrInteger(element);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #endregion

    #region NormalizeInterval Tests

    #region Should_Return_Null_For_Null_Input

    [Fact]
    public void Should_Return_Null_For_Null_Input()
    {
        // Arrange
        string? input = null;

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input!);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Empty_For_Empty_String

    [Fact]
    public void Should_Return_Empty_For_Empty_String()
    {
        // Arrange
        string input = string.Empty;

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region Should_Return_Whitespace_For_Whitespace_String

    [Fact]
    public void Should_Return_Whitespace_For_Whitespace_String()
    {
        // Arrange
        string input = "   ";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("   ", result);
    }

    #endregion

    #region Should_Return_As_Is_For_Human_Readable_Format

    [Theory]
    [InlineData("7 days", "7 days")]
    [InlineData("1 day", "1 day")]
    [InlineData("30 days", "30 days")]
    [InlineData("2 hours", "2 hours")]
    [InlineData("15 minutes", "15 minutes")]
    public void Should_Return_As_Is_For_Human_Readable_Format(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Convert_Mon_To_Month

    [Theory]
    [InlineData("1 mon", "1 month")]
    [InlineData("6 mon", "6 month")]
    [InlineData("12 mon", "12 month")]
    public void Should_Convert_Mon_To_Month(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Convert_TimeSpan_To_Hours

    [Theory]
    [InlineData("01:00:00", "1 hour")]
    [InlineData("02:00:00", "2 hours")]
    [InlineData("10:00:00", "10 hours")]
    [InlineData("23:00:00", "23 hours")]
    public void Should_Convert_TimeSpan_To_Hours(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Convert_TimeSpan_To_Minutes

    [Theory]
    [InlineData("00:01:00", "1 minute")]
    [InlineData("00:05:00", "5 minutes")]
    [InlineData("00:15:00", "15 minutes")]
    [InlineData("00:30:00", "30 minutes")]
    [InlineData("00:59:00", "59 minutes")]
    public void Should_Convert_TimeSpan_To_Minutes(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Convert_TimeSpan_To_Days

    [Theory]
    [InlineData("1.00:00:00", "1 day")]
    [InlineData("2.00:00:00", "2 days")]
    [InlineData("7.00:00:00", "7 days")]
    [InlineData("30.00:00:00", "30 days")]
    public void Should_Convert_TimeSpan_To_Days(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Use_Singular_For_One_Unit

    [Theory]
    [InlineData("00:01:00", "1 minute")]
    [InlineData("01:00:00", "1 hour")]
    [InlineData("1.00:00:00", "1 day")]
    public void Should_Use_Singular_For_One_Unit(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Use_Plural_For_Multiple_Units

    [Theory]
    [InlineData("00:02:00", "2 minutes")]
    [InlineData("02:00:00", "2 hours")]
    [InlineData("2.00:00:00", "2 days")]
    public void Should_Use_Plural_For_Multiple_Units(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Return_Original_For_Unparseable_Format

    [Theory]
    [InlineData("invalid format", "invalid format")]
    [InlineData("not a timespan", "not a timespan")]
    [InlineData("abc:def:ghi", "abc:def:ghi")]
    public void Should_Return_Original_For_Unparseable_Format(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Trim_Whitespace

    [Theory]
    [InlineData("  7 days  ", "7 days")]
    [InlineData("  1 mon  ", "1 month")]
    [InlineData("  01:00:00  ", "1 hour")]
    public void Should_Trim_Whitespace(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Handle_TimeSpan_With_Seconds_Only

    [Fact]
    public void Should_Handle_TimeSpan_With_Seconds_Only()
    {
        // Arrange
        string input = "00:00:30";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        // When timespan has only seconds (no minutes or hours), it doesn't match any condition
        // so it should return the normalized string (with "mon" replaced if any, otherwise trimmed original)
        Assert.Equal("00:00:30", result);
    }

    #endregion

    #region Should_Handle_TimeSpan_With_Hours_And_Minutes

    [Fact]
    public void Should_Handle_TimeSpan_With_Hours_And_Minutes()
    {
        // Arrange
        string input = "01:30:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        // When timespan has both hours and minutes, hours take precedence if totalHours < 24
        Assert.Equal("1 hour", result);
    }

    #endregion

    #region Should_Handle_TimeSpan_Exceeding_24_Hours

    [Fact]
    public void Should_Handle_TimeSpan_Exceeding_24_Hours()
    {
        // Arrange - 48 hours should be 2 days
        string input = "2.00:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("2 days", result);
    }

    #endregion

    #region Should_Handle_Zero_Minutes_With_NonZero_Seconds

    [Fact]
    public void Should_Handle_Zero_Minutes_With_NonZero_Seconds()
    {
        // Arrange
        string input = "00:00:45";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        // Minutes is 0, so the minutes condition won't match
        Assert.Equal("00:00:45", result);
    }

    #endregion

    #region Should_Handle_TimeSpan_At_60_Minutes

    [Fact]
    public void Should_Handle_TimeSpan_At_60_Minutes()
    {
        // Arrange - Exactly 60 minutes (1 hour)
        string input = "01:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        // TotalMinutes is 60 which is NOT < 60, so it should go to hours check
        Assert.Equal("1 hour", result);
    }

    #endregion

    #region Should_Handle_Mixed_TimeSpan_With_Days_Hours_Minutes

    [Fact]
    public void Should_Handle_Mixed_TimeSpan_With_Days_Hours_Minutes()
    {
        // Arrange - 1 day, 5 hours, 30 minutes
        string input = "1.05:30:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        // Days > 0, so it should return days only
        Assert.Equal("1 day", result);
    }

    #endregion

    #endregion
}
