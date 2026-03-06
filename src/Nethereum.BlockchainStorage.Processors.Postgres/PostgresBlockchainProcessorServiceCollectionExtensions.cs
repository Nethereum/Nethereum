using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.BlockchainStore.Postgres;

namespace Nethereum.BlockchainStorage.Processors.Postgres
{
    public static class PostgresBlockchainProcessorServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresBlockchainProcessor(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null)
        {
            var configurationRoot = configuration as IConfigurationRoot;
            var resolvedConnectionString = connectionString
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configurationRoot?.GetBlockchainStorageConnectionString()
                ?? configuration.GetConnectionString("BlockchainDbStorage");

            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:PostgresConnection configuration value.");
            }

            services.AddPostgresBlockchainStorage(resolvedConnectionString);
            services.AddBlockchainProcessingOptions(configuration);
            services.AddBlockchainProcessor();

            return services;
        }

        public static IServiceCollection AddPostgresInternalTransactionProcessor(
            this IServiceCollection services)
        {
            services.AddInternalTransactionProcessor();
            return services;
        }
    }
}
