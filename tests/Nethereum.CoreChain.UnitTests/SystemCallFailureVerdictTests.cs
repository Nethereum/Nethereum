using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    public class SystemCallFailureVerdictTests
    {
        private static readonly byte[] Reverts = "60006000fd".HexToByteArray();

        private static readonly byte[] RunsOutOfGas = "600063007a120052".HexToByteArray();

        private static readonly byte[] Throws = "fe".HexToByteArray();

        private static readonly byte[] WritesSlotZeroThenReverts = "600160005560006000fd".HexToByteArray();

        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> RunAmsterdamWithRequestPredeploy(
            string contractAddress, byte[] code) =>
            ExecuteAsync(HardforkName.Amsterdam, Header(blockNumber: 1, timestamp: 1_700_000_000),
                store => DeployAsync(store, contractAddress, code));

        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> RunAmsterdamWithBuilderDeposit(
            byte[] code) =>
            RunAmsterdamWithRequestPredeploy(SystemCallContracts.BuilderDeposit, code);

        private static void AssertRefusedForSystemCallFailure(BlockExecutionResult result, string contractAddress)
        {
            var failure = Assert.IsType<SystemCallFailedException>(result.Exception);
            Assert.True(failure.TargetAddress.IsTheSameAddress(contractAddress),
                $"the block was refused for {failure.TargetAddress}, not for {contractAddress}");
        }

        [Theory]
        [Trait("Category", "EIP8282")]
        [InlineData(SystemCallContracts.WithdrawalRequests)]
        [InlineData(SystemCallContracts.ConsolidationRequests)]
        [InlineData(SystemCallContracts.BuilderDeposit)]
        [InlineData(SystemCallContracts.BuilderExit)]
        public async Task Given_ARequestPredeployThatReverts_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed(
            string contractAddress)
        {
            var (result, _) = await RunAmsterdamWithRequestPredeploy(contractAddress, Reverts);
            AssertRefusedForSystemCallFailure(result, contractAddress);
        }

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_ARequestPredeployThatRunsOutOfGas_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
        {
            var (result, _) = await RunAmsterdamWithBuilderDeposit(RunsOutOfGas);
            AssertRefusedForSystemCallFailure(result, SystemCallContracts.BuilderDeposit);
        }

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_ARequestPredeployThatHitsAnInvalidOpcode_When_TheBlockIsExecuted_Then_TheBlockIsInvalidWithSystemCallFailed()
        {
            var (result, _) = await RunAmsterdamWithBuilderDeposit(Throws);
            AssertRefusedForSystemCallFailure(result, SystemCallContracts.BuilderDeposit);
        }

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_EveryRequestPredeploySucceeds_When_TheBlockIsExecuted_Then_TheBlockIsValid()
        {
            var (result, _) = await ExecuteAsync(HardforkName.Amsterdam,
                Header(blockNumber: 1, timestamp: 1_700_000_000),
                async store =>
                {
                    await DeployMarkerAsync(store, SystemCallContracts.WithdrawalRequests);
                    await DeployMarkerAsync(store, SystemCallContracts.ConsolidationRequests);
                    await DeployMarkerAsync(store, SystemCallContracts.BuilderDeposit);
                    await DeployMarkerAsync(store, SystemCallContracts.BuilderExit);
                });

            Assert.Null(result.Exception);
        }

        [Theory]
        [Trait("Category", "EIP8282")]
        [InlineData("revert")]
        [InlineData("out-of-gas")]
        [InlineData("invalid-opcode")]
        public async Task Given_TheSameFailingRequestSystemCall_When_RunOnBothEngines_Then_BothRefuseTheBlock(
            string failureShape)
        {
            var failingCode = CodeFor(failureShape);

            var (hostResult, _) = await RunAmsterdamWithBuilderDeposit(failingCode);
            var guestRefusal = await ExecuteGuestVerdictAsync(
                HardforkName.Amsterdam, timestamp: 1_700_000_000,
                accounts: WitnessCarryingEveryActivatedPredeploy(
                    HardforkName.Amsterdam, SystemCallContracts.BuilderDeposit, failingCode));

            AssertRefusedForSystemCallFailure(hostResult, SystemCallContracts.BuilderDeposit);

            var guestFailure = Assert.IsType<SystemCallFailedException>(guestRefusal);
            Assert.True(guestFailure.TargetAddress.IsTheSameAddress(SystemCallContracts.BuilderDeposit),
                $"the guest refused for {guestFailure.TargetAddress}, not for the builder-deposit predeploy");
        }

        [Fact]
        [Trait("Category", "EIP8282")]
        public async Task Given_TheSameSucceedingRequestSystemCall_When_RunOnBothEngines_Then_NeitherRefusesTheBlock()
        {
            var (hostResult, _) = await RunAmsterdamWithBuilderDeposit(MarkerCode);
            var guestRefusal = await ExecuteGuestVerdictAsync(
                HardforkName.Amsterdam, timestamp: 1_700_000_000,
                accounts: WitnessCarryingEveryActivatedPredeploy(
                    HardforkName.Amsterdam, SystemCallContracts.BuilderDeposit, MarkerCode));

            Assert.Null(hostResult.Exception);
            Assert.Null(guestRefusal);
        }

        private static byte[] CodeFor(string failureShape)
        {
            switch (failureShape)
            {
                case "revert": return Reverts;
                case "out-of-gas": return RunsOutOfGas;
                case "invalid-opcode": return Throws;
                default: throw new System.ArgumentOutOfRangeException(nameof(failureShape), failureShape, null);
            }
        }

        [Fact]
        [Trait("Category", "EIP4788")]
        public async Task Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
        {
            var (result, _) = await ExecuteAsync(HardforkName.Cancun,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]),
                store => DeployAsync(store, SystemCallContracts.BeaconRoots, Reverts));

            Assert.Null(result.Exception);
        }

        [Fact]
        [Trait("Category", "EIP4788")]
        public async Task Given_TheBeaconRootsPredeployReverts_When_TheBlockIsExecuted_Then_TheRevertedWriteIsNotPersisted()
        {
            var (_, stateStore) = await ExecuteAcceptedAsync(HardforkName.Cancun,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]),
                store => DeployAsync(store, SystemCallContracts.BeaconRoots, WritesSlotZeroThenReverts));

            var slotZero = await stateStore.GetStorageAsync(SystemCallContracts.BeaconRoots, BigInteger.Zero)
                ?? new byte[32];
            Assert.True(slotZero.All(b => b == 0),
                $"the reverted write survived: slot 0 is 0x{slotZero.ToHex()}");
        }

        [Fact]
        [Trait("Category", "EIP2935")]
        public async Task Given_TheHistoryStoragePredeployReverts_When_TheBlockIsExecuted_Then_TheBlockIsStillValid()
        {
            var (result, _) = await ExecuteAsync(HardforkName.Prague,
                Header(blockNumber: 1, timestamp: 1_700_000_000),
                store => DeployAsync(store, SystemCallContracts.HistoryStorage, Reverts));

            Assert.Null(result.Exception);
        }
    }
}
