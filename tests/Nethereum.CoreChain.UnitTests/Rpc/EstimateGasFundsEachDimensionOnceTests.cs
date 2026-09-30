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
    public class EstimateGasFundsEachDimensionOnceTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Recipient = "0x9999999999999999999999999999999999999999";
        private const long IntrinsicValueTransfer = 21_000;
        private const string InitCodeReturning32Bytes = "0x60206000f3";

        private static async Task<DevChainNode> StartAsync(string fork)
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = fork });
            await node.StartAsync(new[] { Caller });
            return node;
        }

        private static async Task<BigInteger> EstimateAsync(DevChainNode node, object callInput)
        {
            var response = await new EthEstimateGasHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_estimateGas", callInput, "latest"),
                new RpcContext(node, chainId: 1, services: null));

            Assert.Null(response.Error);
            return ((HexBigInteger)response.Result).Value;
        }

        private static BigInteger WithBuffer(BigInteger gas) => gas * 110 / 100;

        private static object ValueTransfer => new { from = Caller, to = Recipient, value = "0x1", data = "0x" };
        private static object Creation => new { from = Caller, data = InitCodeReturning32Bytes };

        [Fact]
        public async Task Given_APlainValueTransferAtPrague_When_Estimated_Then_ItIsTheIntrinsicCostOnceNotTwice()
        {
            using var node = await StartAsync("prague");

            Assert.Equal(WithBuffer(IntrinsicValueTransfer), await EstimateAsync(node, ValueTransfer));
        }

        [Fact]
        public async Task Given_APlainValueTransferAtAmsterdam_When_Estimated_Then_TheStateGasIsFundedOnceNotTwice()
        {
            using var node = await StartAsync("amsterdam");

            var simulated = await node.CallAsync(Recipient, System.Array.Empty<byte>(), Caller, value: 1);

            Assert.Equal(WithBuffer(simulated.GasUsed), await EstimateAsync(node, ValueTransfer));
        }

        [Fact]
        public async Task Given_APlainValueTransferAtAmsterdam_When_Estimated_Then_ItExceedsThePragueTransferByTheStateGas()
        {
            using var prague = await StartAsync("prague");
            using var amsterdam = await StartAsync("amsterdam");

            var atPrague = await EstimateAsync(prague, ValueTransfer);
            var atAmsterdam = await EstimateAsync(amsterdam, ValueTransfer);

            Assert.True(atAmsterdam > atPrague,
                $"Amsterdam meters state gas the caller must fund; prague {atPrague}, amsterdam {atAmsterdam}. " +
                "Equal figures mean the state half was dropped rather than counted once");
        }

        [Fact]
        public async Task Given_AnAmsterdamCreation_When_Estimated_Then_ItStaysBelowTheTotalPlusASecondStateCharge()
        {
            using var node = await StartAsync("amsterdam");

            var simulated = await node.EstimateContractCreationGasAsync(
                InitCodeReturning32Bytes.HexToByteArray(), await node.GetBlockNumberAsync(), Caller);

            var estimate = await EstimateAsync(node, Creation);

            Assert.True(estimate >= WithBuffer(simulated.GasUsed),
                $"the estimate ({estimate}) must cover the simulated total ({simulated.GasUsed}) and the buffer");

            Assert.True(estimate < WithBuffer(simulated.GasUsed + simulated.StateGasUsed),
                $"the simulated total ({simulated.GasUsed}) already contains its {simulated.StateGasUsed} of " +
                $"state gas; an estimate of {estimate} means the caller is being asked to fund it twice");
        }

        [Fact]
        public async Task Given_APragueCreation_When_Estimated_Then_ItIsTheSimulatedTotalIncludingIntrinsicOnce()
        {
            using var node = await StartAsync("prague");

            var simulated = await node.EstimateContractCreationGasAsync(
                InitCodeReturning32Bytes.HexToByteArray(), await node.GetBlockNumberAsync(), Caller);

            Assert.Equal(WithBuffer(simulated.GasUsed), await EstimateAsync(node, Creation));
        }
    }
}
