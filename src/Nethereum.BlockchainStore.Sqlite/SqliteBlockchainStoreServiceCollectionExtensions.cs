using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.Sqlite
{
    public static class SqliteBlockchainStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteBlockchainStorage(
            this IServiceCollection services, string connectionString)
        {
            services.AddSingleton<IBlockchainDbContextFactory>(
                _ => new SqliteBlockchainDbContextFactory(connectionString));
            services.AddBlockchainRepositories();

            return services;
        }
    }
}
