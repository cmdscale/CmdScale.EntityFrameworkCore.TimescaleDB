using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Storage;

#pragma warning disable EF1001 // CSharpHelper is the documented base for provider literal rendering.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Extends the built-in C# helper to render <see cref="NameOfCodeFragment"/> arguments as
    /// <c>nameof(Property)</c> references (or constant interpolated strings when a suffix follows)
    /// instead of quoted string literals, keeping scaffolded attributes rename-safe.
    /// </summary>
    public class TimescaleCSharpHelper(ITypeMappingSource typeMappingSource) : CSharpHelper(typeMappingSource)
    {
        public override string UnknownLiteral(object? value) => value switch
        {
            NameOfCodeFragment nameOf => Literal(nameOf),
            object?[] array when Array.Exists(array, entry => entry is NameOfCodeFragment) =>
                $"new[] {{ {string.Join(", ", array.Select(UnknownLiteral))} }}",
            _ => base.UnknownLiteral(value),
        };

        private static string Literal(NameOfCodeFragment nameOf) => nameOf.Suffix.Length == 0
            ? $"nameof({nameOf.PropertyName})"
            : $"$\"{{nameof({nameOf.PropertyName})}}{nameOf.Suffix}\"";
    }
}
#pragma warning restore EF1001
