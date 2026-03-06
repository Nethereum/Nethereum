using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.BlockchainStore.Sqlite;

namespace Nethereum.BlockchainStorage.Processors.Sqlite
{
    public static class SqliteBlockchainProcessorServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteBlockchainProcessor(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null)
        {
            var configurationRoot = configuration as IConfigurationRoot;
            var resolvedConnectionString = connectionString
                ?? configuration.GetConnectionString("SqliteConnection")
                ?? configurationRoot?.GetBlockchainStorageConnectionString()
                ?? configuration.GetConnectionString("BlockchainDbStorage");

            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:SqliteConnection configuration value.");
            }

            services.AddSqliteBlockchainStorage(resolvedConnectionString);
            services.AddBlockchainProcessingOptions(configuration);
            services.AddBlockchainProcessor();

            return services;
        }

        public static IServiceCollection AddSqliteInternalTransactionProcessor(
            this IServiceCollection services)
        {
            services.AddInternalTransactionProcessor();
            return services;
        }
    }
}
