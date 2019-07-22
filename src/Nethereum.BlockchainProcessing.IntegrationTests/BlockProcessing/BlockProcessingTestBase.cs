using Moq;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.Utils;
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
        protected BlockingProcessingRpcMock mockRpcResponses;
        protected ProcessedBlockchainData processedBlockchainData;
        protected Mock<IWaitStrategy> waitForBlockStrategyMock;

        protected int WaitForBlockOccurances;

        public BlockProcessingTestBase()
        {
            Web3Mock = new Web3Mock();
            processedBlockchainData = new ProcessedBlockchainData();
            waitForBlockStrategyMock = new Mock<IWaitStrategy>();
            waitForBlockStrategyMock.Setup(m => m.Apply(It.IsAny<uint>())).Returns(() =>
            {
                WaitForBlockOccurances ++;
                return Task.CompletedTask;
            });

        }

        protected virtual void Initialise(
            BigInteger? lastBlockProcessed = null, 
            uint minimumBlockConfirmations = LastConfirmedBlockNumberService.DEFAULT_BLOCK_CONFIRMATIONS)
        {
            processingSteps = new BlockProcessingSteps();
            orchestrator = new BlockCrawlOrchestrator(Web3Mock.EthApiContractServiceMock.Object, new[] { processingSteps });
            progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed);
            lastConfirmedBlockService = new LastConfirmedBlockNumberService(Web3Mock.BlockNumberMock.Object, waitForBlockStrategyMock.Object, minimumBlockConfirmations);

            mockRpcResponses = new BlockingProcessingRpcMock(Web3Mock);
        }
    }
}
