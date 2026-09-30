using Nethereum.CoreChain;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    /// <summary>
    /// EIP-7685 §Block Header. The guest builds the header it would produce, so its
    /// <c>requests_hash</c> must be the hash of the requests it actually ran - not the value it
    /// was handed. A header that echoes its input can never disagree with a peer, which is what
    /// made this defect invisible.
    /// </summary>
    public class GuestRequestsCommitmentTests
    {
        private static readonly byte[] ReturnsOneRequestByte = "60ff60005360016000f3".HexToByteArray();

        private static BlockExecutionResult Execute(Nethereum.EVM.Witness.BlockWitnessData block) =>
            BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance,
                null,
                new PatriciaBlockRootCalculator());

        private static BlockExecutionResult Run(string predeploy, byte[] code)
        {
            var block = predeploy == null
                ? SystemCallExpectations.BlockAt(HardforkName.Amsterdam)
                : SystemCallExpectations.BlockWhereTheRequestPredeployRuns(predeploy, code);

            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = false;

            return Execute(block);
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_ABlockWhoseRequestPredeploysReturnNothing_When_Executed_Then_TheGuestCommitsToTheEmptyHash()
        {
            var result = Run(null, null);

            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new byte[0][]).ToHex(),
                result.ProducedHeader.RequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_ARequestPredeployThatReturnsData_When_Executed_Then_TheGuestCommitsToWhatItRan()
        {
            var result = Run(SystemCallExpectations.WithdrawalRequests, ReturnsOneRequestByte);

            var expected = ExecutionRequests.ComputeRequestsHash(new[]
            {
                ExecutionRequests.Compose(ExecutionRequests.WithdrawalRequestType, new byte[] { 0xff })
            });

            Assert.NotNull(result.ProducedHeader.RequestsHash);
            Assert.Equal(expected.ToHex(), result.ProducedHeader.RequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_TheSameDataFromADifferentPredeploy_When_Executed_Then_TheGuestCommitsDifferently()
        {
            var fromWithdrawals = Run(SystemCallExpectations.WithdrawalRequests, ReturnsOneRequestByte);
            var fromBuilderExit = Run(SystemCallExpectations.BuilderExit, ReturnsOneRequestByte);

            Assert.NotEqual(
                fromWithdrawals.ProducedHeader.RequestsHash.ToHex(),
                fromBuilderExit.ProducedHeader.RequestsHash.ToHex());
        }

        /// <summary>
        /// EIP-7685 arrives at Prague, so a header before it carries no commitment at all - which
        /// is a different thing from the hash of an empty list.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP7685")]
        public void Given_ABlockBeforePrague_When_Executed_Then_TheGuestCommitsToNothing()
        {
            var block = SystemCallExpectations.BlockAt(HardforkName.Cancun);
            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = false;

            var result = Execute(block);

            Assert.Null(result.ProducedHeader.RequestsHash);
        }
    }
}
