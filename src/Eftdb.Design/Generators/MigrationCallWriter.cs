using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Writes a typed migrationBuilder call with one named argument per line.
    /// </summary>
    internal sealed class MigrationCallWriter : IDisposable
    {
        private readonly IndentedStringBuilder builder;
        private readonly IDisposable indent;
        private bool hasArgs;

        public MigrationCallWriter(IndentedStringBuilder builder, string methodName)
        {
            this.builder = builder;
            builder.AppendLine($".{methodName}(");
            indent = builder.Indent();
        }

        /// <summary>Adds a named argument whose value is the given pre-rendered literal.</summary>
        public void Arg(string name, string renderedValue)
        {
            WriteName(name);
            builder.Append(renderedValue);
        }

        /// <summary>
        /// Adds a named argument whose value is written directly into the builder, for
        /// multi-line values.
        /// </summary>
        public void Arg(string name, Action<IndentedStringBuilder> writeValue)
        {
            WriteName(name);
            writeValue(builder);
        }

        private void WriteName(string name)
        {
            if (hasArgs)
            {
                builder.AppendLine(",");
            }

            hasArgs = true;
            builder.Append(name).Append(": ");
        }

        public void Dispose()
        {
            builder.Append(")");
            indent.Dispose();
        }
    }
}
