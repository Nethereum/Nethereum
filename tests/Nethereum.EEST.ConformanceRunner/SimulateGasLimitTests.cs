using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateGasLimitFixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }

        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();

        public async Task DisposeAsync()
        {
            if (Chain != null) await Chain.DisposeAsync();
        }
    }

    [Collection(RpcCompatCollection.Name)]
    public class SimulateGasLimitTests : IClassFixture<SimulateGasLimitFixture>
    {
        private readonly SimulateGasLimitFixture _fixture;

        public SimulateGasLimitTests(SimulateGasLimitFixture fixture) => _fixture = fixture;

        private static string VectorPath(string name) =>
            Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "eth_simulateV1", name);

        private async Task<JArray> DispatchSimulateAsync(string requestJson)
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize(
                requestJson, CoreChainJsonContext.Default.JsonRpcRequest);
            var response = await _fixture.Chain.Dispatcher
                .DispatchAsync(parsed.ToRpcRequestMessage()).ConfigureAwait(false);

            Assert.False(response.HasError,
                $"simulate returned error {(response.HasError ? response.Error.Code : 0)}: " +
                $"{(response.HasError ? response.Error.Message : "")}");

            var wire = System.Text.Json.JsonSerializer.Serialize(
                response.ToJsonRpcResponse(), CoreChainJsonContext.Default.Options);
            return (JArray)JObject.Parse(wire)["result"];
        }

        private async Task<JArray> DispatchVectorRequestAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            return await DispatchSimulateAsync(exchange.RequestJson);
        }

        private static string TxGas(JArray blocks, int block, int tx) =>
            (string)((JArray)blocks[block]["transactions"])[tx]["gas"];

        [Fact]
        [Trait("Rule", "SIM-GAS-01")]
        public async Task Given_ATwoCallSimulateBlockWithNoExplicitGas_When_TheBlockIsProduced_Then_EachSyntheticTxGasIsPoolStartMinusCumulative()
        {
            var blocks = await DispatchVectorRequestAsync("ethSimulate-simple-validation-fulltx.io");

            Assert.Equal("0x2faf080", TxGas(blocks, 0, 0));
            Assert.Equal("0x2fa9e78", TxGas(blocks, 0, 1));
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-01")]
        public async Task Given_ASimulateCallStatingItsOwnGas_When_TheBlockIsProduced_Then_TheSyntheticTxKeepsThatExactGas()
        {
            var blocks = await DispatchVectorRequestAsync("ethSimulate-simple-more-params-validate.io");

            Assert.Equal("0x52080", TxGas(blocks, 0, 0));
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-01")]
        public async Task Given_AMultiBlockNoGasSimulate_When_EachBlockIsProduced_Then_TheRpcGasCapPoolDepletesGloballyAcrossBlocks()
        {
            const string request =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{" +
                "\"blockStateCalls\":[" +
                "{\"blockOverrides\":{\"baseFeePerGas\":\"0x0\"},\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":{\"balance\":\"0x3e8\"}},\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}]}," +
                "{\"blockOverrides\":{\"baseFeePerGas\":\"0x0\"},\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":{\"balance\":\"0x3e8\"}},\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}]}]," +
                "\"returnFullTransactions\":true}]}";

            var blocks = await DispatchSimulateAsync(request);

            Assert.Equal("0x2faf080", TxGas(blocks, 0, 0));
            Assert.Equal("0x2fa9e78", TxGas(blocks, 0, 1));
            Assert.Equal("0x2fa4c70", TxGas(blocks, 1, 0));
            Assert.Equal("0x2f9fa68", TxGas(blocks, 1, 1));
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-01")]
        public async Task Given_ABlockGasLimitBelowRpcGasCap_When_ANoGasCallIsResolved_Then_PoolStartIsTheBlockGasLimit()
        {
            const string request =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{" +
                "\"blockStateCalls\":[{" +
                "\"blockOverrides\":{\"gasLimit\":\"0x16e360\",\"baseFeePerGas\":\"0x0\"},\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":{\"balance\":\"0x3e8\"}},\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}]}]," +
                "\"returnFullTransactions\":true}]}";

            var blocks = await DispatchSimulateAsync(request);

            Assert.Equal("0x16e360", TxGas(blocks, 0, 0));
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-02")]
        public async Task Given_ABlockWhoseExplicitGasCallsExceedPoolStartBeforeATrailingNoGasCall_When_TheBlockIsProduced_Then_TheSimulateRaisesAnError()
        {
            const string request =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{" +
                "\"blockStateCalls\":[{" +
                "\"blockOverrides\":{\"gasLimit\":\"0xdac0\",\"baseFeePerGas\":\"0x0\"}," +
                "\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"gas\":\"0x5208\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"gas\":\"0x5208\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"gas\":\"0x5208\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}" +
                "]}]," +
                "\"returnFullTransactions\":true}]}";

            var parsed = System.Text.Json.JsonSerializer.Deserialize(
                request, CoreChainJsonContext.Default.JsonRpcRequest);
            var response = await _fixture.Chain.Dispatcher
                .DispatchAsync(parsed.ToRpcRequestMessage()).ConfigureAwait(false);

            Assert.True(response.HasError, "pool-exhausted / unaffordable simulate must raise an error, not produce a block");
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-01")]
        public async Task Given_ANoGasCallAfterAnExplicitGasCall_When_TheBlockIsProduced_Then_ItGetsTheRemainingPoolAndTheBlockIsProduced()
        {
            const string request =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{" +
                "\"blockStateCalls\":[{" +
                "\"blockOverrides\":{\"baseFeePerGas\":\"0x0\"}," +
                "\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":{\"balance\":\"0x3e8\"}}," +
                "\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"gas\":\"0x5208\",\"value\":\"0x1\"}," +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x1\"}" +
                "]}]," +
                "\"returnFullTransactions\":true}]}";

            var blocks = await DispatchSimulateAsync(request);

            Assert.Equal("0x5208", TxGas(blocks, 0, 0));
            Assert.Equal("0x2fa9e78", TxGas(blocks, 0, 1));
        }
    }
}
