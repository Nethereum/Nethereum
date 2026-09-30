using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateStateFeeFixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }

        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();

        public async Task DisposeAsync()
        {
            if (Chain != null) await Chain.DisposeAsync();
        }
    }

    [Collection(RpcCompatCollection.Name)]
    public class SimulateStateFeeTests : IClassFixture<SimulateStateFeeFixture>
    {
        private const string GethValidationFalseStateRoot =
            "0x147e5101af51b06bae5b89d574a38f6711ecb3582a465a9a4e2aa9e7a1dbc345";
        private const string GethValidationTrueStateRoot =
            "0x375829e5373817d26b84d5314208af86f54a32669d9a82495b3f11aa9e98ad22";

        private readonly SimulateStateFeeFixture _fixture;

        public SimulateStateFeeTests(SimulateStateFeeFixture fixture) => _fixture = fixture;

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

        private async Task<string> DispatchVectorStateRootAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            var blocks = await DispatchSimulateAsync(exchange.RequestJson);
            return ((string)blocks[0]["stateRoot"]).ToLowerInvariant();
        }

        private async Task AssertVectorMatchesGethAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            var result = await RpcCompatDriver.Instance.RunAsync(_fixture.Chain, exchange);
            Assert.True(result.Success, $"{vector} must match geth byte-for-byte: [{result.Kind}] {result.Detail}");
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-01")]
        public async Task Given_AValidationFalseTransferToExactBalance_When_TheBlockIsProduced_Then_TheStateRootMatchesGethAndNoUnlimitedBalancePersists()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-simple.io");
            Assert.Equal(GethValidationFalseStateRoot, await DispatchVectorStateRootAsync("ethSimulate-simple.io"));
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-01")]
        public async Task Given_TheUnlimitedBalanceTopUpIsRestored_When_ProducedOnValidationFalse_Then_TheStateRootDivergesFromGeth()
        {
            const string toppedUpRequest =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{\"blockStateCalls\":[{" +
                "\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":" +
                "{\"balance\":\"0x8000000000000000000000000000000000000000000000000000000000000000\"}}," +
                "\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"value\":\"0x3e8\"}," +
                "{\"from\":\"0xc100000000000000000000000000000000000000\",\"to\":\"0xc200000000000000000000000000000000000000\",\"value\":\"0x3e8\"}]}]},\"latest\"]}";

            var blocks = await DispatchSimulateAsync(toppedUpRequest);
            var toppedUpStateRoot = ((string)blocks[0]["stateRoot"]).ToLowerInvariant();

            Assert.NotEqual(GethValidationFalseStateRoot, toppedUpStateRoot);
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-02")]
        public async Task Given_AValidationTrueTransfer_When_TheBlockIsProduced_Then_TheGasFeeIsBurnedAndTheStateRootMatchesGeth()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-simple-validation-fulltx.io");
            Assert.Equal(GethValidationTrueStateRoot,
                await DispatchVectorStateRootAsync("ethSimulate-simple-validation-fulltx.io"));
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-02")]
        public async Task Given_ValidationFalseWithAFeeCapAtLeastBaseFee_When_Produced_Then_ItSettlesGasLikeValidationTrue()
        {
            const string validationFalseRequest =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{\"blockStateCalls\":[{" +
                "\"blockOverrides\":{\"baseFeePerGas\":\"0xf\"}," +
                "\"stateOverrides\":{\"0xc000000000000000000000000000000000000000\":{\"balance\":\"0xe8d4a51000\"}}," +
                "\"calls\":[" +
                "{\"from\":\"0xc000000000000000000000000000000000000000\",\"to\":\"0xc100000000000000000000000000000000000000\",\"maxFeePerGas\":\"0x10\",\"value\":\"0x2540be400\"}," +
                "{\"from\":\"0xc100000000000000000000000000000000000000\",\"to\":\"0xc200000000000000000000000000000000000000\",\"maxFeePerGas\":\"0x10\",\"value\":\"0x3e8\"}]}]," +
                "\"validation\":false,\"returnFullTransactions\":true},\"latest\"]}";

            var blocks = await DispatchSimulateAsync(validationFalseRequest);
            var settledStateRoot = ((string)blocks[0]["stateRoot"]).ToLowerInvariant();

            Assert.Equal(GethValidationTrueStateRoot, settledStateRoot);
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-02")]
        public async Task Given_SettleTransactionFeesIsSetToValidation_When_AValidationFalseCallHasANonZeroBaseFeeOverride_Then_TheVectorMatchesGethAndDoesNotThrow()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-basefee-too-low-without-validation-38012.io");
            await AssertVectorMatchesGethAsync("ethSimulate-basefee-too-low-without-validation-38012-without-basefee-override.io");
        }
    }
}
