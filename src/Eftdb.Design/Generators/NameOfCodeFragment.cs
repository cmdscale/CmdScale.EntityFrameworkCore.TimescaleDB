namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Marks an attribute argument as a property reference so <c>TimescaleCSharpHelper</c> renders it as
    /// <c>nameof(Property)</c> - or as a constant interpolated string like <c>$"{nameof(Property)} DESC"</c>
    /// when a suffix is present - instead of a quoted string literal. Keeps scaffolded attributes
    /// rename-safe for users who evolve the generated entities by hand.
    /// </summary>
    /// <param name="PropertyName">The CLR property the argument references.</param>
    /// <param name="Suffix">Trailing literal text (e.g. <c>" DESC"</c>); empty for a plain reference.</param>
    internal sealed record NameOfCodeFragment(string PropertyName, string Suffix = "");
}
