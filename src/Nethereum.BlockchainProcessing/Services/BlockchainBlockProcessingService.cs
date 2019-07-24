using System;
using Common.Logging;
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
            uint? minimumBlockConfirmations = null,
            uint? lastBlockNumberProcessed = null,
            ILog log = null)
        {
            var processingSteps = new BlockProcessingSteps();
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });
            var progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed: lastBlockNumberProcessed);

            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber, minimumBlockConfirmations);

            stepsConfiguration?.Invoke(processingSteps);
            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService, log);
        }

        public BlockchainProcessor CreateBlockProcessor(
            IBlockProgressRepository blockProgressRepository,
            Action<BlockProcessingSteps> stepsConfiguration = null,
            uint? minimumBlockConfirmations = null,
            ILog log = null)
        {
            var processingSteps = new BlockProcessingSteps();
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });
            var progressRepository = blockProgressRepository ?? new InMemoryBlockchainProgressRepository(lastBlockProcessed: null);

            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber, minimumBlockConfirmations);

            stepsConfiguration?.Invoke(processingSteps);
            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService, log);
        }

        public BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            Action<BlockProcessingSteps> configureSteps = null,
            uint? minimumBlockConfirmations = null,
            uint? lastBlockNumberProcessed = null,
            ILog log = null)
        {
            var processingSteps = new BlockStorageProcessingSteps(blockchainStorageFactory);
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });

            IBlockProgressRepository blockProgressRepository = null;

            if (blockchainStorageFactory is IBlockProgressRepositoryFactory progressRepoFactory)
            {
                blockProgressRepository = progressRepoFactory.CreateBlockProgressRepository();
            }

            return CreateBlockProcessor(configureSteps, blockProgressRepository, processingSteps, minimumBlockConfirmations, lastBlockNumberProcessed, log);
        }

        public BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            IBlockProgressRepository blockProgressRepository,
            Action<BlockProcessingSteps> configureSteps = null,
            uint? minimumBlockConfirmations = null,
            ILog log = null)
        {
            var processingSteps = new BlockStorageProcessingSteps(blockchainStorageFactory);
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });

            if (blockProgressRepository == null && blockchainStorageFactory is IBlockProgressRepositoryFactory progressRepoFactory)
            {
                blockProgressRepository = progressRepoFactory.CreateBlockProgressRepository();
            }

            return CreateBlockProcessor(configureSteps, blockProgressRepository, processingSteps, minimumBlockConfirmations, lastBlockNumberProcessed: null, log);
        }

        private BlockchainProcessor CreateBlockProcessor(Action<BlockProcessingSteps> configureSteps, IBlockProgressRepository blockProgressRepository, BlockProcessingSteps processingSteps, uint? minimumBlockConfirmations, uint? lastBlockNumberProcessed, ILog log)
        {
            var orchestrator = new BlockCrawlOrchestrator(_ethApiContractService, new[] { processingSteps });
            var progressRepository = blockProgressRepository ?? new InMemoryBlockchainProgressRepository(lastBlockProcessed: lastBlockNumberProcessed);
            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber, minimumBlockConfirmations);
            configureSteps?.Invoke(processingSteps);

            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService, log);
        }
#endif
    }
}