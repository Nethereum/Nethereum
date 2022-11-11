
using Microsoft.Extensions.Logging;
using Moq;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing.UnitTests.Services
{
    public class BlockchainProcessorTestBase
    {
        protected readonly Mock<IBlockchainProcessingOrchestrator> _orchestratorMock;
        protected readonly Mock<IBlockProgressRepository> _progressRepoMock;
        protected readonly Mock<ILastConfirmedBlockNumberService> _lastConfirmedBlockNumberMock;
        protected readonly Mock<ILogger> _logMock;
        protected readonly BlockchainProcessor _blockchainProcessor;

        public BlockchainProcessorTestBase()
        {
            _orchestratorMock = new Mock<IBlockchainProcessingOrchestrator>();
            _progressRepoMock = new Mock<IBlockProgressRepository>();
            _lastConfirmedBlockNumberMock = new Mock<ILastConfirmedBlockNumberService>();
            _logMock = new Mock<ILogger>();
            _blockchainProcessor = new BlockchainProcessor(
                _orchestratorMock.Object, 
                _progressRepoMock.Object, 
                _lastConfirmedBlockNumberMock.Object, 
                _logMock.Object);
        }

    }
}
