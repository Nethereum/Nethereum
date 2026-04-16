using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IBlockchainStoreRepositoryFactory
    {
        IAddressTransactionRepository CreateAddressTransactionRepository();
        IBlockRepository CreateBlockRepository();
        IContractRepository CreateContractRepository();
        ITransactionLogRepository CreateTransactionLogRepository();
        ITransactionRepository CreateTransactionRepository();
        ITransactionVMStackRepository CreateTransactionVmStackRepository();
        IInternalTransactionRepository CreateInternalTransactionRepository();
        IReorgHandler CreateReorgHandler();
        IBlockProgressRepository CreateInternalTransactionBlockProgressRepository();
    }
}
