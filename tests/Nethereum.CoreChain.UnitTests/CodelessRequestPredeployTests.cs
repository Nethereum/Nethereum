using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-7002 §Specification: <i>"If there is no code at
    /// <c>WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS</c>, the corresponding block MUST be marked
    /// invalid."</i> EIP-7251 §Specification states the same for its own predeploy, EIP-8282
    /// §Deployment for both of its builder predeploys.
    ///
    /// <para>EIP-4788 §Block processing and EIP-2935 §Specification state the opposite for
    /// theirs: <i>"if no code exists at <c>BEACON_ROOTS_ADDRESS</c>, the call must fail
    /// silently"</i>. EIP-7997 §Specification: <i>"Client software MUST NOT check for the
    /// existence of the contract at the fork boundary."</i></para>
    /// </summary>
    public class CodelessRequestPredeployTests
    {
        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> ExecuteWithNoCodeAtAsync(
            HardforkName fork, string address) =>
            ExecuteAsync(fork, Header(blockNumber: 1, timestamp: 1_700_000_000,
                    parentBeaconBlockRoot: new byte[32]),
                store => ReplaceWithCodelessAccountAsync(store, address));

        [Theory]
        [Trait("Category", "EIP7002")]
        [InlineData(SystemCallContracts.WithdrawalRequests)]
        [InlineData(SystemCallContracts.ConsolidationRequests)]
        [InlineData(SystemCallContracts.BuilderDeposit)]
        [InlineData(SystemCallContracts.BuilderExit)]
        public async Task Given_ARequestPredeployWithNoCode_When_TheBlockIsExecuted_Then_TheBlockIsInvalid(
            string absentAddress)
        {
            var (result, _) = await ExecuteWithNoCodeAtAsync(HardforkName.Amsterdam, absentAddress);

            AssertRefusedForMissingPredeploy(result.Exception, absentAddress);
        }

        [Theory]
        [Trait("Category", "EIP4788")]
        [InlineData(SystemCallContracts.BeaconRoots)]
        [InlineData(SystemCallContracts.HistoryStorage)]
        public async Task Given_ANonRequestPredeployWithNoCode_When_TheBlockIsExecuted_Then_TheBlockIsStillValid(
            string absentAddress)
        {
            var (result, _) = await ExecuteWithNoCodeAtAsync(HardforkName.Amsterdam, absentAddress);

            AssertBlockWasAccepted(result);
        }

        [Theory]
        [Trait("Category", "EIP8282")]
        [InlineData(SystemCallContracts.BuilderDeposit)]
        [InlineData(SystemCallContracts.BuilderExit)]
        public async Task Given_ABuilderPredeployWithNoCode_When_TheBlockPredatesAmsterdam_Then_TheBlockIsStillValid(
            string absentAddress)
        {
            Assert.DoesNotContain(absentAddress, SystemCallContracts.RequestContractsFor(HardforkName.Prague));

            var (result, _) = await ExecuteWithNoCodeAtAsync(HardforkName.Prague, absentAddress);

            AssertBlockWasAccepted(result);
        }

        [Fact]
        [Trait("Category", "EIP7002")]
        public async Task Given_EveryRequestPredeployHasCode_When_TheBlockIsExecuted_Then_TheBlockIsValid()
        {
            var (result, _) = await ExecuteAsync(HardforkName.Amsterdam,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]));

            AssertBlockWasAccepted(result);
        }

        [Theory]
        [Trait("Category", "EIP7002")]
        [InlineData(SystemCallContracts.WithdrawalRequests)]
        [InlineData(SystemCallContracts.ConsolidationRequests)]
        [InlineData(SystemCallContracts.BuilderDeposit)]
        [InlineData(SystemCallContracts.BuilderExit)]
        public async Task Given_ACodelessRequestPredeploy_When_RunOnBothEngines_Then_BothRefuseTheBlock(
            string absentAddress)
        {
            var (hostResult, _) = await ExecuteWithNoCodeAtAsync(HardforkName.Amsterdam, absentAddress);

            var guestRefusal = await ExecuteGuestVerdictAsync(HardforkName.Amsterdam, timestamp: 1_700_000_000,
                accounts: WitnessCarryingEveryActivatedPredeploy(
                    HardforkName.Amsterdam, absentAddress, new byte[0]));

            AssertRefusedForMissingPredeploy(hostResult.Exception, absentAddress);
            AssertRefusedForMissingPredeploy(guestRefusal, absentAddress);
        }

        private static void AssertRefusedForMissingPredeploy(System.Exception refusal, string address)
        {
            var missing = Assert.IsType<SystemCallPredeployMissingException>(refusal);
            Assert.True(missing.TargetAddress.IsTheSameAddress(address),
                $"the block was refused for {missing.TargetAddress}, not for the codeless {address}");
            Assert.Equal("no_code_at_predeploy", missing.Reason);
        }

        private static void AssertBlockWasAccepted(BlockExecutionResult result)
        {
            Assert.Null(result.Exception);
            Assert.False(result.ContainsInvalidTransaction);
            Assert.NotNull(result.PostStateRoot);
        }

        private static Task ReplaceWithCodelessAccountAsync(InMemoryStateStore stateStore, string address) =>
            stateStore.SaveAccountAsync(address, new Nethereum.Model.Account
            {
                Balance = EvmUInt256.Zero,
                Nonce = SystemContractPredeploy.Nonce,
                CodeHash = null
            });
    }
}
