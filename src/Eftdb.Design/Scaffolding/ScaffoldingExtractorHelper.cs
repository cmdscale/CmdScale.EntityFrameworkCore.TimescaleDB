using System.Data;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Shared infrastructure helpers for scaffolding extractors.
    /// </summary>
    internal static class ScaffoldingExtractorHelper
    {
        /// <summary>
        /// Internal schema names that are never part of a user model. Embed this fragment in
        /// <c>WHERE schema_column NOT IN (...)</c> clauses.
        /// </summary>
        internal const string TimescaleInternalSchemaExclusion =
            "'_timescaledb_internal', '_timescaledb_catalog', '_timescaledb_config', '_timescaledb_cache'";

        /// <summary>
        /// Opens <paramref name="connection"/> when it is not already open, executes
        /// <paramref name="body"/>, then closes the connection only if it was opened here.
        /// </summary>
        internal static TResult UsingConnection<TResult>(DbConnection connection, Func<TResult> body)
        {
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                return body();
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> when the view identified by
        /// <paramref name="schema"/>/<paramref name="viewName"/> exists in
        /// <c>information_schema.views</c>. Uses parameterized commands to prevent SQL injection.
        /// </summary>
        internal static bool ViewExists(DbConnection connection, string schema, string viewName)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.views
                    WHERE table_schema = @schema
                      AND table_name   = @viewName
                );";

            DbParameter schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);

            DbParameter nameParam = command.CreateParameter();
            nameParam.ParameterName = "@viewName";
            nameParam.Value = viewName;
            command.Parameters.Add(nameParam);

            object? result = command.ExecuteScalar();
            return result is true;
        }
    }
}
