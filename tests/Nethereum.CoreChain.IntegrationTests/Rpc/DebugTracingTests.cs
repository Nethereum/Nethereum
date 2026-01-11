using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class DebugTracingTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly RpcDispatcher _dispatcher;

        public DebugTracingTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();
            registry.AddDebugHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(_fixture.Node, _fixture.ChainId, services);
            _dispatcher = new RpcDispatcher(registry, context);
        }

        [Fact]
        public async Task DebugTraceTransaction_WithValidTx_ReturnsTrace()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, 100);
            Assert.True(result.Success);

            var txHash = result.TransactionHash.ToHex(true);
            var request = new RpcRequestMessage(1, "debug_traceTransaction", txHash);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var traceResponse = ParseTraceResponse(response.Result);
            Assert.NotNull(traceResponse.StructLogs);
            Assert.NotEmpty(traceResponse.StructLogs);
        }

        [Fact]
        public async Task DebugTraceTransaction_WithConfig_RespectsLimit()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, 100);
            Assert.True(result.Success);

            var txHash = result.TransactionHash.ToHex(true);
            var config = JObject.FromObject(new { limit = 5 });
            var request = new RpcRequestMessage(1, "debug_traceTransaction", txHash, config);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var traceResponse = ParseTraceResponse(response.Result);
            Assert.NotNull(traceResponse.StructLogs);
            Assert.True(traceResponse.StructLogs.Count <= 5);
        }

        [Fact]
        public async Task DebugTraceTransaction_NonExistentTx_ReturnsError()
        {
            var fakeTxHash = "0x0000000000000000000000000000000000000000000000000000000000000001";
            var request = new RpcRequestMessage(1, "debug_traceTransaction", fakeTxHash);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task DebugTraceCall_WithBasicCall_ReturnsTrace()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = "0x18160ddd" // totalSupply()
            });

            var request = new RpcRequestMessage(1, "debug_traceCall", callInput, "latest");
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var traceResponse = ParseTraceResponse(response.Result);
            Assert.NotNull(traceResponse.StructLogs);
        }

        [Fact]
        public async Task DebugTraceCall_WithConfig_RespectsOptions()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = "0x18160ddd" // totalSupply()
            });

            var config = JObject.FromObject(new
            {
                disableStack = true,
                disableStorage = true
            });

            var request = new RpcRequestMessage(1, "debug_traceCall", callInput, "latest", config);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var traceResponse = ParseTraceResponse(response.Result);
            Assert.NotNull(traceResponse.StructLogs);

            foreach (var log in traceResponse.StructLogs)
            {
                Assert.Null(log.Stack);
                Assert.Null(log.Storage);
            }
        }

        [Fact]
        public async Task DebugTraceCall_WithStateOverride_UsesOverriddenState()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = "0x18160ddd" // totalSupply()
            });

            var newBalance = "0x1000000000000000000";
            var config = JObject.FromObject(new
            {
                stateOverrides = new Dictionary<string, object>
                {
                    [_fixture.Address] = new { balance = newBalance }
                }
            });

            var request = new RpcRequestMessage(1, "debug_traceCall", callInput, "latest", config);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task DebugTraceCall_ToEOA_ReturnsEmptyTrace()
        {
            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = _fixture.RecipientAddress,
                value = "0x1"
            });

            var request = new RpcRequestMessage(1, "debug_traceCall", callInput, "latest");
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var traceResponse = ParseTraceResponse(response.Result);
            Assert.NotNull(traceResponse.StructLogs);
            Assert.Empty(traceResponse.StructLogs);
        }

        private static OpcodeTracerResponse ParseTraceResponse(object result)
        {
            string jsonString;
            if (result is JsonElement je)
            {
                jsonString = je.GetRawText();
            }
            else
            {
                jsonString = JsonConvert.SerializeObject(result);
            }
            return JsonConvert.DeserializeObject<OpcodeTracerResponse>(jsonString);
        }
    }
}
