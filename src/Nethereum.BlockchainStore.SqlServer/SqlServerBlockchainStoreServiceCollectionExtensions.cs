using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.SqlServer
{
    public static class SqlServerBlockchainStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlServerBlockchainStorage(
            this IServiceCollection services, string connectionString, string schema = null)
        {
            services.AddSingleton<IBlockchainDbContextFactory>(
                _ => new SqlServerBlockchainDbContextFactory(connectionString, schema));
            services.AddBlockchainRepositories();

            return services;
        }
    }
}
