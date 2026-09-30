using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class SimulateFullTxProjectionFixture : IAsyncLifetime
    {
        public RpcReferenceChain Chain { get; private set; }

        public async Task InitializeAsync() => Chain = await RpcReferenceChain.CreateAsync();

        public async Task DisposeAsync()
        {
            if (Chain != null) await Chain.DisposeAsync();
        }
    }

    [Collection(RpcCompatCollection.Name)]
    public class SimulateFullTxProjectionTests : IClassFixture<SimulateFullTxProjectionFixture>
    {
        private const string GethTx0Hash = "0xc2df7444d2b0604cdfaa20cad5855422b575d8d2ec02868ed9eb45eb20490f66";
        private const string GethTx1Hash = "0x5c78719d6ce3bf5b515e9fc3a41ab755c007645928bf35eba1a318721b774595";
        private const string GethTransactionsRoot = "0x9e80508f974bbf713b286fe5a4d0fae2f54f17e33b742176cc4c5bb7e0942762";
        private const string NonCanonicalNullFieldHash = "0x3e413f552f0cfc8888b3d71a91034512bd829de7e46c51c1644f9ff0b4509ca3";

        private const string SenderC000 = "0xc000000000000000000000000000000000000000";
        private const string SenderC100 = "0xc100000000000000000000000000000000000000";

        private readonly SimulateFullTxProjectionFixture _fixture;

        public SimulateFullTxProjectionTests(SimulateFullTxProjectionFixture fixture) => _fixture = fixture;

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

        private async Task<JObject> DispatchAsync(string requestJson)
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize(
                requestJson, CoreChainJsonContext.Default.JsonRpcRequest);
            var response = await _fixture.Chain.Dispatcher
                .DispatchAsync(parsed.ToRpcRequestMessage()).ConfigureAwait(false);

            Assert.False(response.HasError,
                $"request returned error {(response.HasError ? response.Error.Code : 0)}: " +
                $"{(response.HasError ? response.Error.Message : "")}");

            var wire = System.Text.Json.JsonSerializer.Serialize(
                response.ToJsonRpcResponse(), CoreChainJsonContext.Default.Options);
            return (JObject)JObject.Parse(wire)["result"];
        }

        private async Task AssertVectorMatchesGethAsync(string vector)
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath(vector)).Single();
            var result = await RpcCompatDriver.Instance.RunAsync(_fixture.Chain, exchange);
            Assert.True(result.Success, $"{vector} must match geth byte-for-byte: [{result.Kind}] {result.Detail}");
        }

        [Fact]
        [Trait("Rule", "SIM-TX-01")]
        public async Task Given_ACallOmittingMaxPriorityFee_When_TheSyntheticTxIsEncoded_Then_TheZeroFieldIsCanonicalEmptyRlpAndTheHashMatchesGeth()
        {
            await AssertVectorMatchesGethAsync("ethSimulate-simple-validation-fulltx.io");

            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-simple-validation-fulltx.io")).Single();
            var blocks = await DispatchSimulateAsync(exchange.RequestJson);
            var txs = (JArray)blocks[0]["transactions"];

            Assert.Equal(GethTx0Hash, ((string)txs[0]["hash"]).ToLowerInvariant());
            Assert.Equal(GethTx1Hash, ((string)txs[1]["hash"]).ToLowerInvariant());
            Assert.Equal(GethTransactionsRoot, ((string)blocks[0]["transactionsRoot"]).ToLowerInvariant());
        }

        [Fact]
        [Trait("Rule", "SIM-TX-01")]
        public void Given_TheNullFieldFallbackIsRestored_When_Encoded_Then_TheHashIsTheNonCanonical0x3e413f55()
        {
            var canonicalZeroTx = BuildFulltxTx0(coalesceOmittedToZero: true);
            var nonCanonicalNullTx = BuildFulltxTx0(coalesceOmittedToZero: false);

            Assert.Equal(GethTx0Hash, canonicalZeroTx.Hash.ToHex(true));
            Assert.Equal(NonCanonicalNullFieldHash, nonCanonicalNullTx.Hash.ToHex(true));
            Assert.NotEqual(canonicalZeroTx.Hash.ToHex(true), nonCanonicalNullTx.Hash.ToHex(true));
        }

        private static Transaction1559 BuildFulltxTx0(bool coalesceOmittedToZero)
        {
            var chainId = (EvmUInt256)new HexBigInteger("0xc72dd9d5e883e").Value;
            EvmUInt256? omittedNonce = coalesceOmittedToZero ? EvmUInt256.Zero : (EvmUInt256?)null;
            EvmUInt256? omittedPriorityFee = coalesceOmittedToZero ? EvmUInt256.Zero : (EvmUInt256?)null;

            return new Transaction1559(
                chainId: chainId,
                nonce: omittedNonce,
                maxPriorityFeePerGas: omittedPriorityFee,
                maxFeePerGas: (EvmUInt256)new HexBigInteger("0x10").Value,
                gasLimit: (EvmUInt256)new HexBigInteger("0x2faf080").Value,
                receiverAddress: SenderC100,
                amount: (EvmUInt256)new HexBigInteger("0x2540be400").Value,
                data: "0x",
                accessList: null);
        }

        [Fact]
        [Trait("Rule", "SIM-TX-02")]
        public async Task Given_ReturnFullTransactions_When_ASignatureLessSimulateTxIsProjected_Then_FromIsTheSpoofedSenderAndRsYParityAreZero()
        {
            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-simple-validation-fulltx.io")).Single();
            var blocks = await DispatchSimulateAsync(exchange.RequestJson);
            var txs = (JArray)blocks[0]["transactions"];

            Assert.Equal(SenderC000, ((string)txs[0]["from"]).ToLowerInvariant());
            Assert.Equal(SenderC100, ((string)txs[1]["from"]).ToLowerInvariant());

            foreach (var tx in txs)
            {
                Assert.Equal("0x0", (string)tx["r"]);
                Assert.Equal("0x0", (string)tx["s"]);
                Assert.Equal("0x0", (string)tx["yParity"]);
                Assert.Equal("0x0", (string)tx["v"]);
            }
        }

        [Fact]
        [Trait("Rule", "SIM-TX-02")]
        public async Task Given_ARealSignedTxInAGetBlockResponse_When_Projected_Then_RsYParityAreTheRealSignatureNotZero()
        {
            const string request =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getTransactionByHash\",\"params\":[" +
                "\"0x0d6999c0e9e4bec347945593e97bdcdf7c25be08ca1a1efdc520dbe75be985f3\"]}";

            var tx = await DispatchAsync(request);

            Assert.Equal("0x7435ed30a8b4aeb0877cef0c6e8cffe834eb865f", ((string)tx["from"]).ToLowerInvariant());
            Assert.Equal("0x1b", (string)tx["v"]);
            Assert.Equal("0xfad01b9a15db73d59b0a74b3d85718dfebb7cde80f916840a006ef5f2a85769e", ((string)tx["r"]).ToLowerInvariant());
            Assert.Equal("0x60b08e6b12604d91c6e2c514f2c020562b61f97973ada18a1701937d3302e5f3", ((string)tx["s"]).ToLowerInvariant());
            Assert.NotEqual("0x0", (string)tx["r"]);
            Assert.NotEqual("0x0", (string)tx["s"]);
        }
    }
}
