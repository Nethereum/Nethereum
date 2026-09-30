using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EstimateIncludesStateGasTests
    {
        private const int DeployedBytes = 32;
        private const long PreAmsterdamPerByte = 200;
        private const long AmsterdamPerStateByte = 1530;

        private static byte[] InitCodeReturning32Bytes()
            => new byte[] { 0x60, 0x20, 0x60, 0x00, 0xf3 };

        private static async Task<CallResult> EstimateAsync(string fork)
        {
            using var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = fork });
            await node.StartAsync();
            return await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());
        }

        [Fact]
        public async Task Given_APragueCreation_When_Estimated_Then_NoStateGasIsReported()
        {
            var estimate = await EstimateAsync("prague");

            Assert.True(estimate.Success, estimate.RevertReason);
            Assert.Equal(BigInteger.Zero, estimate.StateGasUsed);
        }

        [Fact]
        public async Task Given_AnAmsterdamCreation_When_Estimated_Then_TheDepositIsReportedAsStateGasNotExecutionGas()
        {
            var estimate = await EstimateAsync("amsterdam");

            Assert.True(estimate.Success, estimate.RevertReason);
            Assert.True(estimate.StateGasUsed >= DeployedBytes * AmsterdamPerStateByte,
                $"the {DeployedBytes}-byte deposit costs {DeployedBytes * AmsterdamPerStateByte} state gas " +
                $"and must be reported as state gas; got {estimate.StateGasUsed}");
        }

        [Fact]
        public async Task Given_BothForks_When_Compared_Then_AmsterdamCostsTheStateByteDifferenceMore()
        {
            var prague = await EstimateAsync("prague");
            var amsterdam = await EstimateAsync("amsterdam");

            var minimumIncrease = DeployedBytes * (AmsterdamPerStateByte - PreAmsterdamPerByte);

            Assert.True(amsterdam.GasUsed - prague.GasUsed >= minimumIncrease,
                $"crossing to Amsterdam must add at least {minimumIncrease}; " +
                $"prague {prague.GasUsed}, amsterdam {amsterdam.GasUsed}. Equal totals mean the " +
                "state-gas half is being dropped from what the caller is told to fund");
        }
    }
}
