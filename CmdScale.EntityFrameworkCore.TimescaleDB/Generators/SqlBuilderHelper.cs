using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class SqlBuilderHelper(string quoteString)
    {
        private readonly string quoteString = quoteString;

        public static void BuildQueryString(List<string> statements, MigrationCommandListBuilder builder, bool suppressTransaction = false)
        {
            if (statements.Count == 0)
            {
                return;
            }

            // Group consecutive statements that don't end with semicolon into single commands
            List<List<string>> commandGroups = [];
            List<string> currentGroup = [];

            foreach (string statement in statements)
            {
                currentGroup.Add(statement);

                // If statement ends with semicolon, it's a complete command
                if (statement.TrimEnd().EndsWith(';'))
                {
                    commandGroups.Add([.. currentGroup]);
                    currentGroup.Clear();
                }
            }

            // Add any remaining statements as a final command
            if (currentGroup.Count > 0)
            {
                commandGroups.Add([.. currentGroup]);
            }

            // Build each command group
            foreach (List<string> group in commandGroups)
            {
                string command = string.Join("\n", group);
                builder
                    .Append(command)
                    .EndCommand(suppressTransaction: suppressTransaction);
            }
        }

        public static void BuildQueryString(List<string> statements, IndentedStringBuilder builder, bool suppressTransaction = false)
        {
            if (statements.Count > 0)
            {
                builder.AppendLine(".Sql(@\"");
                using (builder.Indent())
                {
                    foreach (string statement in statements)
                    {
                        builder.AppendLine(statement);
                    }
                }
                if (suppressTransaction)
                {
                    builder.Append("\", suppressTransaction: true)");
                }
                else
                {
                    builder.Append("\")");
                }
            }
        }

        public string Regclass(string tableName, string schema = DefaultValues.DefaultSchema)
        {
            return $"'{schema}.{quoteString}{tableName}{quoteString}'";
        }

        public string QualifiedIdentifier(string tableName, string schema = DefaultValues.DefaultSchema)
        {
            return $"{quoteString}{schema}{quoteString}.{quoteString}{tableName}{quoteString}";
        }
    }
}
