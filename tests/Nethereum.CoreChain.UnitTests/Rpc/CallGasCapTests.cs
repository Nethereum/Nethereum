using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class CallGasCapTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Target = "0x9999999999999999999999999999999999999a";

        private static readonly byte[] MemoryExpansionCode = "600062060E005200".HexToByteArray();

        private static async Task<DevChainNode> BuildNodeAsync(BigInteger rpcGasCap)
        {
            var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller });
            node.DevConfig.RpcGasCap = rpcGasCap;
            await node.SetCodeAsync(Target, MemoryExpansionCode);
            return node;
        }

        [Fact]
        public async Task CallAsync_GasFarAboveLowCap_EffectiveGasIsCapped_Fails()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 100_000);

            var result = await node.CallAsync(Target, System.Array.Empty<byte>(), Caller, gasLimit: 5_000_000);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CallAsync_GasWithinCap_Unaffected_Succeeds()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 1_000_000);

            var result = await node.CallAsync(Target, System.Array.Empty<byte>(), Caller, gasLimit: 500_000);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CallAsync_GasCapZero_Disabled_SameHighGasThatFailedWhenCapped_NowSucceeds()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 0);

            var result = await node.CallAsync(Target, System.Array.Empty<byte>(), Caller, gasLimit: 5_000_000);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task EstimateContractCreationGasAsync_GasFarAboveLowCap_EffectiveGasIsCapped_Fails()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 100_000);

            var result = await node.EstimateContractCreationGasAsync(MemoryExpansionCode, Caller, gasLimit: 5_000_000);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task EstimateContractCreationGasAsync_GasCapZero_Disabled_Succeeds()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 0);

            var result = await node.EstimateContractCreationGasAsync(MemoryExpansionCode, Caller, gasLimit: 5_000_000);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task EthCallHandler_GasFarAboveLowCap_ReturnsExecutionRevertedError()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 100_000);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthCallHandler();

            var request = new RpcRequestMessage(1, "eth_call",
                new { to = Target, data = "0x", gas = "0xffffffffffff" }, "latest");

            var response = await handler.HandleAsync(request, context);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task EthCallHandler_GasCapZero_Disabled_SameHugeGasSucceeds()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 0);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthCallHandler();

            var request = new RpcRequestMessage(1, "eth_call",
                new { to = Target, data = "0x", gas = "0xffffffffffff" }, "latest");

            var response = await handler.HandleAsync(request, context);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task EthEstimateGasHandler_CeilingBoundedByLowCap_ReturnsExecutionRevertedError()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 100_000);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthEstimateGasHandler();

            var request = new RpcRequestMessage(1, "eth_estimateGas",
                new { to = Target, data = "0x" }, "latest");

            var response = await handler.HandleAsync(request, context);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task EthEstimateGasHandler_CapDisabled_UsesFullCeiling_Succeeds()
        {
            using var node = await BuildNodeAsync(rpcGasCap: 0);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthEstimateGasHandler();

            var request = new RpcRequestMessage(1, "eth_estimateGas",
                new { to = Target, data = "0x" }, "latest");

            var response = await handler.HandleAsync(request, context);

            Assert.Null(response.Error);
        }
    }
}
