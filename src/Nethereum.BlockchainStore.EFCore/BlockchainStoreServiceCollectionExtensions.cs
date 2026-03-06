using Microsoft.Extensions.DependencyInjection;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainStore.EFCore.Repositories;

namespace Nethereum.BlockchainStore.EFCore
{
    public static class BlockchainStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddBlockchainRepositories(this IServiceCollection services)
        {
            services.AddTransient<IBlockchainStoreRepositoryFactory, BlockchainStoreRepositoryFactory>();
            services.AddTransient<IBlockProgressRepositoryFactory, BlockchainStoreRepositoryFactory>();
            services.AddTransient<IChainStateRepositoryFactory, BlockchainStoreRepositoryFactory>();

            services.AddTransient<IBlockRepository, BlockRepository>();
            services.AddTransient<ITransactionRepository, TransactionRepository>();
            services.AddTransient<ITransactionLogRepository, TransactionLogRepository>();
            services.AddTransient<ITransactionVMStackRepository, TransactionVMStackRepository>();
            services.AddTransient<IContractRepository, ContractRepository>();
            services.AddTransient<IAddressTransactionRepository, AddressTransactionRepository>();
            services.AddTransient<IBlockProgressRepository, BlockProgressRepository>();
            services.AddTransient<IChainStateRepository, ChainStateRepository>();

            return services;
        }
    }
}
