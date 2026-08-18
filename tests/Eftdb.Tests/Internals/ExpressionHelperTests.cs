using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests that verify ExpressionHelper.GetPropertyName extracts the correct dot-separated path
/// for simple properties, complex-type chains, and boxed expressions, and rejects non-parameter-rooted
/// expressions with an ArgumentException.
/// </summary>
public class ExpressionHelperTests
{
    // ── Single-level ──

    #region Should_Return_Property_Name_For_Single_Level_Access

    private class SingleLevelEntity
    {
        public DateTime Value { get; set; }
    }

    [Fact]
    public void Should_Return_Property_Name_For_Single_Level_Access()
    {
        // Arrange
        Expression<Func<SingleLevelEntity, DateTime>> expr = x => x.Value;

        // Act
        string result = ExpressionHelper.GetPropertyName(expr);

        // Assert
        Assert.Equal("Value", result);
    }

    #endregion

    // ── Two-level chain ──

    #region Should_Return_Dot_Separated_Path_For_Two_Level_Chain

    private class Param1Complex
    {
        public double Value { get; set; }
    }

    private class TwoLevelEntity
    {
        public Param1Complex Param1 { get; set; } = new();
    }

    [Fact]
    public void Should_Return_Dot_Separated_Path_For_Two_Level_Chain()
    {
        // Arrange
        Expression<Func<TwoLevelEntity, double>> expr = x => x.Param1.Value;

        // Act
        string result = ExpressionHelper.GetPropertyName(expr);

        // Assert
        Assert.Equal("Param1.Value", result);
    }

    #endregion

    // ── Three-level chain (complex within complex) ──

    #region Should_Return_Dot_Separated_Path_For_Three_Level_Chain

    private class InnerComplex
    {
        public double Value { get; set; }
    }

    private class OuterComplex
    {
        public InnerComplex Inner { get; set; } = new();
    }

    private class ThreeLevelEntity
    {
        public OuterComplex Outer { get; set; } = new();
    }

    [Fact]
    public void Should_Return_Dot_Separated_Path_For_Three_Level_Chain()
    {
        // Arrange
        Expression<Func<ThreeLevelEntity, double>> expr = x => x.Outer.Inner.Value;

        // Act
        string result = ExpressionHelper.GetPropertyName(expr);

        // Assert
        Assert.Equal("Outer.Inner.Value", result);
    }

    #endregion

    // ── Boxed two-level chain (object-typed selector) ──

    #region Should_Return_Dot_Separated_Path_For_Boxed_Two_Level_Chain

    private class BoxedParam1Complex
    {
        public double Value { get; set; }
    }

    private class BoxedTwoLevelEntity
    {
        public BoxedParam1Complex Meta { get; set; } = new();
    }

    [Fact]
    public void Should_Return_Dot_Separated_Path_For_Boxed_Two_Level_Chain()
    {
        // Arrange
        Expression<Func<BoxedTwoLevelEntity, object>> expr = x => x.Meta.Value;

        // Act
        string result = ExpressionHelper.GetPropertyName(expr);

        // Assert
        Assert.Equal("Meta.Value", result);
    }

    #endregion

    // ── Closure/variable-rooted expression throws ──

    #region Should_Throw_ArgumentException_For_Variable_Rooted_Expression

    private class ClosureEntity
    {
        public double Value { get; set; }
    }

    [Fact]
    public void Should_Throw_ArgumentException_For_Variable_Rooted_Expression()
    {
        // Arrange
        ClosureEntity captured = new();
        Expression<Func<ClosureEntity, double>> expr = _ => captured.Value;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ExpressionHelper.GetPropertyName(expr));
    }

    #endregion

    // ── Non-member expression throws ──

    #region Should_Throw_ArgumentException_For_Constant_Expression

    [Fact]
    public void Should_Throw_ArgumentException_For_Constant_Expression()
    {
        // Arrange
        Expression<Func<SingleLevelEntity, int>> expr = _ => 42;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ExpressionHelper.GetPropertyName(expr));
    }

    #endregion

    // ── Static member access throws ──

    #region Should_Throw_ArgumentException_For_Static_Member_Access

    [Fact]
    public void Should_Throw_ArgumentException_For_Static_Member_Access()
    {
        // Arrange
        Expression<Func<SingleLevelEntity, DateTime>> expr = _ => DateTime.Now;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ExpressionHelper.GetPropertyName(expr));
    }

    #endregion
}
