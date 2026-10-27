using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests placeholder parsing and formatting for continuous aggregate expression templates.
/// </summary>
public class ColumnTemplateTests
{
    #region Should_Replace_Placeholders_In_Order

    [Fact]
    public void Should_Replace_Placeholders_In_Order()
    {
        // Act
        string sql = ColumnTemplate.Format("mcv_agg(50, 2.0, {1}) FILTER (WHERE {0} = 1)", i => $"\"c{i}\"");

        // Assert
        Assert.Equal("mcv_agg(50, 2.0, \"c1\") FILTER (WHERE \"c0\" = 1)", sql);
    }

    #endregion

    #region Should_Ignore_Braces_Inside_Literals_And_Quoted_Identifiers

    [Fact]
    public void Should_Ignore_Braces_Inside_Literals_And_Quoted_Identifiers()
    {
        // Act
        string sql = ColumnTemplate.Format("coalesce({0}, '{x}', 'it''s {1}') || \"odd{name}\"", _ => "\"v\"");

        // Assert
        Assert.Equal("coalesce(\"v\", '{x}', 'it''s {1}') || \"odd{name}\"", sql);
    }

    #endregion

    #region Should_Unescape_Doubled_Braces

    [Fact]
    public void Should_Unescape_Doubled_Braces()
    {
        // Act
        string sql = ColumnTemplate.Format("ARRAY[{0}]::text[] = '{{}}' OR {{0}}", _ => "\"v\"");

        // Assert
        Assert.Equal("ARRAY[\"v\"]::text[] = '{{}}' OR {0}", sql);
    }

    #endregion

    #region Should_List_Placeholder_Indices

    [Fact]
    public void Should_List_Placeholder_Indices()
    {
        // Act
        List<int> indices = ColumnTemplate.GetPlaceholderIndices("{1} + {0} + {1} + '{2}'");

        // Assert
        Assert.Equal([1, 0, 1], indices);
    }

    #endregion

    #region Should_Throw_For_Invalid_Placeholder

    [Theory]
    [InlineData("count({column})")]
    [InlineData("count({0)")]
    [InlineData("count(0})")]
    public void Should_Throw_For_Invalid_Placeholder(string template)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => ColumnTemplate.GetPlaceholderIndices(template));
    }

    #endregion

    #region Should_Keep_Unterminated_Literal_Verbatim

    [Fact]
    public void Should_Keep_Unterminated_Literal_Verbatim()
    {
        // Act
        string sql = ColumnTemplate.Format("coalesce({0}, 'open {1}", _ => "\"v\"");

        // Assert
        Assert.Equal("coalesce(\"v\", 'open {1}", sql);
    }

    #endregion
}
