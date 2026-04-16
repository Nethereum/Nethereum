using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryBlockchainStoreRepositoryFactory : IBlockchainStoreRepositoryFactory
    {
        public InMemoryBlockchainStoreRepositoryFactory(InMemoryBlockchainStorageRepositoryContext context)
        {
            Context = context;
        }

        public InMemoryBlockchainStorageRepositoryContext Context { get; }

        public IAddressTransactionRepository CreateAddressTransactionRepository() => new InMemoryAddressTransactionRepository(Context.AddressTransactions);

        public IBlockRepository CreateBlockRepository() => new InMemoryBlockRepository(Context.Blocks);

        public IContractRepository CreateContractRepository() => new InMemoryContractRepository(Context.Contracts);

        public ITransactionLogRepository CreateTransactionLogRepository() => new InMemoryTransactionLogRepository(Context.TransactionLogs);

        public ITransactionRepository CreateTransactionRepository() => new InMemoryTransactionRepository(Context.Transactions);

        public ITransactionVMStackRepository CreateTransactionVmStackRepository() => new InMemoryTransactionVMStackRepository(Context.VmStacks);

        public IInternalTransactionRepository CreateInternalTransactionRepository() => new InMemoryInternalTransactionRepository(Context.InternalTransactions);

        public IReorgHandler CreateReorgHandler() => throw new System.NotSupportedException(
            "In-memory factory has no default reorg handler. Hosts that need reorg handling should wire a real implementation.");

        public IBlockProgressRepository CreateInternalTransactionBlockProgressRepository() => throw new System.NotSupportedException(
            "In-memory factory has no default block-progress repository. Hosts should provide a real implementation (e.g. the EF-Core one, or a simple file-backed helper).");
    }
}
