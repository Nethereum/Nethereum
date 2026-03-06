using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;
using Microsoft.EntityFrameworkCore;

namespace Nethereum.BlockchainStore.Postgres
{
    public static class PostgresBlockchainStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresBlockchainStorage(
            this IServiceCollection services, string connectionString)
        {
            services.AddSingleton<IBlockchainDbContextFactory>(
                _ => new PostgresBlockchainDbContextFactory(connectionString));
            services.AddBlockchainRepositories();

            return services;
        }
    }
}
