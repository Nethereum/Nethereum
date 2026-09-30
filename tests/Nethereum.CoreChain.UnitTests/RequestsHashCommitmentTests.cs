using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-7685 §Block Header: <i>"For a block with no requests data, the <c>requests_hash</c> is
    /// simply <c>sha256("")</c>."</i> and the commitment is built by hashing each non-empty request
    /// and then hashing the digests.
    ///
    /// <para>Nothing in the corpus can see this rule: every fixture chain has no deposit contract
    /// and idle predeploys, so its request list is empty and the answer is <c>sha256("")</c>
    /// whether the engine computes it or hardcodes it. These tests are the only evidence.</para>
    /// </summary>
    public class RequestsHashCommitmentTests
    {
        private static readonly byte[] ReturnsOneRequestByte = "60ff60005360016000f3".HexToByteArray();

        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)>
            ExecuteAmsterdamAsync(string contractAddress = null, byte[] code = null) =>
            ExecuteAsync(HardforkName.Amsterdam,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]),
                contractAddress == null ? null : store => DeployAsync(store, contractAddress, code));

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_ABlockWhoseRequestPredeploysReturnNothing_When_Executed_Then_TheHashIsSha256OfTheEmptyString()
        {
            var (result, _) = await ExecuteAmsterdamAsync();

            Assert.Null(result.Exception);
            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new byte[0][]).ToHex(),
                result.ComputedRequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_ARequestPredeployThatReturnsData_When_Executed_Then_TheHashIsNoLongerTheEmptyOne()
        {
            var (result, _) = await ExecuteAmsterdamAsync(
                SystemCallContracts.WithdrawalRequests, ReturnsOneRequestByte);

            Assert.Null(result.Exception);
            Assert.NotEqual(
                ExecutionRequests.ComputeRequestsHash(new byte[0][]).ToHex(),
                result.ComputedRequestsHash.ToHex());
            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new[]
                {
                    ExecutionRequests.Compose(ExecutionRequests.WithdrawalRequestType, new byte[] { 0xff })
                }).ToHex(),
                result.ComputedRequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_TheSameDataFromADifferentPredeploy_When_Executed_Then_TheHashDiffers()
        {
            var (fromWithdrawals, _) = await ExecuteAmsterdamAsync(
                SystemCallContracts.WithdrawalRequests, ReturnsOneRequestByte);
            var (fromBuilderExit, _) = await ExecuteAmsterdamAsync(
                SystemCallContracts.BuilderExit, ReturnsOneRequestByte);

            Assert.NotEqual(
                fromWithdrawals.ComputedRequestsHash.ToHex(),
                fromBuilderExit.ComputedRequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_AHeaderCommittingToTheRequestsTheBlockProduced_When_Executed_Then_ItIsNotRefused()
        {
            var header = Header(blockNumber: 1, timestamp: 1_700_000_000,
                parentBeaconBlockRoot: new byte[32]);
            header.RequestsHash = ExecutionRequests.ComputeRequestsHash(new byte[0][]);

            var (result, _) = await ExecuteAsync(HardforkName.Amsterdam, header);

            Assert.NotNull(result.ComputedRequestsHash);
            Assert.Equal(header.RequestsHash.ToHex(), result.ComputedRequestsHash.ToHex());
            Assert.False(result.RequestsHashMismatch);
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_AHeaderWhoseRequestsHashIsNotTheBlocksOwn_When_Executed_Then_TheBlockIsRefused()
        {
            var header = Header(blockNumber: 1, timestamp: 1_700_000_000,
                parentBeaconBlockRoot: new byte[32]);
            header.RequestsHash = Enumerable.Repeat((byte)0xab, 32).ToArray();

            var (result, _) = await ExecuteAsync(HardforkName.Amsterdam, header);

            Assert.True(result.RequestsHashMismatch,
                $"the block computed 0x{result.ComputedRequestsHash?.ToHex()} and the header " +
                "declared something else, and it was accepted");
        }
    }
}
