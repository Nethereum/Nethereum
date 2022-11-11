#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing
{
    public class BlockchainCrawlingProcessor : BlockchainProcessor
    {
        public BlockCrawlOrchestrator Orchestrator => (BlockCrawlOrchestrator)BlockchainProcessingOrchestrator;
        public BlockchainCrawlingProcessor(BlockCrawlOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService, ILogger log = null):base(blockchainProcessingOrchestrator, blockProgressRepository, lastConfirmedBlockNumberService, log)
        {
            
        }
    }
}