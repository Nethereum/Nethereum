using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.BlockchainProcessing.Token.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.BlockchainProcessing.Token.IntegrationTests.Tests
{
    [Collection("TokenPipeline")]
    public class ContinuousProcessorTests
    {
        private readonly TokenPipelineFixture _fixture;

        public ContinuousProcessorTests(TokenPipelineFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_TransferLogs_When_ProcessorExecuted_Then_ProgressUpdated()
        {
            var transferLogRepo = new InMemoryTokenTransferLogRepository();
            var progressRepo = new InMemoryBlockchainProgressRepository();

            var logProcessing = new BlockchainLogProcessingService(_fixture.Web3.Eth);
            var transferService = new TokenTransferLogProcessingService(logProcessing, _fixture.Web3.Eth);

            var currentBlock = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            var processor = transferService.CreateProcessor(
                transferLogRepo,
                progressRepo,
                numberOfBlocksPerRequest: 100);

            await processor.ExecuteAsync(
                cancellationToken: CancellationToken.None,
                toBlockNumber: currentBlock.Value);

            Assert.True(transferLogRepo.Records.Count > 0);
            Assert.NotNull(progressRepo.LastBlockProcessed);
            Assert.True(progressRepo.LastBlockProcessed >= currentBlock.Value);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ProcessorRunTwice_When_NoNewBlocks_Then_NoNewLogs()
        {
            var transferLogRepo = new InMemoryTokenTransferLogRepository();
            var progressRepo = new InMemoryBlockchainProgressRepository();

            var logProcessing = new BlockchainLogProcessingService(_fixture.Web3.Eth);
            var transferService = new TokenTransferLogProcessingService(logProcessing, _fixture.Web3.Eth);

            var currentBlock = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            var processor = transferService.CreateProcessor(
                transferLogRepo,
                progressRepo,
                numberOfBlocksPerRequest: 100);

            await processor.ExecuteAsync(
                cancellationToken: CancellationToken.None,
                toBlockNumber: currentBlock.Value);

            var countAfterFirstRun = transferLogRepo.Records.Count;
            Assert.True(countAfterFirstRun > 0);

            // Run again — same block range, should pick up from checkpoint
            var processor2 = transferService.CreateProcessor(
                transferLogRepo,
                progressRepo,
                numberOfBlocksPerRequest: 100);

            await processor2.ExecuteAsync(
                cancellationToken: CancellationToken.None,
                toBlockNumber: currentBlock.Value);

            Assert.Equal(countAfterFirstRun, transferLogRepo.Records.Count);
        }
    }
}
