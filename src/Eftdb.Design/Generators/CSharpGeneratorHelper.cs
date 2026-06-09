using Microsoft.EntityFrameworkCore.Design;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Shared helpers for emitting C# literals into migration files.
    /// </summary>
    internal static class CSharpGeneratorHelper
    {
        /// <summary>
        /// Formats a string collection as a C# collection expression, e.g. <c>["a", "b"]</c>.
        /// </summary>
        public static string LiteralStringList(ICSharpHelper code, IReadOnlyList<string> items)
        {
            string elements = string.Join(", ", items.Select(code.Literal));
            return $"[{elements}]";
        }

        /// <summary>
        /// Formats a static method call like <c>TypeRef.Method(arg1, arg2)</c>, literalizing
        /// each argument via <see cref="ICSharpHelper.UnknownLiteral"/> so primitive types are
        /// rendered correctly without per-call <c>Literal</c> boilerplate.
        /// </summary>
        public static string StaticCall(ICSharpHelper code, string typeRef, string method, params object?[] args)
        {
            string argList = string.Join(", ", args.Select(code.UnknownLiteral));
            return $"{typeRef}.{method}({argList})";
        }
    }
}
