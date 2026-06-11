using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests that verify TimeColumnStoreTypeValidator accepts the PostgreSQL store types valid as a
/// TimescaleDB time dimension and rejects everything else, including normalization of length/precision
/// qualifiers and null or blank input.
/// </summary>
public class TimeColumnStoreTypeValidatorTests
{
    [Theory]
    [InlineData("timestamp without time zone")]
    [InlineData("timestamp with time zone")]
    [InlineData("timestamp")]
    [InlineData("timestamptz")]
    [InlineData("date")]
    [InlineData("smallint")]
    [InlineData("int2")]
    [InlineData("integer")]
    [InlineData("int")]
    [InlineData("int4")]
    [InlineData("bigint")]
    [InlineData("int8")]
    [InlineData("TIMESTAMPTZ")]
    [InlineData("  timestamptz  ")]
    [InlineData("timestamp(6) with time zone")]
    [InlineData("timestamp(3)")]
    public void IsValid_Returns_True_For_Valid_Store_Types(string storeType)
    {
        Assert.True(TimeColumnStoreTypeValidator.IsValid(storeType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("boolean")]
    [InlineData("uuid")]
    [InlineData("text")]
    [InlineData("time without time zone")]
    [InlineData("interval")]
    [InlineData("numeric(10,2)")]
    [InlineData("timestamp(")]
    public void IsValid_Returns_False_For_Invalid_Store_Types(string? storeType)
    {
        Assert.False(TimeColumnStoreTypeValidator.IsValid(storeType));
    }
}
