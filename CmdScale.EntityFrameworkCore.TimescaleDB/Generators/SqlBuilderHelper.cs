using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    public class SqlBuilderHelper(string quoteString)
    {
        private readonly string quoteString = quoteString;

        public static void BuildQueryString(List<string> statements, MigrationCommandListBuilder builder)
        {
            foreach (string statement in statements)
            {
                builder
                    .Append(statement)
                    .EndCommand();
            }
        }

        public static void BuildQueryString(List<string> statements, IndentedStringBuilder builder)
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
                builder.Append("\")");
            }
        }

        public string Regclass(string tableName)
        {
            return $"'{quoteString}{tableName}{quoteString}'";
        }

        public string QualifiedIdentifier(string tableName)
        {
            return $"{quoteString}{tableName}{quoteString}";
        }
    }
}
