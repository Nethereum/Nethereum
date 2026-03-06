using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainStore.EFCore.Repositories;

namespace Nethereum.BlockchainStore.EFCore
{
    public class BlockchainStoreRepositoryFactory : IBlockchainStoreRepositoryFactory, IBlockProgressRepositoryFactory, IChainStateRepositoryFactory
    {
        private readonly IBlockchainDbContextFactory _blockchainDbContextFactory;

        public BlockchainStoreRepositoryFactory(IBlockchainDbContextFactory contextFactory)
        {
            _blockchainDbContextFactory = contextFactory;
        }

        public IAddressTransactionRepository CreateAddressTransactionRepository() => new AddressTransactionRepository(_blockchainDbContextFactory);
        public IBlockRepository CreateBlockRepository() => new BlockRepository(_blockchainDbContextFactory);
        public IContractRepository CreateContractRepository() => new ContractRepository(_blockchainDbContextFactory);
        public ITransactionLogRepository CreateTransactionLogRepository() => new TransactionLogRepository(_blockchainDbContextFactory);
        public ITransactionVMStackRepository CreateTransactionVmStackRepository() => new TransactionVMStackRepository(_blockchainDbContextFactory);
        public ITransactionRepository CreateTransactionRepository() => new TransactionRepository(_blockchainDbContextFactory);
        public IInternalTransactionRepository CreateInternalTransactionRepository() => new InternalTransactionRepository(_blockchainDbContextFactory);

        public IBlockProgressRepository CreateBlockProgressRepository() => new BlockProgressRepository(_blockchainDbContextFactory);
        public IBlockProgressRepository CreateInternalTransactionBlockProgressRepository() => new InternalTransactionBlockProgressRepository(_blockchainDbContextFactory);
        public IChainStateRepository CreateChainStateRepository() => new ChainStateRepository(_blockchainDbContextFactory);
        public IReorgHandler CreateReorgHandler() => new EfCoreReorgHandler(_blockchainDbContextFactory);
    }
}
