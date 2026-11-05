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
    [Trait("Requires", "Externals")]
    public class SimulateStateFeeTests : IClassFixture<SimulateStateFeeFixture>
    {
        private const string GethValidationFalseStateRoot =
            "0x026d07e9a1fb2c8e737ede84b4d70fd2c3be2872646b9ff1d13c215eb81354e5";
        private const string GethValidationTrueStateRoot =
            "0x9779f0b3e1d8059dc2c74634288ba0d5e68794d7dededc67025a3af544603b9b";

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
