using System.Linq;
using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class CreateAccessListTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly RpcDispatcher _dispatcher;
        private readonly RpcContext _context;

        public CreateAccessListTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            _context = new RpcContext(_fixture.Node, _fixture.ChainId, services);
            _dispatcher = new RpcDispatcher(registry, _context);
        }

        private static JObject ResultToJObject(object result)
        {
            if (result is JsonElement jsonElement)
            {
                return JObject.Parse(jsonElement.GetRawText());
            }
            return JObject.FromObject(result);
        }

        [Fact]
        public async Task CreateAccessList_SimpleTransfer_ReturnsEmptyAccessList()
        {
            var callInput = new
            {
                from = _fixture.Address,
                to = _fixture.RecipientAddress,
                value = "0xde0b6b3a7640000",
                data = "0x"
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = ResultToJObject(response.Result);
            var accessList = result["accessList"] as JArray;
            Assert.NotNull(accessList);
            Assert.Empty(accessList);
        }

        [Fact]
        public async Task CreateAccessList_ContractCall_CapturesStorageAccesses()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var balanceOfAbi = "0x70a08231000000000000000000000000" + _fixture.Address.RemoveHexPrefix().ToLower();

            var callInput = new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = balanceOfAbi
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = ResultToJObject(response.Result);
            Assert.NotNull(result["gasUsed"]);
        }

        [Fact]
        public async Task CreateAccessList_TransferCall_CapturesStorageAccesses()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var recipient = _fixture.RecipientAddress.RemoveHexPrefix().ToLower().PadLeft(64, '0');
            var amount = "0000000000000000000000000000000000000000000000000de0b6b3a7640000";
            var transferAbi = "0xa9059cbb" + recipient + amount;

            var callInput = new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = transferAbi
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = ResultToJObject(response.Result);
            Assert.Null(result["error"]?.Value<string>());
            Assert.NotNull(result["gasUsed"]);
        }

        [Fact]
        public async Task CreateAccessList_NonExistentContract_ReturnsEmptyAccessList()
        {
            var callInput = new
            {
                from = _fixture.Address,
                to = "0x1111111111111111111111111111111111111111",
                data = "0x70a08231"
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = ResultToJObject(response.Result);
            var accessList = result["accessList"] as JArray;
            Assert.NotNull(accessList);
            Assert.Empty(accessList);
        }

        [Fact]
        public async Task CreateAccessList_InvalidBlockParameter_ThrowsError()
        {
            var callInput = new
            {
                from = _fixture.Address,
                to = _fixture.RecipientAddress,
                value = "0x0"
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "earliest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("historical state queries not supported", response.Error.Message);
        }

        [Fact]
        public async Task CreateAccessList_ReturnsGasUsed()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var balanceOfAbi = "0x70a08231000000000000000000000000" + _fixture.Address.RemoveHexPrefix().ToLower();

            var callInput = new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = balanceOfAbi
            };

            var request = new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = ResultToJObject(response.Result);

            var gasUsedStr = result["gasUsed"]?.Value<string>();
            Assert.NotNull(gasUsedStr);
            Assert.True(gasUsedStr.StartsWith("0x"));
            var gasUsed = gasUsedStr.HexToBigInteger(false);
            Assert.True(gasUsed > 0);
        }
    }
}
