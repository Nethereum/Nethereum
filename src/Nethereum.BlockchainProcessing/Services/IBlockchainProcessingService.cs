using System;
using Common.Logging;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainProcessing.Services
{
    public interface IBlockchainProcessingService
    {
        IBlockchainLogProcessingService Logs { get; }
        IBlockchainBlockProcessingService Blocks { get; }
    }

    public interface IBlockchainBlockProcessingService
    {
        BlockchainProcessor CreateBlockProcessor(
            Action<BlockProcessingSteps> stepsConfiguration,
            ILog log = null);

        BlockchainProcessor CreateBlockProcessor(
            Action<BlockProcessingSteps> stepsConfiguration,
            uint minimumBlockConfirmations,
            ILog log = null);

        BlockchainProcessor CreateBlockProcessor(
            IBlockProgressRepository blockProgressRepository,
            Action<BlockProcessingSteps> stepsConfiguration,
            ILog log = null);

        BlockchainProcessor CreateBlockProcessor(
            IBlockProgressRepository blockProgressRepository,
            Action<BlockProcessingSteps> stepsConfiguration,
            uint minimumBlockConfirmations,
            ILog log = null);

        BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            Action<BlockProcessingSteps> configureSteps = null,
            ILog log = null);

        BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            uint minimumBlockConfirmations,
            Action<BlockProcessingSteps> configureSteps = null,
            ILog log = null);

        BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            IBlockProgressRepository blockProgressRepository,
            Action<BlockProcessingSteps> configureSteps = null,
            ILog log = null);

        BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            IBlockProgressRepository blockProgressRepository,
            uint minimumBlockConfirmations,
            Action<BlockProcessingSteps> configureSteps = null,
            ILog log = null);
    }
}