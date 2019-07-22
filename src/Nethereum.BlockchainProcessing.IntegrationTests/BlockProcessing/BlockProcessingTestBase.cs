using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Blocks;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{
    public class BlockProcessingTestBase
    {
        protected Web3Mock Web3Mock;
        protected BlockProcessingSteps processingSteps;
        protected BlockCrawlOrchestrator orchestrator;
        protected InMemoryBlockchainProgressRepository progressRepository;
        protected LastConfirmedBlockNumberService lastConfirmedBlockService;
        protected Queue<HexBigInteger> blockNumberQueue;
        protected BlockingProcessingRpcMock mockRpcResponses;
        protected ProcessedBlockchainData processedBlockchainData;

        public BlockProcessingTestBase()
        {
            Web3Mock = new Web3Mock();
            processedBlockchainData = new ProcessedBlockchainData();
        }

        protected virtual void Initialise(BigInteger? lastBlockProcessed)
        {
            processingSteps = new BlockProcessingSteps();
            orchestrator = new BlockCrawlOrchestrator(Web3Mock.EthApiContractServiceMock.Object, new[] { processingSteps });
            progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed);
            lastConfirmedBlockService = new LastConfirmedBlockNumberService(Web3Mock.BlockNumberMock.Object);

            blockNumberQueue = new Queue<HexBigInteger>();

            Web3Mock
                .BlockNumberMock
                .Setup(m => m.SendRequestAsync(null))
                .Returns(() => Task.FromResult(blockNumberQueue.Dequeue()));

            mockRpcResponses = new BlockingProcessingRpcMock(Web3Mock);
        }

        protected virtual void MockGetBlockNumber(BigInteger blockNumberToReturn)
        {
            blockNumberQueue.Enqueue(new HexBigInteger(blockNumberToReturn));
        }
    }
}
