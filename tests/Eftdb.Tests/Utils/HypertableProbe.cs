using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils
{
    /// <summary>
    /// Shared TimescaleDB catalog probes used by hypertable integration tests to inspect
    /// server-side state (hypertable registration, chunk interval, compression) after a migration.
    /// </summary>
    internal static class HypertableProbe
    {
        public static async Task<bool> IsHypertableAsync(DbContext context, string tableName)
        {
            NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) > 0
                FROM timescaledb_information.hypertables
                WHERE hypertable_name = @tableName;
            ";
            command.Parameters.AddWithValue("tableName", tableName);

            object? result = await command.ExecuteScalarAsync();

            if (!wasOpen)
            {
                await connection.CloseAsync();
            }

            return result is bool boolResult && boolResult;
        }

        public static async Task<string> GetChunkIntervalAsync(DbContext context, string tableName)
        {
            NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT time_interval::text
                FROM timescaledb_information.dimensions
                WHERE hypertable_name = @tableName
                  AND dimension_type = 'Time'
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("tableName", tableName);

            object? result = await command.ExecuteScalarAsync();

            if (!wasOpen)
            {
                await connection.CloseAsync();
            }

            return result?.ToString() ?? string.Empty;
        }

        public static async Task<bool> IsCompressionEnabledAsync(DbContext context, string tableName)
        {
            NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT compression_enabled
                FROM timescaledb_information.hypertables
                WHERE hypertable_name = @tableName;
            ";
            command.Parameters.AddWithValue("tableName", tableName);

            object? result = await command.ExecuteScalarAsync();

            if (!wasOpen)
            {
                await connection.CloseAsync();
            }

            return result is bool boolResult && boolResult;
        }
    }
}
