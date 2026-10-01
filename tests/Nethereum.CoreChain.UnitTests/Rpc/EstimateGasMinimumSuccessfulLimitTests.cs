using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EstimateGasMinimumSuccessfulLimitTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string GasGated = "0x7777777777777777777777777777777777777777";
        private const string RevertsUnlessMoreThan100000GasLeft = "0x5a620186a010600a57fe5b00";
        private static readonly BigInteger MinimumSuccessfulLimit = 21_000 + 2 + 100_001;

        private static async Task<DevChainNode> StartWithGasGatedContractAsync()
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "prague" });
            await node.StartAsync(new[] { Caller });
            await node.SetCodeAsync(GasGated, RevertsUnlessMoreThan100000GasLeft.HexToByteArray());
            return node;
        }

        private static async Task<BigInteger> EstimateAsync(DevChainNode node)
        {
            var response = await new EthEstimateGasHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_estimateGas", new { from = Caller, to = GasGated, data = "0x" }, "latest"),
                new RpcContext(node, chainId: 1, services: null));

            Assert.Null(response.Error);
            return ((HexBigInteger)response.Result).Value;
        }

        [Fact]
        public async Task Given_AContractThatNeedsMoreGasLeftThanItUses_When_Estimated_Then_ACallWithTheEstimatedGasSucceeds()
        {
            using var node = await StartWithGasGatedContractAsync();

            var estimate = await EstimateAsync(node);
            var atEstimate = await node.CallAsync(GasGated, Array.Empty<byte>(), Caller, gasLimit: estimate);

            Assert.True(atEstimate.Success,
                $"a call given the estimated {estimate} gas must succeed; the contract needs {MinimumSuccessfulLimit}");
        }

        [Fact]
        public async Task Given_AContractThatNeedsMoreGasLeftThanItUses_When_Estimated_Then_TheEstimateStaysNearTheLowestSuccessfulLimit()
        {
            using var node = await StartWithGasGatedContractAsync();

            var estimate = await EstimateAsync(node);

            Assert.InRange(estimate, MinimumSuccessfulLimit, MinimumSuccessfulLimit * 1015 / 1000 * 110 / 100 + 1);
        }
    }
}
