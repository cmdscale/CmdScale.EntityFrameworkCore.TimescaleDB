using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

/// <summary>
/// Tests that verify <c>TimescaleCSharpHelper.UnknownLiteral</c> renders <see cref="NameOfCodeFragment"/>
/// values and mixed arrays correctly, while delegating other types to the base class.
/// </summary>
public class TimescaleCSharpHelperTests
{
    private readonly ICSharpHelper _code = DesignTimeHelper.CreateRealCSharpHelper();

    #region UnknownLiteral_NameOfCodeFragment_NoSuffix_RendersAsNameof

    [Fact]
    public void UnknownLiteral_NameOfCodeFragment_NoSuffix_RendersAsNameof()
    {
        NameOfCodeFragment fragment = new("Timestamp");

        string result = _code.UnknownLiteral(fragment);

        Assert.Equal("nameof(Timestamp)", result);
    }

    #endregion

    #region UnknownLiteral_NameOfCodeFragment_WithSuffix_RendersAsInterpolatedString

    [Fact]
    public void UnknownLiteral_NameOfCodeFragment_WithSuffix_RendersAsInterpolatedString()
    {
        NameOfCodeFragment fragment = new("Timestamp", " DESC");

        string result = _code.UnknownLiteral(fragment);

        Assert.Equal("$\"{nameof(Timestamp)} DESC\"", result);
    }

    #endregion

    #region UnknownLiteral_MixedArray_RendersAsNewArrayExpression

    [Fact]
    public void UnknownLiteral_MixedArray_RendersAsNewArrayExpression()
    {
        object?[] array = [new NameOfCodeFragment("MyProp"), "literal"];

        string result = _code.UnknownLiteral(array);

        Assert.StartsWith("new[] {", result);
        Assert.Contains("nameof(MyProp)", result);
        Assert.Contains("\"literal\"", result);
    }

    #endregion

    #region UnknownLiteral_PlainString_Passthrough

    [Fact]
    public void UnknownLiteral_PlainString_Passthrough()
    {
        string result = _code.UnknownLiteral("hello");

        Assert.Equal("\"hello\"", result);
    }

    #endregion

    #region UnknownLiteral_Integer_Passthrough

    [Fact]
    public void UnknownLiteral_Integer_Passthrough()
    {
        string result = _code.UnknownLiteral(42);

        Assert.Equal("42", result);
    }

    #endregion

    #region UnknownLiteral_Null_Passthrough

    [Fact]
    public void UnknownLiteral_Null_Passthrough()
    {
        string result = _code.UnknownLiteral(null);

        Assert.Equal("null", result);
    }

    #endregion

    #region UnknownLiteral_AllNameOfCodeFragment_Array_RendersAsNewArrayExpression

    [Fact]
    public void UnknownLiteral_AllNameOfCodeFragment_Array_RendersAsNewArrayExpression()
    {
        object?[] array = [new NameOfCodeFragment("PropA"), new NameOfCodeFragment("PropB")];

        string result = _code.UnknownLiteral(array);

        Assert.StartsWith("new[] {", result);
        Assert.Contains("nameof(PropA)", result);
        Assert.Contains("nameof(PropB)", result);
    }

    #endregion
}
