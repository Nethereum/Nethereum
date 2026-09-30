using System.Threading.Tasks;
using Nethereum.EVM.Core.Tests.GeneralStateTests;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class AsyncRequestSystemCallTests
    {
        private static Task<Nethereum.EVM.Execution.BlockExecutionResult> RunAsync(HardforkName fork)
            => RunAsync(SystemCallExpectations.BlockAt(fork));

        private static async Task<Nethereum.EVM.Execution.BlockExecutionResult> RunAsync(
            Nethereum.EVM.Witness.BlockWitnessData block)
        {
            return await Nethereum.EVM.Execution.BlockExecutor.ExecuteAsync(
                block,
                RlpBlockEncodingProvider.Instance,
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance);
        }

        private static Task<System.Exception> AttemptAsync(Nethereum.EVM.Witness.BlockWitnessData block)
            => Record.ExceptionAsync(() => RunAsync(block));

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_AnAmsterdamBlock_When_ExecutedAsynchronously_Then_AllFourRequestContractsAreCalled()
            => SystemCallExpectations.AssertAmsterdamCallsAllFour(await RunAsync(HardforkName.Amsterdam));

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_APragueBlock_When_ExecutedAsynchronously_Then_OnlyTheTwoPragueContractsAreCalled()
            => SystemCallExpectations.AssertPragueCallsOnlyTheTwoPragueContracts(await RunAsync(HardforkName.Prague));

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_ACancunBlock_When_ExecutedAsynchronously_Then_NoRequestContractIsCalled()
            => SystemCallExpectations.AssertCancunCallsNone(await RunAsync(HardforkName.Cancun));

        [Fact]
        [Trait("Category", "EIP7928")]
        public async Task Given_AnAmsterdamBlock_When_ExecutedAsynchronously_Then_TheRequestContractsAreIndexedAfterTheTransactions()
            => SystemCallExpectations.AssertRequestsIndexedAfterTransactions(
                await RunAsync(HardforkName.Amsterdam), transactionCount: 0);

        [Theory]
        [Trait("Category", "EIP8282")]
        [InlineData(SystemCallExpectations.WithdrawalRequests)]
        [InlineData(SystemCallExpectations.Consolidations)]
        [InlineData(SystemCallExpectations.BuilderDeposit)]
        [InlineData(SystemCallExpectations.BuilderExit)]
        public async Task Given_ARequestPredeployThatReverts_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed(
            string contractAddress)
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                await AttemptAsync(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    contractAddress, SystemCallExpectations.Reverts())),
                contractAddress);

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_ARequestPredeployThatRunsOutOfGas_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                await AttemptAsync(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    SystemCallExpectations.BuilderDeposit, SystemCallExpectations.RunsOutOfGas())),
                SystemCallExpectations.BuilderDeposit);

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_ARequestPredeployThatHitsAnInvalidOpcode_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                await AttemptAsync(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    SystemCallExpectations.BuilderDeposit, SystemCallExpectations.Throws())),
                SystemCallExpectations.BuilderDeposit);

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_EveryRequestPredeploySucceeds_When_TheBlockIsExecuted_Then_TheBlockIsValid()
            => SystemCallExpectations.AssertAccepted(
                await AttemptAsync(SystemCallExpectations.BlockAt(HardforkName.Amsterdam)));

        [Fact]
        [Trait("Category", "EIP4788")]
        public async Task Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
            => SystemCallExpectations.AssertAccepted(
                await AttemptAsync(SystemCallExpectations.BlockWhereTheBeaconRootsContractRuns(
                    SystemCallExpectations.Reverts())));

        [Fact]
        [Trait("Category", "EIP2935")]
        public async Task Given_TheHistoryStoragePredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
            => SystemCallExpectations.AssertAccepted(
                await AttemptAsync(SystemCallExpectations.BlockWhereTheHistoryStorageContractRuns(
                    SystemCallExpectations.Reverts())));

        [Fact]
        [Trait("Category", "EIP4788")]
        public async Task Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheRevertedWriteIsNotPersisted()
            => SystemCallExpectations.AssertSlotZeroIsUnwritten(
                await RunAsync(SystemCallExpectations.BlockWhereTheBeaconRootsContractRuns(
                    SystemCallExpectations.WritesSlotZeroThenReverts())),
                Nethereum.EVM.Execution.SystemCallContracts.BeaconRoots);
    }
}
