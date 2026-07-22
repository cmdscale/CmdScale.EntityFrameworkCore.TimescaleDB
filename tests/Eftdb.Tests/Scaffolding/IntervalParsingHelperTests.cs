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

    #region Should_Convert_Oversize_Hours_To_Days

    [Theory]
    [InlineData("24:00:00", "1 day")]
    [InlineData("48:00:00", "2 days")]
    [InlineData("168:00:00", "7 days")]
    [InlineData("240:00:00", "10 days")]
    [InlineData("720:00:00", "30 days")]
    public void Should_Convert_Oversize_Hours_To_Days(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Convert_Oversize_Non_Day_Aligned_Hours

    [Theory]
    [InlineData("25:00:00", "25 hours")]
    [InlineData("36:00:00", "36 hours")]
    public void Should_Convert_Oversize_Non_Day_Aligned_Hours(string input, string expected)
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
        Assert.Equal("30 seconds", result);
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
        Assert.Equal("90 minutes", result);
    }

    #endregion

    #region Should_Handle_TimeSpan_Exceeding_24_Hours

    [Fact]
    public void Should_Handle_TimeSpan_Exceeding_24_Hours()
    {
        // Arrange
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
        Assert.Equal("45 seconds", result);
    }

    #endregion

    #region Should_Handle_TimeSpan_At_60_Minutes

    [Fact]
    public void Should_Handle_TimeSpan_At_60_Minutes()
    {
        // Arrange
        string input = "01:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("1 hour", result);
    }

    #endregion

    #region Should_Handle_Mixed_TimeSpan_With_Days_Hours_Minutes

    [Fact]
    public void Should_Handle_Mixed_TimeSpan_With_Days_Hours_Minutes()
    {
        // Arrange
        string input = "1.05:30:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("1770 minutes", result);
    }

    #endregion

    #region Should_Keep_Fractional_Seconds_Raw

    [Theory]
    [InlineData("48:00:00.5")]
    [InlineData("168:00:00.5")]
    [InlineData("01:00:00.000001")]
    public void Should_Keep_Fractional_Seconds_Raw(string input)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(input, result);
    }

    #endregion

    #region Should_Humanize_When_Fraction_Is_Zero

    [Fact]
    public void Should_Humanize_When_Fraction_Is_Zero()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("48:00:00.000000");

        // Assert
        Assert.Equal("2 days", result);
    }

    #endregion

    #region Should_Expand_Mons_TokenSafe

    [Theory]
    [InlineData("2 mons", "2 months")]
    [InlineData("1 mon", "1 month")]
    [InlineData("1 month", "1 month")]
    [InlineData("6 months", "6 months")]
    public void Should_Expand_Mons_TokenSafe(string input, string expected)
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Should_Keep_Composite_Interval_Raw

    [Fact]
    public void Should_Keep_Composite_Interval_Raw()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("2 days 03:00:00");

        // Assert
        Assert.Equal("2 days 03:00:00", result);
    }

    #endregion

    // ── TryGetTotalMicroseconds ─────────────────────────────────────────────

    #region TryGetTotalMicroseconds_Parses_FixedDuration_Units

    [Theory]
    [InlineData("1 day", 86_400_000_000L)]
    [InlineData("7 days", 604_800_000_000L)]
    [InlineData("30 minutes", 1_800_000_000L)]
    [InlineData("1 hour", 3_600_000_000L)]
    [InlineData("2 weeks", 1_209_600_000_000L)]
    [InlineData("500 milliseconds", 500_000L)]
    [InlineData("10 seconds", 10_000_000L)]
    public void TryGetTotalMicroseconds_Parses_FixedDuration_Units(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_TimeOfDay_Form

    [Theory]
    [InlineData("01:00:00", 3_600_000_000L)]
    [InlineData("168:00:00", 604_800_000_000L)]
    [InlineData("00:00:30.5", 30_500_000L)]
    [InlineData("1.00:00:00", 86_400_000_000L)]
    public void TryGetTotalMicroseconds_Parses_TimeOfDay_Form(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Rejects_NonFixed_Durations

    [Theory]
    [InlineData("1 month")]
    [InlineData("2 years")]
    [InlineData("2 days 03:00:00")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData("garbage")]
    public void TryGetTotalMicroseconds_Rejects_NonFixed_Durations(string input)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, result);
    }

    #endregion

    #endregion

    // ── Additional edge cases ───────────────────────────────────────────────

    #region NormalizeInterval_Returns_Unchanged_For_Zero_Time

    [Fact]
    public void NormalizeInterval_Returns_Unchanged_For_Zero_Time()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("00:00:00");

        // Assert
        Assert.Equal("00:00:00", result);
    }

    #endregion

    #region NormalizeInterval_Returns_Correct_Value_For_Negative_Day_Prefix

    [Fact]
    public void NormalizeInterval_Returns_Correct_Value_For_Negative_Day_Prefix()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("-1.00:00:00");

        // Assert
        Assert.Equal("-1.00:00:00", result);
    }

    #endregion

    #region TryParseTimeParts_Returns_False_For_Minutes_Out_Of_Range

    [Fact]
    public void TryParseTimeParts_Returns_False_For_Minutes_Out_Of_Range()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("01:75:00");

        // Assert
        Assert.Equal("01:75:00", result);
    }

    #endregion

    #region TryParseTimeParts_Returns_False_For_Non_Digit_Fraction

    [Fact]
    public void TryParseTimeParts_Returns_False_For_Non_Digit_Fraction()
    {
        // Act
        string result = IntervalParsingHelper.NormalizeInterval("00:00:01.abc");

        // Assert
        Assert.Equal("00:00:01.abc", result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Week_Unit

    [Fact]
    public void TryGetTotalMicroseconds_Parses_Week_Unit()
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds("2 weeks", out long microseconds);

        // Assert
        Assert.True(success);
        Assert.Equal(2L * 604_800L * 1_000_000L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Negative_Day_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Negative_Day_Returns_False()
    {
        // Arrange
        string input = "-1.00:00:00";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Negative_Minutes_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Negative_Minutes_Returns_False()
    {
        // Arrange
        string input = "00:-01:00";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Minutes_60_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Minutes_60_Returns_False()
    {
        // Arrange
        string input = "00:60:00";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Empty_Fraction_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Empty_Fraction_Returns_False()
    {
        // Arrange
        string input = "00:00:30.";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_NonDigit_Fraction_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_NonDigit_Fraction_Returns_False()
    {
        // Arrange
        string input = "00:00:30.abc";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Seconds_60_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Seconds_60_Returns_False()
    {
        // Arrange
        string input = "00:00:60";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_Negative_Seconds_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Negative_Seconds_Returns_False()
    {
        // Arrange
        string input = "00:00:-1";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_DayDot_Hours_Over23_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_DayDot_Hours_Over23_Returns_False()
    {
        // Arrange
        string input = "1.25:00:00";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region NormalizeInterval_ThreeMinutes_Returns_3_Minutes

    [Fact]
    public void NormalizeInterval_ThreeMinutes_Returns_3_Minutes()
    {
        // Arrange
        string input = "00:03:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("3 minutes", result);
    }

    #endregion

    #region NormalizeInterval_FractionPresent_Returns_Unchanged

    [Fact]
    public void NormalizeInterval_FractionPresent_Returns_Unchanged()
    {
        // Arrange
        string input = "00:00:01.500000";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("00:00:01.500000", result);
    }

    #endregion

    // ── TryParseTimeParts edge cases ────────────────────────────────────────

    #region TryParseTimeParts_Returns_False_For_Negative_Hours_Without_Day_Dot

    [Fact]
    public void TryParseTimeParts_Returns_False_For_Negative_Hours_Without_Day_Dot()
    {
        // Arrange
        string input = "-01:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("-01:00:00", result);
    }

    #endregion

    #region TryParseTimeParts_Returns_False_For_NonParseable_Hour

    [Fact]
    public void TryParseTimeParts_Returns_False_For_NonParseable_Hour()
    {
        // Arrange
        string input = "XX:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("XX:00:00", result);
    }

    #endregion

    #region TryParseTimeParts_Returns_False_For_NonParseable_Day_Part

    [Fact]
    public void TryParseTimeParts_Returns_False_For_NonParseable_Day_Part()
    {
        // Arrange
        string input = "X.12:00:00";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("X.12:00:00", result);
    }

    #endregion

    #region TryParseTimeParts_Handles_Fraction_Longer_Than_Six_Digits

    [Fact]
    public void TryParseTimeParts_Handles_Fraction_Longer_Than_Six_Digits()
    {
        // Arrange
        string input = "00:00:01.123456789";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("00:00:01.123456789", result);
    }

    #endregion

    #region TryParseTimeParts_Handles_Short_Fraction_Padded_To_Six

    [Fact]
    public void TryParseTimeParts_Handles_Short_Fraction_Padded_To_Six()
    {
        // Arrange
        string input = "00:00:01.1";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("00:00:01.1", result);
    }

    #endregion

    // ── NormalizeInterval — seconds plural / singular ────────────────────────

    #region NormalizeInterval_Single_Second_Returns_Singular

    [Fact]
    public void NormalizeInterval_Single_Second_Returns_Singular()
    {
        // Arrange
        string input = "00:00:01";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("1 second", result);
    }

    #endregion

    #region NormalizeInterval_Multiple_Seconds_Returns_Plural

    [Fact]
    public void NormalizeInterval_Multiple_Seconds_Returns_Plural()
    {
        // Arrange
        string input = "00:00:02";

        // Act
        string result = IntervalParsingHelper.NormalizeInterval(input);

        // Assert
        Assert.Equal("2 seconds", result);
    }

    #endregion

    // ── TryGetTotalMicroseconds — alternate unit spellings ───────────────────

    #region TryGetTotalMicroseconds_Parses_Microsecond_Abbreviations

    [Theory]
    [InlineData("1 us", 1L)]
    [InlineData("1 usec", 1L)]
    [InlineData("2 usecs", 2L)]
    [InlineData("3 microsecond", 3L)]
    [InlineData("4 microseconds", 4L)]
    public void TryGetTotalMicroseconds_Parses_Microsecond_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Millisecond_Abbreviations

    [Theory]
    [InlineData("1 ms", 1_000L)]
    [InlineData("2 msec", 2_000L)]
    [InlineData("3 msecs", 3_000L)]
    [InlineData("4 millisecond", 4_000L)]
    public void TryGetTotalMicroseconds_Parses_Millisecond_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Second_Abbreviations

    [Theory]
    [InlineData("1 s", 1_000_000L)]
    [InlineData("2 sec", 2_000_000L)]
    [InlineData("3 secs", 3_000_000L)]
    [InlineData("4 second", 4_000_000L)]
    public void TryGetTotalMicroseconds_Parses_Second_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Minute_Abbreviations

    [Theory]
    [InlineData("1 min", 60_000_000L)]
    [InlineData("2 mins", 120_000_000L)]
    [InlineData("3 minute", 180_000_000L)]
    public void TryGetTotalMicroseconds_Parses_Minute_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Hour_Abbreviations

    [Theory]
    [InlineData("1 h", 3_600_000_000L)]
    [InlineData("2 hr", 7_200_000_000L)]
    [InlineData("3 hrs", 10_800_000_000L)]
    [InlineData("4 hour", 14_400_000_000L)]
    public void TryGetTotalMicroseconds_Parses_Hour_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Day_Abbreviations

    [Theory]
    [InlineData("1 d", 86_400_000_000L)]
    [InlineData("2 day", 172_800_000_000L)]
    public void TryGetTotalMicroseconds_Parses_Day_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Parses_Week_Abbreviations

    [Theory]
    [InlineData("1 w", 604_800_000_000L)]
    [InlineData("2 week", 1_209_600_000_000L)]
    public void TryGetTotalMicroseconds_Parses_Week_Abbreviations(string input, long expected)
    {
        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryGetTotalMicroseconds_Whitespace_Only_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_Whitespace_Only_Returns_False()
    {
        // Arrange
        string input = "   ";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_ColonPath_Returns_False_For_Invalid_Time

    [Fact]
    public void TryGetTotalMicroseconds_ColonPath_Returns_False_For_Invalid_Time()
    {
        // Arrange
        string input = "bad:value";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion

    #region TryGetTotalMicroseconds_NumberUnit_Regex_No_Match_Returns_False

    [Fact]
    public void TryGetTotalMicroseconds_NumberUnit_Regex_No_Match_Returns_False()
    {
        // Arrange
        string input = "days 7";

        // Act
        bool success = IntervalParsingHelper.TryGetTotalMicroseconds(input, out long microseconds);

        // Assert
        Assert.False(success);
        Assert.Equal(0L, microseconds);
    }

    #endregion
}
