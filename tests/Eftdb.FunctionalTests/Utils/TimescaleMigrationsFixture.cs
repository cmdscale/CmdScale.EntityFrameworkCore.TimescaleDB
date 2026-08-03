using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils
{
    public class TimescaleMigrationsFixture : MigrationsInfrastructureFixtureBase, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("timescale/timescaledb:latest-pg17")
            .WithDatabase("migration_tests_db")
            .WithUsername(TimescaleConnectionHelper.Username)
            .WithPassword(TimescaleConnectionHelper.Password)
            .Build();

        public string ConnectionString => _dbContainer.GetConnectionString();

        public override async Task InitializeAsync()
        {
            await _dbContainer.StartAsync();
            TimescaleTestStoreFactory.ConnectionString = ConnectionString;
            await base.InitializeAsync();
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await _dbContainer.StopAsync();
        }

        protected override ITestStoreFactory TestStoreFactory => TimescaleTestStoreFactory.Instance;
    }
}
