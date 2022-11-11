using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.BlockchainProcessing.BlockStorage
{
    public class BlockStorageProcessingSteps: BlockProcessingSteps
    {
        public BlockStorageProcessingSteps(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            AddBlockStepStorageHandler(repositoryFactory);
            AddContractCreationStepStorageHandler(repositoryFactory);
            AddTransactionReceiptStepStorageHandler(repositoryFactory);
            AddFilterLogStepStorageHandler(repositoryFactory);
        }

        protected virtual void AddBlockStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new BlockStorageStepHandler(repositoryFactory.CreateBlockRepository());
            this.BlockStep.AddProcessorHandler(handler);
        }

        protected virtual void AddContractCreationStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new ContractCreationStorageStepHandler(repositoryFactory.CreateContractRepository());
            this.ContractCreationStep.AddProcessorHandler(handler);
        }

        protected virtual void AddTransactionReceiptStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new TransactionReceiptStorageStepHandler(repositoryFactory.CreateTransactionRepository(), repositoryFactory.CreateAddressTransactionRepository());
            this.TransactionReceiptStep.AddProcessorHandler(handler);
        }

        protected virtual void AddFilterLogStepStorageHandler(IBlockchainStoreRepositoryFactory repositoryFactory)
        {
            var handler = new FilterLogStorageStepHandler(repositoryFactory.CreateTransactionLogRepository());
            this.FilterLogStep.AddProcessorHandler(handler);
        }
    }
}
