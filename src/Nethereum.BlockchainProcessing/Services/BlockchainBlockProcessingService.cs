using System;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing.Services
{
    public class BlockchainBlockProcessingService : IBlockchainBlockProcessingService
    {
        private readonly IEthApiContractService _ethApiContractService;

        public BlockchainBlockProcessingService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

#if !DOTNET35
        public BlockchainProcessor CreateBlockProcessor(
            Action<BlockProcessingSteps> stepsConfiguration = null,
            IBlockProgressRepository blockProgressRepository = null)
        {
            var processingSteps = new BlockProcessingSteps();
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });
            var progressRepository = blockProgressRepository ??
                                     new InMemoryBlockchainProgressRepository(lastBlockProcessed: null);
            var lastConfirmedBlockNumberService =
                new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber);
            stepsConfiguration?.Invoke(processingSteps);
            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService);
        }


        public BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            Action<BlockProcessingSteps> configureSteps = null,
            IBlockProgressRepository blockProgressRepository = null)
        {
            var processingSteps = new BlockStorageProcessingSteps(blockchainStorageFactory);
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });

            if (blockProgressRepository == null && blockchainStorageFactory is IBlockProgressRepositoryFactory progressRepoFactory)
            {
                blockProgressRepository = progressRepoFactory.CreateBlockProgressRepository();
            }

            return CreateBlockProcessor(configureSteps, blockProgressRepository, processingSteps);
        }

        private BlockchainProcessor CreateBlockProcessor(Action<BlockProcessingSteps> configureSteps, IBlockProgressRepository blockProgressRepository, BlockProcessingSteps processingSteps)
        {
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });
            var progressRepository = blockProgressRepository ?? new InMemoryBlockchainProgressRepository(lastBlockProcessed: null);
            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber);

            configureSteps?.Invoke(processingSteps);

            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService);
        }
#endif
    }
}