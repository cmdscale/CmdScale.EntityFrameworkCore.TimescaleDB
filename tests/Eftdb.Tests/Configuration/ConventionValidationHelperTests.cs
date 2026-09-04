using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify ConventionValidationHelper normalizes and parses policy InitialStart values
/// to machine-independent UTC regardless of the host time zone.
/// </summary>
public class ConventionValidationHelperTests
{
    // ── NormalizeInitialStartToUtc ─────────────────────────────────────────────

    #region NormalizeInitialStartToUtc_Utc_Returns_Unchanged

    [Fact]
    public void NormalizeInitialStartToUtc_Utc_Returns_Unchanged()
    {
        // Arrange
        DateTime value = new(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc);

        // Act
        DateTime result = ConventionValidationHelper.NormalizeInitialStartToUtc(value);

        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(value, result);
    }

    #endregion

    #region NormalizeInitialStartToUtc_Local_Converts_To_Correct_Instant

    [Fact]
    public void NormalizeInitialStartToUtc_Local_Converts_To_Correct_Instant()
    {
        // Arrange
        DateTime value = new(2025, 9, 23, 9, 15, 19, DateTimeKind.Local);
        DateTime expected = value.ToUniversalTime();

        // Act
        DateTime result = ConventionValidationHelper.NormalizeInitialStartToUtc(value);

        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(expected, result);
    }

    #endregion

    #region NormalizeInitialStartToUtc_Unspecified_Keeps_WallClock_Gains_Utc_Kind

    [Fact]
    public void NormalizeInitialStartToUtc_Unspecified_Keeps_WallClock_Gains_Utc_Kind()
    {
        // Arrange
        DateTime value = new(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified);

        // Act
        DateTime result = ConventionValidationHelper.NormalizeInitialStartToUtc(value);

        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(value.Year, result.Year);
        Assert.Equal(value.Month, result.Month);
        Assert.Equal(value.Day, result.Day);
        Assert.Equal(value.Hour, result.Hour);
        Assert.Equal(value.Minute, result.Minute);
        Assert.Equal(value.Second, result.Second);
    }

    #endregion

    #region NormalizeInitialStartToUtc_Nullable_Null_Passes_Through

    [Fact]
    public void NormalizeInitialStartToUtc_Nullable_Null_Passes_Through()
    {
        // Arrange
        DateTime? value = null;

        // Act
        DateTime? result = ConventionValidationHelper.NormalizeInitialStartToUtc(value);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region NormalizeInitialStartToUtc_Nullable_Local_Converts_To_Correct_Instant

    [Fact]
    public void NormalizeInitialStartToUtc_Nullable_Local_Converts_To_Correct_Instant()
    {
        // Arrange
        DateTime? value = new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Local);
        DateTime expected = value.Value.ToUniversalTime();

        // Act
        DateTime? result = ConventionValidationHelper.NormalizeInitialStartToUtc(value);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(expected, result.Value);
    }

    #endregion

    // ── ParseInitialStart ──────────────────────────────────────────────────────

    #region ParseInitialStart_Z_Suffix_Returns_Utc_With_Unshifted_Digits

    [Fact]
    public void ParseInitialStart_Z_Suffix_Returns_Utc_With_Unshifted_Digits()
    {
        // Arrange
        string raw = "2025-09-23T09:15:19Z";

        // Act
        DateTime? result = ConventionValidationHelper.ParseInitialStart(raw, "Entity", "[ReorderPolicy]");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), result.Value);
    }

    #endregion

    #region ParseInitialStart_Explicit_Offset_Shifts_To_Utc

    [Fact]
    public void ParseInitialStart_Explicit_Offset_Shifts_To_Utc()
    {
        // Arrange
        string raw = "2025-09-23T09:15:19+02:00";

        // Act
        DateTime? result = ConventionValidationHelper.ParseInitialStart(raw, "Entity", "[ReorderPolicy]");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(new DateTime(2025, 9, 23, 7, 15, 19, DateTimeKind.Utc), result.Value);
    }

    #endregion

    #region ParseInitialStart_Unsuffixed_Treated_As_Utc

    [Fact]
    public void ParseInitialStart_Unsuffixed_Treated_As_Utc()
    {
        // Arrange
        string raw = "2025-09-23T09:15:19";

        // Act
        DateTime? result = ConventionValidationHelper.ParseInitialStart(raw, "Entity", "[ReorderPolicy]");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), result.Value);
    }

    #endregion

    #region ParseInitialStart_Garbage_Throws_InvalidOperationException

    [Fact]
    public void ParseInitialStart_Garbage_Throws_InvalidOperationException()
    {
        // Arrange
        string raw = "not-a-date";

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConventionValidationHelper.ParseInitialStart(raw, "Entity", "[ReorderPolicy]"));
        Assert.Contains("not a valid DateTime format", ex.Message);
    }

    #endregion

    #region ParseInitialStart_Null_Returns_Null

    [Fact]
    public void ParseInitialStart_Null_Returns_Null()
    {
        // Arrange & Act
        DateTime? result = ConventionValidationHelper.ParseInitialStart(null, "Entity", "[ReorderPolicy]");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ParseInitialStart_Whitespace_Returns_Null

    [Fact]
    public void ParseInitialStart_Whitespace_Returns_Null()
    {
        // Arrange & Act
        DateTime? result = ConventionValidationHelper.ParseInitialStart("   ", "Entity", "[ReorderPolicy]");

        // Assert
        Assert.Null(result);
    }

    #endregion
}
