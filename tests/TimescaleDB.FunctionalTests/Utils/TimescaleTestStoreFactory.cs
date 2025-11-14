using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils
{
    public class TimescaleTestStoreFactory : RelationalTestStoreFactory
    {
        public static TimescaleTestStoreFactory Instance { get; } = new();
        public static string ConnectionString { get; set; } = string.Empty;

        public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        {
            return serviceCollection.AddEntityFrameworkNpgsql().AddEntityFrameworkTimescaleDb();
        }

        public override TestStore Create(string storeName)
            => TimescaleTestStore.Create(storeName, ConnectionString);

        public override TestStore GetOrCreate(string storeName)
            => TimescaleTestStore.GetOrCreateShared(storeName, ConnectionString);

        public override ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
            => new TestSqlLoggerFactory(shouldLogCategory);
    }
}
