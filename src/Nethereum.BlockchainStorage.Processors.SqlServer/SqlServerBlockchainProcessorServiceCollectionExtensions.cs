using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.BlockchainStore.SqlServer;

namespace Nethereum.BlockchainStorage.Processors.SqlServer
{
    public static class SqlServerBlockchainProcessorServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlServerBlockchainProcessor(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null,
            string? schema = null)
        {
            var configurationRoot = configuration as IConfigurationRoot;
            var resolvedConnectionString = connectionString
                ?? configuration.GetConnectionString("SqlServerConnection")
                ?? configurationRoot?.GetBlockchainStorageConnectionString()
                ?? configuration.GetConnectionString("BlockchainDbStorage");

            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:SqlServerConnection configuration value.");
            }

            services.AddSqlServerBlockchainStorage(resolvedConnectionString, schema);
            services.AddBlockchainProcessingOptions(configuration);
            services.AddBlockchainProcessor();

            return services;
        }

        public static IServiceCollection AddSqlServerInternalTransactionProcessor(
            this IServiceCollection services)
        {
            services.AddInternalTransactionProcessor();
            return services;
        }
    }
}
