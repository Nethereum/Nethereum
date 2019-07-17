namespace Nethereum.BlockchainProcessing.Storage.Repositories
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
    }
}
