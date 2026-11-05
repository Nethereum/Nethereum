using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class NestedCreate2AcrossForksTests
    {
        private const string FactoryAddress = "0x00000000000000000000000000000000000fac70";

        private static byte[] ChildInitCodeReturning(int size) => new byte[]
        {
            0x62, (byte)(size >> 16), (byte)(size >> 8), (byte)size,
            0x60, 0x00,
            0xf3
        };

        private static byte[] FactoryRuntimeCreating(int childSize)
        {
            var child = ChildInitCodeReturning(childSize);

            return new byte[] { 0x66 }
                .Concat(child)
                .Concat(new byte[]
                {
                    0x60, 0x00, 0x52,
                    0x60, 0x00,
                    0x60, 0x07,
                    0x60, 0x19,
                    0x60, 0x00,
                    0xf5,
                    0x60, 0x00, 0x52,
                    0x60, 0x20, 0x60, 0x00, 0xf3
                })
                .ToArray();
        }

        private static async Task<CallResult> CreateThroughFactoryAsync(string hardfork, int childSize)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;

            using var node = new DevChainNode(config);
            await node.StartAsync();
            await node.SetCodeAsync(FactoryAddress, FactoryRuntimeCreating(childSize));

            return await node.CallAsync(FactoryAddress, Array.Empty<byte>());
        }

        private static bool IsZeroAddress(byte[] returned) =>
            returned == null || returned.All(b => b == 0);

        [Theory]
        [InlineData("prague")]
        [InlineData("osaka")]
        [InlineData("amsterdam")]
        public async Task Given_AContractCreatingASmallChildWithCreate2_When_CalledAtAnyFork_Then_TheChildIsCreated(string hardfork)
        {
            var result = await CreateThroughFactoryAsync(hardfork, childSize: 32);

            Assert.True(result.Success, $"{hardfork}: {result.RevertReason}");
            Assert.False(IsZeroAddress(result.ReturnData),
                $"{hardfork}: CREATE2 returned the zero address, so the nested creation failed");
        }

        [Fact]
        public async Task Given_ANestedCreationAtAmsterdam_When_TheDepositGrows_Then_TheReportedGasGrowsByTheStateGasItWillBeCharged()
        {
            const int small = 32;
            const int large = 6_000;

            var atSmall = await CreateThroughFactoryAsync("amsterdam", small);
            var atLarge = await CreateThroughFactoryAsync("amsterdam", large);

            Assert.True(atSmall.Success, atSmall.RevertReason);
            Assert.True(atLarge.Success, atLarge.RevertReason);

            var reportedGrowth = atLarge.GasUsed - atSmall.GasUsed;
            var stateGasForTheExtraBytes =
                (long)(large - small) * Nethereum.EVM.Gas.GasConstants.EIP8037_COST_PER_STATE_BYTE;

            Assert.True(reportedGrowth >= stateGasForTheExtraBytes,
                $"EIP-8037 charges {Nethereum.EVM.Gas.GasConstants.EIP8037_COST_PER_STATE_BYTE} per deposited " +
                $"byte, so depositing {large - small} more bytes adds at least {stateGasForTheExtraBytes} state " +
                $"gas; the reported gas grew by only {reportedGrowth}. A caller sizing a transaction from this " +
                "number is short by the state gas the transaction will be charged.");
        }

        [Fact]
        public async Task Given_ANestedCreationThatDepositsCode_When_ItsGasIsEstimated_Then_AmsterdamIsNotCheaperThanOsaka()
        {
            const int childSize = 6_000;

            var atOsaka = await CreateThroughFactoryAsync("osaka", childSize);
            var atAmsterdam = await CreateThroughFactoryAsync("amsterdam", childSize);

            Assert.True(atOsaka.Success, atOsaka.RevertReason);
            Assert.True(atAmsterdam.Success, atAmsterdam.RevertReason);

            Assert.True(atAmsterdam.GasUsed >= atOsaka.GasUsed,
                $"EIP-8037 prices every deposited byte as state gas, so depositing {childSize} bytes " +
                $"cannot cost less at Amsterdam than at Osaka; reported {atAmsterdam.GasUsed} against " +
                $"{atOsaka.GasUsed}. An estimate that omits state gas leaves a caller short by exactly " +
                "the amount the transaction will be charged.");
        }

        [Theory]
        [InlineData("prague")]
        [InlineData("osaka")]
        [InlineData("amsterdam")]
        public async Task Given_AContractCreatingAContractSizedLikeAnAccount_When_CalledAtAnyFork_Then_TheChildIsCreated(string hardfork)
        {
            var result = await CreateThroughFactoryAsync(hardfork, childSize: 6_000);

            Assert.True(result.Success, $"{hardfork}: {result.RevertReason}");
            Assert.False(IsZeroAddress(result.ReturnData),
                $"{hardfork}: CREATE2 returned the zero address, so the nested creation failed");
        }
    }
}
