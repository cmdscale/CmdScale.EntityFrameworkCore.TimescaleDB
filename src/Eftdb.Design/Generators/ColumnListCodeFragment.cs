namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Represents a comma-joined column list destined for a single string parameter, where
    /// entries are <see cref="NameOfCodeFragment"/> references (rename-safe) or raw strings
    /// (unmapped columns). Rendered as <c>nameof(...)</c> for a single reference and as a
    /// constant interpolated string for mixed or multi-entry lists.
    /// </summary>
    internal sealed record ColumnListCodeFragment(IReadOnlyList<object> Entries);
}
