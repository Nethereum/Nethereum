using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EstimateGasNewAccountStateGasTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string FreshAccount = "0x00000000000000000000000000000000000fe5a1";
        private static readonly BigInteger TransferToANewAccountOnAmsterdam = 21_000 + 120 * 1_530;

        private static async Task<DevChainNode> StartAmsterdamAsync()
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "amsterdam" });
            await node.StartAsync(new[] { Caller });
            return node;
        }

        private static async Task<BigInteger> EstimateTransferAsync(DevChainNode node)
        {
            var response = await new EthEstimateGasHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_estimateGas", new { from = Caller, to = FreshAccount, value = "0x1" }, "latest"),
                new RpcContext(node, chainId: 1, services: null));

            Assert.Null(response.Error);
            return ((HexBigInteger)response.Result).Value;
        }

        [Fact]
        public async Task Given_AnAmsterdamChain_When_AValueTransferToANewAccountIsEstimated_Then_TheEstimateCoversTheNewAccountStateGas()
        {
            using var node = await StartAmsterdamAsync();

            var estimate = await EstimateTransferAsync(node);

            Assert.True(estimate >= TransferToANewAccountOnAmsterdam,
                $"estimate {estimate} is below the {TransferToANewAccountOnAmsterdam} a value transfer to a new account needs on Amsterdam");
        }

        [Fact]
        public async Task Given_AnAmsterdamChain_When_AValueTransferToANewAccountIsSentWithTheEstimate_Then_ItSucceeds()
        {
            using var node = await StartAmsterdamAsync();

            var estimate = await EstimateTransferAsync(node);
            var atEstimate = await node.CallAsync(FreshAccount, Array.Empty<byte>(), Caller, value: BigInteger.One, gasLimit: estimate);

            Assert.True(atEstimate.Success, $"a transfer given the estimated {estimate} gas must succeed");
        }

        [Fact]
        public async Task Given_AnAmsterdamChain_When_AValueTransferToANewAccountGetsLessThanItsStateGas_Then_ItFails()
        {
            using var node = await StartAmsterdamAsync();

            var belowStateGas = await node.CallAsync(FreshAccount, Array.Empty<byte>(), Caller, value: BigInteger.One, gasLimit: TransferToANewAccountOnAmsterdam - 1);

            Assert.False(belowStateGas.Success);
        }
    }
}
