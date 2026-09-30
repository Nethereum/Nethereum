using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class ContractCreationEstimateDeploymentGuardTests
    {
        private const long AboveIntrinsicBelowTheDeposit = 55_000;

        private static async Task<CallResult> EstimateAsync(
            byte[] initCode, string hardfork = "prague", long? gasLimit = null)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;

            using var node = new DevChainNode(config);
            await node.StartAsync();

            return gasLimit.HasValue
                ? await node.EstimateContractCreationGasAsync(initCode, null, null, gasLimit.Value)
                : await node.EstimateContractCreationGasAsync(initCode);
        }

        private static byte[] InitCodeReturning(int size) => new byte[]
        {
            0x62, (byte)(size >> 16), (byte)(size >> 8), (byte)size,
            0x60, 0x00,
            0xf3
        };

        [Fact]
        [Trait("Category", "EIP170")]
        public async Task Given_DeployedCodeOverTheSizeCap_When_Estimated_Then_TheCreationIsReportedAsFailing()
        {
            var overCap = await EstimateAsync(InitCodeReturning(24_577));
            Assert.False(overCap.Success,
                $"a creation deploying 24,577 bytes exceeds EIP-170 and cannot succeed; " +
                $"estimate reported success with gasUsed {overCap.GasUsed}");
            Assert.Contains("max code size", overCap.RevertReason);

            var atCap = await EstimateAsync(InitCodeReturning(24_576));
            Assert.True(atCap.Success, atCap.RevertReason);
            Assert.Equal(24_576, atCap.ReturnData.Length);
        }

        [Fact]
        [Trait("Category", "EIP3541")]
        public async Task Given_DeployedCodeBeginningWith0xEF_When_Estimated_Then_TheCreationIsReportedAsFailing()
        {
            var initCode = new byte[]
            {
                0x60, 0xEF, 0x60, 0x00, 0x53,
                0x60, 0x01, 0x60, 0x00, 0xf3
            };

            var atPrague = await EstimateAsync(initCode);
            Assert.False(atPrague.Success,
                $"EIP-3541 forbids deployed code beginning 0xEF; estimate reported success " +
                $"with gasUsed {atPrague.GasUsed}");
            Assert.Contains("0xEF", atPrague.RevertReason);

            var atBerlin = await EstimateAsync(initCode, hardfork: "berlin");
            Assert.True(atBerlin.Success, atBerlin.RevertReason);
            Assert.Equal(new byte[] { 0xEF }, atBerlin.ReturnData);
        }

        [Fact]
        [Trait("Category", "CodeDeposit")]
        public async Task Given_ADepositItCannotAfford_When_Estimated_Then_TheCreationIsReportedAsFailing()
        {
            var initCode = InitCodeReturning(32);

            var underfunded = await EstimateAsync(initCode, gasLimit: AboveIntrinsicBelowTheDeposit);
            Assert.False(underfunded.Success,
                $"the deposit for 32 bytes (6,400) is unaffordable within {AboveIntrinsicBelowTheDeposit} gas, " +
                $"which covers the intrinsic cost and reaches the deposit charge; " +
                $"estimate reported success with gasUsed {underfunded.GasUsed}");
            Assert.Contains("contract code deposit", underfunded.RevertReason);

            var funded = await EstimateAsync(initCode, gasLimit: 1_000_000);
            Assert.True(funded.Success, funded.RevertReason);

            var evmAndDepositGas = funded.GasUsed - funded.IntrinsicGasUsed;
            Assert.True(evmAndDepositGas >= 6_400,
                $"the deposit for 32 bytes (6,400) must be included; got {evmAndDepositGas}");
            Assert.True(evmAndDepositGas < 6_400 + 1_000,
                $"a 7-byte init code costs only a few gas to run, so {evmAndDepositGas} beyond the " +
                "intrinsic cost means something other than the deposit is being charged");
        }

        [Fact]
        [Trait("Category", "EIP3541")]
        public async Task Given_CodeBothOverTheCapAndPrefixed_When_Estimated_Then_ItFailsForTheReasonTheExecutorWouldGive()
        {
            var initCode = new byte[]
            {
                0x60, 0xEF, 0x60, 0x00, 0x53,
                0x62, 0x00, 0x60, 0x01,
                0x60, 0x00, 0xf3
            };

            var result = await EstimateAsync(initCode);

            Assert.False(result.Success);
            Assert.Contains("0xEF", result.RevertReason);
        }

        [Fact]
        [Trait("Category", "EIP170")]
        public async Task Given_TheHistoricalBlockOverload_When_CodeIsOverTheCap_Then_ItGuardsIdentically()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "prague";

            using var node = new DevChainNode(config);
            await node.StartAsync();
            await node.MineBlockAsync();

            var atBlock = await node.EstimateContractCreationGasAsync(
                InitCodeReturning(24_577), await node.GetBlockNumberAsync());

            Assert.False(atBlock.Success,
                $"the historical-block overload must apply the same EIP-170 cap; " +
                $"got success with gasUsed {atBlock.GasUsed}");
            Assert.Contains("max code size", atBlock.RevertReason);
        }
    }
}
