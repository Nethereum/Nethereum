using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.Storage.Repositories;
using Nethereum.BlockchainProcessing.Storage.StorageStepsHandlers;

namespace Nethereum.BlockchainProcessing.Storage
{
    public class StorageBlockProcessingSteps: BlockProcessingSteps
    {
        public StorageBlockProcessingSteps(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            AddBlockStepStorageHandler(repositoryFactory);
            AddContractCreationStepStorageHandler(repositoryFactory);
            AddTransactionReceiptStepStorageHandler(repositoryFactory);
            AddFilterLogStepStorageHandler(repositoryFactory);
        }

        protected virtual void AddBlockStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new BlockStepStorageHandler(repositoryFactory.CreateBlockRepository());
            this.BlockStep.AddProcessorHandler(handler);
        }

        protected virtual void AddContractCreationStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new ContractCreationStorageStepHandler(repositoryFactory.CreateContractRepository());
            this.ContractCreationStep.AddProcessorHandler(handler);
        }

        protected virtual void AddTransactionReceiptStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new TransactionReceiptStepStorageHandler(repositoryFactory.CreateTransactionRepository(), repositoryFactory.CreateAddressTransactionRepository());
            this.TransactionReceiptStep.AddProcessorHandler(handler);
        }

        protected virtual void AddFilterLogStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new FilterLogStepStorageHandler(repositoryFactory.CreateTransactionLogRepository());
            this.FilterLogStep.AddProcessorHandler(handler);
        }
    }
}
