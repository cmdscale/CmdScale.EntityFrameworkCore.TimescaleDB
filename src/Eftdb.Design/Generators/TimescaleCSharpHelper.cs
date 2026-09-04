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
            SparseIndexSelectorCodeFragment selector => Literal(selector),
            ColumnListCodeFragment columnList => Literal(columnList),
            object?[] array when Array.Exists(array, entry => entry is NameOfCodeFragment) =>
                $"new[] {{ {string.Join(", ", array.Select(UnknownLiteral))} }}",
            _ => base.UnknownLiteral(value),
        };

        private static string Literal(NameOfCodeFragment nameOf) => nameOf.Suffix.Length == 0
            ? $"nameof({nameOf.PropertyName})"
            : $"$\"{{nameof({nameOf.PropertyName})}}{nameOf.Suffix}\"";

        private static string Literal(ColumnListCodeFragment columnList)
        {
            if (columnList.Entries.Count == 1 && columnList.Entries[0] is NameOfCodeFragment single)
            {
                return Literal(single);
            }

            string body = string.Join(", ", columnList.Entries.Select(entry => entry switch
            {
                NameOfCodeFragment nameOf => $"{{nameof({nameOf.PropertyName})}}{EscapeInterpolatedText(nameOf.Suffix)}",
                _ => EscapeInterpolatedText((string)entry),
            }));
            return $"$\"{body}\"";
        }

        private static string EscapeInterpolatedText(string text)
            => text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("{", "{{").Replace("}", "}}");

        private static string Literal(SparseIndexSelectorCodeFragment selector)
        {
            string method = selector.Kind == Abstractions.ESparseIndexType.MinMax
                ? nameof(Configuration.Hypertable.SparseIndexSelector<object>.MinMax)
                : nameof(Configuration.Hypertable.SparseIndexSelector<object>.Bloom);
            string arguments = string.Join(", ", selector.PropertyNames.Select(p => $"x => x.{p}"));
            return $"s => s.{method}({arguments})";
        }
    }
}
#pragma warning restore EF1001
