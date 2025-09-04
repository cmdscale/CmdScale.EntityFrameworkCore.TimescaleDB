using Npgsql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils
{
    public static class TimescaleConnectionHelper
    {
        public static string Host { get; } = "localhost";
        public static int Port { get; } = 5432;
        public static string Username { get; } = "test_user";
        public static string Password { get; } = "test_password";

        public static string GetConnectionString(string database)
            => new NpgsqlConnectionStringBuilder
            {
                Host = Host,
                Port = Port,
                Username = Username,
                Password = Password,
                Database = database,
                Pooling = false // Disable pooling for test isolation
            }.ToString();
    }
}
