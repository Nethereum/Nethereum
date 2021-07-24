using Nethereum.Contracts;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.Services
{
    public class BlockchainProcessorTests : BlockchainProcessorExecutionTestBase
    {

        [Fact]
        public async Task Should_Process_Block_0_To_0()
        {
            var blockNumberZero = new BigInteger(0);

            //setup / mock out dependencies
            SetupProgressRepoForNoPriorProgress();
            SetupOrchestratorMock();
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            await _blockchainProcessor.ExecuteAsync(toBlockNumber: blockNumberZero, startAtBlockNumberIfNotProcessed: blockNumberZero);

            //assert
            Assert.Single(_blocksUpsertedInToProgressRepo, blockNumberZero);
            Assert.Single(_orchestratedBlockRanges, new BlockRange(blockNumberZero, blockNumberZero));
        }

        [Fact]
        public async Task Given_Prior_Progress_Should_Pick_Up_Where_It_Left_Off()
        {
            var lastBlockProcessed = new BigInteger(100);
            var expectedNextBlock = lastBlockProcessed + 1;

            //setup / mock out dependencies
            SetupProgressRepoForPreviousProgress(lastBlockProcessed);
            SetupOrchestratorMock(invokeCancellationTokenOnceHandled: true);
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            await _blockchainProcessor.ExecuteAsync(_cancellationTokenSource.Token);

            //assert
            Assert.Single(_blocksUpsertedInToProgressRepo, expectedNextBlock);
            Assert.Single(_orchestratedBlockRanges, new BlockRange(expectedNextBlock, expectedNextBlock));
        }

        [Fact]
        public async Task When_There_Is_No_Given_Range_Will_Run_Until_Cancellation()
        {
            var lastBlockProcessed = new BigInteger(100);
            const int numberOfIterations = 10;
            var firstExpectedBlock = lastBlockProcessed + 1;
            var lastExpectedBlock = (firstExpectedBlock + numberOfIterations) - 1;

            //setup / mock out dependencies
            SetupProgressRepoForPreviousProgress(lastBlockProcessed);
            SetupOrchestratorMock(iterationsBeforeCancellation: numberOfIterations);
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            await _blockchainProcessor.ExecuteAsync(_cancellationTokenSource.Token);

            //assert
            Assert.Equal(_blocksUpsertedInToProgressRepo.Last(), lastExpectedBlock);
            Assert.Equal(firstExpectedBlock, _orchestratedBlockRanges.First().From);
            Assert.Equal(lastExpectedBlock, _orchestratedBlockRanges.Last().To);
        }

        [Fact]
        public async Task When_Minimum_Block_Exceeds_Last_Processed_Block_The_Next_Block_Is_Min_Value()
        {
            var lastBlockProcessed = new BigInteger(100);
            var minimumBlock = new BigInteger(150);
            var expectedNextBlock = minimumBlock;

            //setup / mock out dependencies
            SetupProgressRepoForPreviousProgress(lastBlockProcessed);
            SetupOrchestratorMock(invokeCancellationTokenOnceHandled: true);
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            await _blockchainProcessor.ExecuteAsync(_cancellationTokenSource.Token, minimumBlock);

            //assert
            Assert.Single(_blocksUpsertedInToProgressRepo, expectedNextBlock);
            Assert.Single(_orchestratedBlockRanges, new BlockRange(expectedNextBlock, expectedNextBlock));
        }

        [Fact]
        public async Task When_Progress_Exceeds_Minimum_Block_Next_Block_Is_Last_Processed_Plus_One()
        {
            var lastBlockProcessed = new BigInteger(150);
            var minimumBlock = new BigInteger(100);
            var expectedNextBlock = lastBlockProcessed + 1;

            //setup / mock out dependencies
            SetupProgressRepoForPreviousProgress(lastBlockProcessed);
            SetupOrchestratorMock(invokeCancellationTokenOnceHandled: true);
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            await _blockchainProcessor.ExecuteAsync(_cancellationTokenSource.Token, minimumBlock);

            //assert
            Assert.Single(_blocksUpsertedInToProgressRepo, expectedNextBlock);
            Assert.Single(_orchestratedBlockRanges, new BlockRange(expectedNextBlock, expectedNextBlock));
        }

        [Fact]
        public async Task When_Orchestrator_Returns_An_Error_Should_Throw_And_Not_Update_Progress_Repo()
        {
            var blockNumberZero = new BigInteger(0);

            //setup / mock out dependencies
            SetupProgressRepoForNoPriorProgress();
            var mockOrchestrationError = SetupOrchestratorMockToSimulateError(new Exception("orchestration error"));
            SetupProgressRepoUpsertMock();
            SetupLastConfirmedBlockNumberMock();

            //act
            var actualException = await Assert.ThrowsAsync<Exception>(
                () => _blockchainProcessor.ExecuteAsync(
                    toBlockNumber: blockNumberZero, 
                    startAtBlockNumberIfNotProcessed: blockNumberZero));

            //assert
            Assert.Empty(_blocksUpsertedInToProgressRepo);
            Assert.Equal(mockOrchestrationError, actualException);
        }
    }
}
