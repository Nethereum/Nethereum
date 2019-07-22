using System;
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
            Action<BlockProcessingSteps> stepsConfiguration = null,
            IBlockProgressRepository blockProgressRepository = null);

        BlockchainProcessor CreateBlockStorageProcessor(
            IBlockchainStoreRepositoryFactory blockchainStorageFactory,
            Action<BlockProcessingSteps> configureSteps = null,
            IBlockProgressRepository blockProgressRepository = null);
    }
}