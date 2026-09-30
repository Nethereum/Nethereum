using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateR2R4R5ErrorFixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }
        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();
        public async Task DisposeAsync() { if (Chain != null) await Chain.DisposeAsync(); }
    }

    [Collection(RpcCompatCollection.Name)]
    public class SimulateR2R4R5ErrorTests : IClassFixture<SimulateR2R4R5ErrorFixture>
    {
        private readonly SimulateR2R4R5ErrorFixture _fixture;
        public SimulateR2R4R5ErrorTests(SimulateR2R4R5ErrorFixture fixture) => _fixture = fixture;

        private static string VectorPath(string name) =>
            Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "eth_simulateV1", name);

        private async Task<ConformanceCaseResult> RunVectorAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            return await RpcCompatDriver.Instance.RunAsync(_fixture.Chain, exchange);
        }

        private async Task<JArray> DispatchAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            var parsed = System.Text.Json.JsonSerializer.Deserialize(
                exchange.RequestJson, CoreChainJsonContext.Default.JsonRpcRequest);
            var response = await _fixture.Chain.Dispatcher.DispatchAsync(parsed.ToRpcRequestMessage());
            Assert.False(response.HasError,
                $"{vector}: unexpected error {(response.HasError ? response.Error.Code : 0)}");
            var wire = System.Text.Json.JsonSerializer.Serialize(
                response.ToJsonRpcResponse(), CoreChainJsonContext.Default.Options);
            return (JArray)JObject.Parse(wire)["result"];
        }

        [Fact]
        [Trait("Rule", "SIM-REVERT-01")]
        public async Task Given_ACallThatReverts_When_Produced_Then_ErrorIsCode3ObjectWithReasonAndReturnDataCleared()
        {
            var result = await RunVectorAsync("ethSimulate-eth-send-should-not-produce-logs-on-revert.io");
            Assert.True(result.Success, $"revert vector must match geth: [{result.Kind}] {result.Detail}");

            var call = (await DispatchAsync("ethSimulate-eth-send-should-not-produce-logs-on-revert.io"))[0]["calls"][0];
            Assert.Equal("0x0", (string)call["status"]);
            Assert.Equal("0x", (string)call["returnData"]);
            var error = call["error"];
            Assert.Equal(3, (int)error["code"]);
            Assert.Equal("execution reverted: Always reverting contract", (string)error["message"]);
            Assert.StartsWith("0x08c379a0", (string)error["data"]);
        }

        [Fact]
        [Trait("Rule", "SIM-REVERT-01")]
        public async Task Given_ASuccessfulCall_When_Produced_Then_ErrorIsAbsent()
        {
            var call = (await DispatchAsync("ethSimulate-simple.io"))[0]["calls"][0];
            Assert.Equal("0x1", (string)call["status"]);
            Assert.Null(call["error"]);
        }

        [Fact]
        [Trait("Rule", "SIM-LOG-01")]
        public async Task Given_ASimulateThatEmitsLogs_When_Produced_Then_EachLogCarriesTheBlocksTxHashBlockHashAndTimestamp()
        {
            var block = (await DispatchAsync("ethSimulate-logs.io"))[0];
            var blockHash = (string)block["hash"];
            var blockTimestamp = (string)block["timestamp"];
            var firstTxHash = (string)block["transactions"][0];

            var log = block["calls"][0]["logs"][0];
            Assert.False(string.IsNullOrEmpty((string)log["blockHash"]));
            Assert.False(string.IsNullOrEmpty((string)log["transactionHash"]));
            Assert.False(string.IsNullOrEmpty((string)log["blockTimestamp"]));
            Assert.Equal(blockHash, (string)log["blockHash"]);
            Assert.Equal(blockTimestamp, (string)log["blockTimestamp"]);
            Assert.Equal(firstTxHash, (string)log["transactionHash"]);
        }

        [Fact]
        [Trait("Rule", "SIM-GAP-01")]
        public async Task Given_NonContiguousBlockNumbers_When_Simulated_Then_EmptyGapBlocksAreInserted()
        {
            var result = await RunVectorAsync("ethSimulate-blocknumber-increment.io");
            Assert.True(result.Success, $"gap-fill vector must match geth: [{result.Kind}] {result.Detail}");

            var blocks = await DispatchAsync("ethSimulate-blocknumber-increment.io");
            Assert.Equal(33, blocks.Count);
        }

        [Fact]
        [Trait("Rule", "SIM-GAP-01")]
        public async Task Given_ContiguousBlocks_When_Simulated_Then_NoExtraBlocksAreInserted()
        {
            var blocks = await DispatchAsync("ethSimulate-block-timestamps-incrementing.io");
            Assert.Equal(2, blocks.Count);
        }

        [Theory]
        [Trait("Rule", "SIM-ERR")]
        [InlineData("ethSimulate-block-num-order-38020.io")]
        [InlineData("ethSimulate-block-timestamp-order-38021.io")]
        [InlineData("ethSimulate-big-block-state-calls-array.io")]
        [InlineData("ethSimulate-make-call-with-future-block.io")]
        [InlineData("ethSimulate-try-to-move-non-precompile.io")]
        [InlineData("ethSimulate-simple-no-funds.io")]
        [InlineData("ethSimulate-instrict-gas-38013.io")]
        [InlineData("ethSimulate-basefee-too-low-with-validation-38012.io")]
        public async Task Given_AnInvalidSimulateInput_When_Simulated_Then_ItRaisesAnError(string vector)
        {
            var result = await RunVectorAsync(vector);
            Assert.True(result.Success, $"{vector} must raise an error the driver accepts: [{result.Kind}] {result.Detail}");
        }

        [Theory]
        [Trait("Rule", "SIM-ERR")]
        [InlineData("ethSimulate-basefee-too-low-without-validation-38012.io")]
        [InlineData("ethSimulate-transaction-too-low-nonce-38010.io")]
        public async Task Given_AValidSimulateInputResemblingAnError_When_Simulated_Then_ItReturnsAResult(string vector)
        {
            var result = await RunVectorAsync(vector);
            Assert.True(result.Success, $"{vector} must return a matching result, not an error: [{result.Kind}] {result.Detail}");
        }
    }
}
