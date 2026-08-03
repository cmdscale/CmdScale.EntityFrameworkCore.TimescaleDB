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
            IEnumerable<string> lines = sql.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => line.Trim())
                           .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", lines);
        }
    }
}
