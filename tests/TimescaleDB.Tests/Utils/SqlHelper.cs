namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils
{
    internal static class SqlHelper
    {
        /// <summary>
        /// Normalizes a multi-line SQL string for comparison by trimming each line
        /// and removing empty lines, making the comparison insensitive to indentation.
        /// </summary>
        public static string NormalizeSql(string sql)
        {
            // Split into lines, trim each line, and filter out empty ones
            IEnumerable<string> lines = sql.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => line.Trim())
                           .Where(line => !string.IsNullOrWhiteSpace(line));

            // Join back with a consistent newline character
            return string.Join("\n", lines);
        }
    }
}
