using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Marks a fluent <c>WithSparseIndex</c> argument as a typed selector so
    /// <c>TimescaleCSharpHelper</c> renders it as <c>s => s.Bloom(x => x.Property)</c> or
    /// <c>s => s.MinMax(x => x.Property)</c> instead of a raw string entry. Keeps scaffolded
    /// fluent configuration rename-safe for users who evolve the generated entities by hand.
    /// </summary>
    /// <param name="Kind">The sparse index type the selector creates.</param>
    /// <param name="PropertyNames">The CLR properties the entry references, in order.</param>
    internal sealed record SparseIndexSelectorCodeFragment(ESparseIndexType Kind, IReadOnlyList<string> PropertyNames);
}
