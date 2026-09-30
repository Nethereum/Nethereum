using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateResidualsFixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }
        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();
        public async Task DisposeAsync() { if (Chain != null) await Chain.DisposeAsync(); }
    }

    [Collection(RpcCompatCollection.Name)]
    public class SimulateResidualsTests : IClassFixture<SimulateResidualsFixture>
    {
        private readonly SimulateResidualsFixture _fixture;
        public SimulateResidualsTests(SimulateResidualsFixture fixture) => _fixture = fixture;

        private static string VectorPath(string name) =>
            Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "eth_simulateV1", name);

        private async Task AssertVectorMatchesGethAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            var result = await RpcCompatDriver.Instance.RunAsync(_fixture.Chain, exchange);
            Assert.True(result.Success, $"{vector} must match geth byte-for-byte: [{result.Kind}] {result.Detail}");
        }

        private async Task<JArray> DispatchAsync(string requestJson)
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize(requestJson, CoreChainJsonContext.Default.JsonRpcRequest);
            var response = await _fixture.Chain.Dispatcher.DispatchAsync(parsed.ToRpcRequestMessage());
            Assert.False(response.HasError, response.HasError ? $"{response.Error.Code}: {response.Error.Message}" : "");
            var wire = System.Text.Json.JsonSerializer.Serialize(response.ToJsonRpcResponse(), CoreChainJsonContext.Default.Options);
            return (JArray)JObject.Parse(wire)["result"];
        }

        [Fact]
        [Trait("Rule", "SIM-NONCE-01")]
        public async Task Given_RepeatedSenderOmittingNonce_When_Simulated_Then_TheEncodedNonceIsTheRunningNonceAcrossBlocks()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-transfer-over-BlockStateCalls.io");

            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-check-that-balance-is-there-after-new-block.io")).Single();
            var req = JObject.Parse(exchange.RequestJson);
            ((JObject)req["params"][0])["returnFullTransactions"] = true;
            var blocks = await DispatchAsync(req.ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("0x0", (string)blocks[0]["transactions"][0]["nonce"]);
            Assert.Equal("0x2", (string)blocks[0]["transactions"][2]["nonce"]);
            Assert.Equal("0x3", (string)blocks[1]["transactions"][0]["nonce"]);
            Assert.Equal("0x5", (string)blocks[1]["transactions"][2]["nonce"]);
        }

        [Fact]
        [Trait("Rule", "SIM-GAS-03")]
        public async Task Given_ANoGasCallInALaterBlock_When_Simulated_Then_ItsGasIsTheGlobalRpcCapMinusEveryPriorBlocksGas()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-check-that-balance-is-there-after-new-block.io");

            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-check-that-balance-is-there-after-new-block.io")).Single();
            var req = JObject.Parse(exchange.RequestJson);
            ((JObject)req["params"][0])["returnFullTransactions"] = true;
            var blocks = await DispatchAsync(req.ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("0x2faf080", (string)blocks[0]["transactions"][0]["gas"]);
            Assert.Equal("0x2f9e94e", (string)blocks[1]["transactions"][0]["gas"]);
        }

        [Fact]
        [Trait("Rule", "SIM-OOG-01")]
        public async Task Given_AMultiBlockRunOutOfGas_When_Simulated_Then_TheOogCallErrorsWithCodeMinus32015AndTheGasLimitOverrideCarriesForward()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-run-out-of-gas-in-block-38015.io");

            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-run-out-of-gas-in-block-38015.io")).Single();
            var blocks = await DispatchAsync(exchange.RequestJson);
            var call1 = blocks[1]["calls"][1];
            Assert.Equal("0x0", (string)call1["status"]);
            Assert.Equal(-32015, (int)call1["error"]["code"]);
            Assert.Equal("out of gas", (string)call1["error"]["message"]);
            Assert.Equal("0x16e360", (string)blocks[1]["gasLimit"]);
            Assert.Null(blocks[1]["calls"][0]["error"]);
        }

        [Fact]
        [Trait("Rule", "SIM-STATE-04")]
        public async Task Given_ValidationFalseCallsWithANonZeroFee_When_Simulated_Then_TheGasIsChargedAndTheStateRootMatchesGeth()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-two-blocks-with-complete-eth-sends.io");
            await AssertVectorMatchesGethAsync("ethSimulate-basefee-too-low-without-validation-38012.io");
        }

        [Fact]
        [Trait("Rule", "SIM-INPUT-01")]
        public async Task Given_ACallSuppliedViaInputField_When_Simulated_Then_TheCalldataAndItsIntrinsicGasAreCounted()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-logs.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-logs.io")).Single().RequestJson);
            Assert.Equal("0x55d4", (string)blocks[0]["calls"][0]["gasUsed"]);
        }

        [Fact]
        [Trait("Rule", "SIM-LOG-02")]
        public async Task Given_TraceTransfers_When_Simulated_Then_TheTransferLogIsInTheCallButExcludedFromTheBlockBloomAndReceiptsRoot()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-eth-send-should-produce-logs.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-eth-send-should-produce-logs.io")).Single().RequestJson);
            var log = blocks[0]["calls"][0]["logs"][0];
            Assert.Equal("0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", (string)log["address"]);
            Assert.Matches("^0x0+$", (string)blocks[0]["logsBloom"]);
        }

        [Fact]
        [Trait("Rule", "SIM-RO-04")]
        public async Task Given_ASelfDestructThenAnUnrelatedSimulate_When_RunOnTheSameChain_Then_TheSecondIsUncontaminated()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-self-destructive-contract-produces-logs.io");
            await AssertVectorMatchesGethAsync("ethSimulate-transfer-over-BlockStateCalls.io");
            await AssertVectorMatchesGethAsync("ethSimulate-two-blocks-with-complete-eth-sends.io");
        }

        // ---- R-FEERECIPIENT: validation:true credits the fee recipient and gap-fills with EIP-1559 base fees.
        [Fact]
        [Trait("Rule", "SIM-FEE-01")]
        public async Task Given_AFeeRecipientOverrideWithABlockNumberGap_When_Simulated_Then_TheGapBlocksAndCoinbaseSettlementMatchGeth()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-fee-recipient-receiving-funds.io");
        }
    }
}
