using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class RequestSystemCallTests
    {
        private static Nethereum.EVM.Execution.BlockExecutionResult Run(HardforkName fork)
            => Run(SystemCallExpectations.BlockAt(fork));

        private static Nethereum.EVM.Execution.BlockExecutionResult Run(Nethereum.EVM.Witness.BlockWitnessData block)
        {
            return Nethereum.EVM.Execution.BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance);
        }

        private static System.Exception Attempt(Nethereum.EVM.Witness.BlockWitnessData block)
            => Record.Exception(() => Run(block));

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_AnAmsterdamBlock_When_ExecutedSynchronously_Then_AllFourRequestContractsAreCalled()
            => SystemCallExpectations.AssertAmsterdamCallsAllFour(Run(HardforkName.Amsterdam));

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_APragueBlock_When_ExecutedSynchronously_Then_OnlyTheTwoPragueContractsAreCalled()
            => SystemCallExpectations.AssertPragueCallsOnlyTheTwoPragueContracts(Run(HardforkName.Prague));

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_ACancunBlock_When_ExecutedSynchronously_Then_NoRequestContractIsCalled()
            => SystemCallExpectations.AssertCancunCallsNone(Run(HardforkName.Cancun));

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAmsterdamBlock_When_ExecutedSynchronously_Then_TheRequestContractsAreIndexedAfterTheTransactions()
            => SystemCallExpectations.AssertRequestsIndexedAfterTransactions(Run(HardforkName.Amsterdam), transactionCount: 0);

        [Theory]
        [Trait("Category", "EIP8282")]
        [InlineData(SystemCallExpectations.WithdrawalRequests)]
        [InlineData(SystemCallExpectations.Consolidations)]
        [InlineData(SystemCallExpectations.BuilderDeposit)]
        [InlineData(SystemCallExpectations.BuilderExit)]
        public void Given_ARequestPredeployThatReverts_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed(
            string contractAddress)
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                Attempt(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    contractAddress, SystemCallExpectations.Reverts())),
                contractAddress);

        [Fact]
        [Trait("Category", "EIP8282")]
        public void Given_ARequestPredeployThatRunsOutOfGas_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                Attempt(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    SystemCallExpectations.BuilderDeposit, SystemCallExpectations.RunsOutOfGas())),
                SystemCallExpectations.BuilderDeposit);

        [Fact]
        [Trait("Category", "EIP8282")]
        public void Given_ARequestPredeployThatHitsAnInvalidOpcode_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
            => SystemCallExpectations.AssertRefusedForSystemCallFailure(
                Attempt(SystemCallExpectations.BlockWhereTheRequestPredeployRuns(
                    SystemCallExpectations.BuilderDeposit, SystemCallExpectations.Throws())),
                SystemCallExpectations.BuilderDeposit);

        [Fact]
        [Trait("Category", "EIP8282")]
        public void Given_EveryRequestPredeploySucceeds_When_TheBlockIsExecuted_Then_TheBlockIsValid()
            => SystemCallExpectations.AssertAccepted(Attempt(SystemCallExpectations.BlockAt(HardforkName.Amsterdam)));

        [Fact]
        [Trait("Category", "EIP4788")]
        public void Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
            => SystemCallExpectations.AssertAccepted(
                Attempt(SystemCallExpectations.BlockWhereTheBeaconRootsContractRuns(SystemCallExpectations.Reverts())));

        [Fact]
        [Trait("Category", "EIP2935")]
        public void Given_TheHistoryStoragePredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
            => SystemCallExpectations.AssertAccepted(
                Attempt(SystemCallExpectations.BlockWhereTheHistoryStorageContractRuns(SystemCallExpectations.Reverts())));

        [Fact]
        [Trait("Category", "EIP4788")]
        public void Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheRevertedWriteIsNotPersisted()
            => SystemCallExpectations.AssertSlotZeroIsUnwritten(
                Run(SystemCallExpectations.BlockWhereTheBeaconRootsContractRuns(
                    SystemCallExpectations.WritesSlotZeroThenReverts())),
                Nethereum.EVM.Execution.SystemCallContracts.BeaconRoots);
    }
}
