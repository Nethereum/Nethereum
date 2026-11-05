using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EthBaseFeeAndStorageValuesTests
    {
        private const int ChainId = 31337;
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string Contract = "0x1234567890123456789012345678901234567890";

        private sealed class NoServices : System.IServiceProvider
        {
            public object GetService(System.Type serviceType) => null;
        }

        private sealed class FeeFreeDevChainConfig : DevChainConfig
        {
            public override bool FeeFreeBaseFee => true;
        }

        private static RpcContext ContextFor(DevChainNode node) => new RpcContext(node, chainId: ChainId, services: new NoServices());

        private static async Task<DevChainNode> StartedNodeAsync(DevChainConfig config)
        {
            config.ChainId = ChainId;
            var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync(new[] { Sender }, BigInteger.Parse("10000000000000000000000"));
            return node;
        }

        private static string Result(RpcResponseMessage response)
        {
            Assert.False(response.HasError, response.Error?.Message);
            return JsonConvert.SerializeObject(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_AChainWithABlock_When_EthBaseFeeIsAsked_Then_TheAnswerIsTheBaseFeeTheNextBlockMustCarry()
        {
            using var node = await StartedNodeAsync(new DevChainConfig());
            await node.MineBlockAsync();
            var latest = await node.GetLatestBlockAsync();
            var expected = (BigInteger)Nethereum.Model.BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                latest.BaseFee, latest.GasLimit, latest.GasUsed);

            var response = await new EthBaseFeeHandler().HandleAsync(new RpcRequestMessage(1, "eth_baseFee"), ContextFor(node));

            Assert.Equal("\"0x" + expected.ToString("x") + "\"", Result(response));
        }

        [Fact]
        public async Task Given_AFeeFreeChain_When_EthBaseFeeIsAsked_Then_TheAnswerIsZero()
        {
            using var node = await StartedNodeAsync(new FeeFreeDevChainConfig());
            await node.MineBlockAsync();

            var response = await new EthBaseFeeHandler().HandleAsync(new RpcRequestMessage(1, "eth_baseFee"), ContextFor(node));

            Assert.Equal("\"0x0\"", Result(response));
        }

        [Fact]
        public async Task Given_StoredSlots_When_EthGetStorageValuesIsAsked_Then_EachKeyAnswersAThirtyTwoByteWordInRequestOrderAndUnsetSlotsAreZero()
        {
            using var node = await StartedNodeAsync(new DevChainConfig());
            await node.State.SaveStorageAsync(Contract, 1, new byte[] { 0xAB, 0xCD });
            await node.State.SaveStorageAsync(Contract, 2, new byte[] { 0x07 });
            await node.MineBlockAsync();
            var requests = new Dictionary<string, string[]>
            {
                [Contract] = new[]
                {
                    "0x0000000000000000000000000000000000000000000000000000000000000002",
                    "0x1",
                    "0x9"
                }
            };

            var response = await new EthGetStorageValuesHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getStorageValues", requests, "latest"), ContextFor(node));

            var values = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(Result(response));
            Assert.Equal(new[]
            {
                "0x0000000000000000000000000000000000000000000000000000000000000007",
                "0x000000000000000000000000000000000000000000000000000000000000abcd",
                "0x0000000000000000000000000000000000000000000000000000000000000000"
            }, values.Single(v => v.Key.ToLowerInvariant() == Contract).Value);
        }

        [Fact]
        public async Task Given_AnEmptyRequestOrAKeyLongerThanThirtyTwoBytes_When_EthGetStorageValuesIsAsked_Then_TheAnswerIsInvalidParams()
        {
            using var node = await StartedNodeAsync(new DevChainConfig());
            var empty = new Dictionary<string, string[]> { [Contract] = new string[0] };
            var tooLong = new Dictionary<string, string[]> { [Contract] = new[] { "0x" + new string('1', 66) } };

            var emptyError = await Assert.ThrowsAsync<RpcException>(() => new EthGetStorageValuesHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getStorageValues", empty, "latest"), ContextFor(node)));
            var longError = await Assert.ThrowsAsync<RpcException>(() => new EthGetStorageValuesHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getStorageValues", tooLong, "latest"), ContextFor(node)));

            Assert.Equal(-32602, emptyError.Code);
            Assert.Equal(-32602, longError.Code);
        }

        [Fact]
        public async Task Given_MoreThanOneThousandAndTwentyFourSlots_When_EthGetStorageValuesIsAsked_Then_TheAnswerIsTheClientLimitError()
        {
            using var node = await StartedNodeAsync(new DevChainConfig());
            var keys = Enumerable.Range(0, 1025).Select(i => "0x" + i.ToString("x")).ToArray();
            var requests = new Dictionary<string, string[]> { [Contract] = keys };

            var error = await Assert.ThrowsAsync<RpcException>(() => new EthGetStorageValuesHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getStorageValues", requests, "latest"), ContextFor(node)));

            Assert.Equal(-38026, error.Code);
        }
    }
}
