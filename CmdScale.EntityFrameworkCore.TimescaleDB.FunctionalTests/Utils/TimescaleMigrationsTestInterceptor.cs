using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils
{
    public class TimescaleMigrationsTestInterceptor : DbCommandInterceptor
    {
        /// <summary>
        /// The <see cref="MigrationsInfrastructureTestBase"/> from <see cref="Microsoft.EntityFrameworkCore.Migrations"/> does not quote table names
        /// in the raw SQL queries it provides. In PostgreSQL, unquoted names are folded to lower case, while in the model they are defined in PascalCase.
        /// Further, the Bar column is defined without a type and is by default interpreted as an integer type in PostgreSQL, but the seed data provides a string value.
        /// 
        /// This results in failed migrations due to missing tables or columns, which is a false negative. To prevent this, we can intercept the SQL commands
        /// and fix the casing of the table and column names, as well as any other issues.
        /// </summary>
        private static string Fix(string commandText)
        {
            return commandText
                // Fixes the invalid string-to-integer insert.
                .Replace("' '", "0")
                // Fixes the unquoted, case-sensitive table and column names.
                .Replace("INSERT INTO Table1 (Id, Bar, Description)", "INSERT INTO \"Table1\" (\"Id\", \"Bar\", \"Description\")");
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            command.CommandText = Fix(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            command.CommandText = Fix(command.CommandText);
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            command.CommandText = Fix(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            command.CommandText = Fix(command.CommandText);
            return new ValueTask<InterceptionResult<int>>(result);
        }
    }
}
