using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    public static class MigrationBuilderSqlHelper
    {
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
    }
}
