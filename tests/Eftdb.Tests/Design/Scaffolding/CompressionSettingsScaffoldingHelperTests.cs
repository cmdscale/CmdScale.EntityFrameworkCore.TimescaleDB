using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Scaffolding;

public class CompressionSettingsScaffoldingHelperTests
{
    // ── Unquoted column, no direction → defaults to ASC ──

    #region Should_Parse_Unquoted_Column_With_No_Direction_As_Asc

    [Fact]
    public void Should_Parse_Unquoted_Column_With_No_Direction_As_Asc()
    {
        // Arrange
        string token = "ts";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("ts ASC", result);
    }

    #endregion

    // ── Unquoted column with explicit DESC ──

    #region Should_Parse_Unquoted_Column_With_Desc

    [Fact]
    public void Should_Parse_Unquoted_Column_With_Desc()
    {
        // Arrange
        string token = "ts DESC";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("ts DESC", result);
    }

    #endregion

    // ── Quoted mixed-case column with DESC ──

    #region Should_Parse_Quoted_MixedCase_Column_With_Desc

    [Fact]
    public void Should_Parse_Quoted_MixedCase_Column_With_Desc()
    {
        // Arrange
        string token = "\"Timestamp\" DESC";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("Timestamp DESC", result);
    }

    #endregion

    // ── Quoted mixed-case column with DESC NULLS LAST (non-default for DESC) ──

    #region Should_Parse_Quoted_Column_With_Desc_Nulls_Last

    [Fact]
    public void Should_Parse_Quoted_Column_With_Desc_Nulls_Last()
    {
        // Arrange
        string token = "\"Timestamp\" DESC NULLS LAST";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("Timestamp DESC NULLS LAST", result);
    }

    #endregion

    // ── Doubled-quote escape inside identifier ──

    #region Should_Unescape_Doubled_Quote_In_Identifier

    [Fact]
    public void Should_Unescape_Doubled_Quote_In_Identifier()
    {
        // Arrange
        string token = "\"It\"\"s\" DESC";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("It\"s DESC", result);
    }

    #endregion

    // ── Bare column name with no suffix at all ──

    #region Should_Parse_Bare_Column_With_No_Suffix

    [Fact]
    public void Should_Parse_Bare_Column_With_No_Suffix()
    {
        // Arrange
        string token = "value";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("value ASC", result);
    }

    #endregion

    // ── Empty string token ──

    #region Should_Parse_Empty_Token_As_Asc

    [Fact]
    public void Should_Parse_Empty_Token_As_Asc()
    {
        // Arrange
        string token = string.Empty;

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal(" ASC", result);
    }

    #endregion

    // ── Quoted column with ASC (explicit direction) ──

    #region Should_Parse_Quoted_Column_With_Explicit_Asc

    [Fact]
    public void Should_Parse_Quoted_Column_With_Explicit_Asc()
    {
        // Arrange
        string token = "\"DeviceId\" ASC";

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal("DeviceId ASC", result);
    }

    #endregion

    // ── Output matches BuildOrderByEntry for ASC column (canonical equivalence) ──

    #region Should_Produce_Same_Output_As_BuildOrderByEntry_For_Asc

    [Fact]
    public void Should_Produce_Same_Output_As_BuildOrderByEntry_For_Asc()
    {
        // Arrange
        string token = "\"MyColumn\" ASC";
        string expected = CompressionSettingsScaffoldingHelper.BuildOrderByEntry("MyColumn", isAscending: true, isNullsFirst: false);

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    // ── Output matches BuildOrderByEntry for DESC column (canonical equivalence) ──

    #region Should_Produce_Same_Output_As_BuildOrderByEntry_For_Desc

    [Fact]
    public void Should_Produce_Same_Output_As_BuildOrderByEntry_For_Desc()
    {
        // Arrange
        string token = "\"MyColumn\" DESC";
        string expected = CompressionSettingsScaffoldingHelper.BuildOrderByEntry("MyColumn", isAscending: false, isNullsFirst: true);

        // Act
        string result = CompressionSettingsScaffoldingHelper.ParseColumnstoreOrderByToken(token);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
