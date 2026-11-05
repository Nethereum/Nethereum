using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateResiduals2Fixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }
        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();
        public async Task DisposeAsync() { if (Chain != null) await Chain.DisposeAsync(); }
    }

    [Collection(RpcCompatCollection.Name)]
    [Trait("Requires", "Externals")]
    public class SimulateResiduals2Tests : IClassFixture<SimulateResiduals2Fixture>
    {
        private readonly SimulateResiduals2Fixture _fixture;
        public SimulateResiduals2Tests(SimulateResiduals2Fixture fixture) => _fixture = fixture;

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
        [Trait("Rule", "SIM2-PRECOMPILE-MOVE")]
        public async Task Given_APrecompileMovedToANewAddress_When_Called_Then_TheNewAddressRunsItAndTheOriginalIsAPlainAccount()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-move-ecrecover-and-call.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-move-ecrecover-and-call.io")).Single().RequestJson);
            var moved = blocks[1]["calls"];
            Assert.Contains("b11cad98ad3f8114e0b3a1f6e7228bc8424df48a", ((string)moved[1]["returnData"]));
            Assert.Equal("0x", (string)moved[2]["returnData"]);
        }

        [Fact]
        [Trait("Rule", "SIM2-PRECOMPILE-MOVE")]
        public async Task Given_IdentityMovedAndTheOriginalCodeOverridden_When_Called_Then_OnlyTheNewAddressEchoes()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-override-identity.io");

            var calls = (await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-override-identity.io")).Single().RequestJson))[0]["calls"];
            Assert.Equal("0x1234", (string)calls[0]["returnData"]);
            Assert.Equal("0x", (string)calls[1]["returnData"]);
        }

        [Fact]
        [Trait("Rule", "SIM2-PRECOMPILE-MOVE")]
        public async Task Given_APrecompileMovedInEachOfTwoBlocks_When_Called_Then_EachBlocksMoveIsIndependentOfThePrior()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-move-ecrecover-twice-and-call.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-move-ecrecover-twice-and-call.io")).Single().RequestJson);
            var b3 = blocks[2]["calls"];
            Assert.Equal("0x", (string)b3[0]["returnData"]);
            Assert.Contains("b11cad98ad3f8114e0b3a1f6e7228bc8424df48a", (string)b3[3]["returnData"]);
        }

        [Fact]
        [Trait("Rule", "SIM2-PRECOMPILE-MOVE")]
        public async Task Given_MoveOnlyOverridesAndNoCalls_When_Simulated_Then_TheBlockIsIdenticalToAPlainEmptyBlock()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-move-two-accounts-to-same-38023.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-move-two-accounts-to-same-38023.io")).Single().RequestJson);
            Assert.Equal("0x3fbf6ce5817de3f70675b5dd105def1c5c2081bb4d46273eef744e7bbb78ea97", (string)blocks[0]["hash"]);
        }

        [Fact]
        [Trait("Rule", "SIM2-BASEFEE-ZERO")]
        public async Task Given_ValidationOff_When_ContractReadsBASEFEE_Then_ItReportsZeroNotOne()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-get-block-properties.io");

            var calls = (await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-get-block-properties.io")).Single().RequestJson))[0]["calls"];
            var returnData = (string)calls[0]["returnData"];
            var baseFeeWord = returnData.Substring(2, 64);
            Assert.Equal(new string('0', 64), baseFeeWord);
        }

        [Fact]
        [Trait("Rule", "SIM2-BLOCKHASH-SIM")]
        public async Task Given_ABlockhashOfAPreviouslySimulatedBlock_When_Read_Then_ItReturnsThatSimulatedBlocksHash()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-blockhash-start-before-head.io");

            var blocks = await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-blockhash-start-before-head.io")).Single().RequestJson);
            var b3b = blocks.Single(b => (string)b["number"] == "0x3b")["calls"];
            var b3a = blocks.Single(b => (string)b["number"] == "0x3a");
            Assert.Equal((string)b3a["hash"], (string)b3b[0]["returnData"]);
            Assert.NotEqual(new string('0', 64), ((string)b3b[0]["returnData"]).Substring(2));
        }

        [Fact]
        [Trait("Rule", "SIM2-NONCE-MAX")]
        public async Task Given_ASenderAtMaxNonce_When_ValidationOff_Then_TheCallsExecute_And_ValidationOnStillRejects()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-overflow-nonce.io");

            var calls = (await DispatchAsync(RpcCompatVectorLoader.Load(VectorPath("ethSimulate-overflow-nonce.io")).Single().RequestJson))[0]["calls"];
            Assert.Equal("0x1", (string)calls[0]["status"]);
            Assert.Equal("0x5208", (string)calls[0]["gasUsed"]);
            Assert.Equal("0x1", (string)calls[1]["status"]);

            await AssertVectorMatchesGethAsync("ethSimulate-overflow-nonce-validation.io");
        }
        [Fact]
        [Trait("Rule", "SIM2-LOGINDEX-BLOCK-ORDINAL")]
        public async Task Given_ARevertedChildFrameThatLogged_When_TheParentLogsAfterIt_Then_TheKeptLogTakesTheNextBlockOrdinalAndTheNextCallContinuesIt()
        {
            const string request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_simulateV1\",\"params\":[{\"blockStateCalls\":[{"
                + "\"stateOverrides\":{"
                + "\"0xc1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1\":{\"code\":\"0x60006000a060006000fd\"},"
                + "\"0xd2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2\":{\"code\":\"0x6000600060006000600073c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c161fffff15060006000a000\"}},"
                + "\"calls\":[{\"from\":\"0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"to\":\"0xd2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2\"},"
                + "{\"from\":\"0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"to\":\"0xd2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2\"}]}]},\"latest\"]}";

            var blocks = await DispatchAsync(request);

            var calls = blocks[0]["calls"];
            var first = (JArray)calls[0]["logs"];
            var second = (JArray)calls[1]["logs"];
            Assert.Single(first);
            Assert.Single(second);
            Assert.Equal("0x1", (string)first[0]["logIndex"]);
            Assert.Equal("0x3", (string)second[0]["logIndex"]);
        }
    }
}
