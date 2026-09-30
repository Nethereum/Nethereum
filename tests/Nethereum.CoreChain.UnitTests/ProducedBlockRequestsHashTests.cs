using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using static Nethereum.CoreChain.UnitTests.AmsterdamBlockPipelineHarness;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-7685 §Block Header: <i>"Extend the header with a new 32 byte commitment value
    /// <c>requests_hash</c>"</i>. EIP-7685 §Removing empty requests in commitment: <i>"For a
    /// block with no requests data, the <c>requests_hash</c> is simply <c>sha256("")</c>."</i>
    ///
    /// <para>The execution side of that commitment is covered elsewhere; what a produced block
    /// carries in its sealed header is not. Every pipeline block is produced with idle predeploys
    /// and no deposits, where the commitment is <c>sha256("")</c> whether the producer stamps what
    /// the block computed or a constant - so a produced block committing to the wrong requests is
    /// invisible. A predeploy that returns request data is what separates the two.</para>
    /// </summary>
    public class ProducedBlockRequestsHashTests
    {
        private static readonly byte[] ReturnsOneRequestByte = "60ff60005360016000f3".HexToByteArray();

        private static byte[] EmptyRequestsHash =>
            ExecutionRequests.ComputeRequestsHash(new byte[0][]);

        private static Task<BlockProductionResult> ProduceAsync(
            HardforkName fork = HardforkName.Amsterdam, string requestPredeploy = null) =>
            ProduceBlockAsync(
                new List<ISignedTransaction> { Transfer(nonce: 0) },
                DefaultBlockGasLimit,
                blockAccessListStore: null,
                fork: fork,
                seed: requestPredeploy == null
                    ? null
                    : store => SystemCallBlockHarness.DeployAsync(
                        store, requestPredeploy, ReturnsOneRequestByte));

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_ARequestPredeployReturningData_When_ABlockIsProduced_Then_ItsHeaderCommitsToThoseRequests()
        {
            var produced = await ProduceAsync(requestPredeploy: SystemCallContracts.WithdrawalRequests);

            Assert.NotNull(produced.Header.RequestsHash);
            Assert.NotEqual(EmptyRequestsHash.ToHex(), produced.Header.RequestsHash.ToHex());
            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new[]
                {
                    ExecutionRequests.Compose(ExecutionRequests.WithdrawalRequestType, new byte[] { 0xff })
                }).ToHex(),
                produced.Header.RequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_IdlePredeploysAndNoDeposits_When_ABlockIsProduced_Then_ItsHeaderCommitsToSha256OfTheEmptyString()
        {
            var produced = await ProduceAsync();

            Assert.Equal(EmptyRequestsHash.ToHex(), produced.Header.RequestsHash.ToHex());
        }

        /// <summary>
        /// EIP-7685 arrives at Prague, so a block produced before it carries no
        /// <c>requests_hash</c> field at all - which is a different header, and a different block
        /// hash, from one committing to an empty request list.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP7685")]
        public async Task Given_APrePragueFork_When_ABlockIsProduced_Then_ItsHeaderCarriesNoRequestsHash()
        {
            var produced = await ProduceAsync(HardforkName.Cancun);

            Assert.Null(produced.Header.RequestsHash);
        }
    }
}
