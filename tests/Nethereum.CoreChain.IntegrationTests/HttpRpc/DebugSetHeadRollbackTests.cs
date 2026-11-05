using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("DebugSetHead")]
    public class DebugSetHeadRollbackTests : IClassFixture<DebugSetHeadHttpFixture>
    {
        private readonly DebugSetHeadHttpFixture _fixture;
        private static readonly HttpClient Client = new HttpClient();
        private static int _nextId = 1;

        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        public DebugSetHeadRollbackTests(DebugSetHeadHttpFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly struct RpcHttpResult
        {
            public RpcHttpResult(HttpStatusCode status, JsonElement body)
            {
                Status = status;
                Body = body;
            }

            public HttpStatusCode Status { get; }
            public JsonElement Body { get; }
        }

        private static async Task<RpcHttpResult> PostAsync(string url, string method, object[] parameters, string? bearerToken)
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = _nextId++,
                method,
                @params = parameters
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            if (bearerToken != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            }

            using var response = await Client.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            var body = string.IsNullOrWhiteSpace(responseJson)
                ? default
                : JsonDocument.Parse(responseJson).RootElement.Clone();

            return new RpcHttpResult(response.StatusCode, body);
        }

        private Task<RpcHttpResult> PostEngineAsync(string method, object[] parameters) =>
            PostAsync(_fixture.EngineUrl, method, parameters, _fixture.MintValidToken());

        private Task<RpcHttpResult> PostPlainAsync(string method, object[] parameters) =>
            PostAsync(_fixture.Url, method, parameters, null);

        private static BigInteger HexToBigInteger(string hex) => hex.HexToBigInteger(false);

        private async Task<(string BlockHash, string StateRoot)> BuildAndAdoptBlockOnGenesisAsync(
            string genesisHash, long genesisTimestamp)
        {
            var parentBeaconBlockRoot = "0x" + new string('0', 64);
            var attributes = new
            {
                timestamp = "0x" + (genesisTimestamp + 1).ToString("x"),
                prevRandao = "0x" + new string('7', 64),
                suggestedFeeRecipient = "0x0000000000000000000000000000000000009999",
                withdrawals = Array.Empty<object>(),
                parentBeaconBlockRoot
            };

            var forkchoiceState = new
            {
                headBlockHash = genesisHash,
                safeBlockHash = genesisHash,
                finalizedBlockHash = genesisHash
            };

            var fcuResult = await PostEngineAsync("engine_forkchoiceUpdatedV3", new object[] { forkchoiceState, attributes });
            Assert.Equal(HttpStatusCode.OK, fcuResult.Status);
            var fcuResponse = fcuResult.Body.GetProperty("result");
            Assert.Equal("VALID", fcuResponse.GetProperty("payloadStatus").GetProperty("status").GetString());
            var payloadId = fcuResponse.GetProperty("payloadId").GetString();
            Assert.False(string.IsNullOrEmpty(payloadId));

            var getPayloadResult = await PostEngineAsync("engine_getPayloadV3", new object[] { payloadId! });
            Assert.Equal(HttpStatusCode.OK, getPayloadResult.Status);
            var payload = getPayloadResult.Body.GetProperty("result");
            Assert.Equal(genesisHash, payload.GetProperty("parentHash").GetString());

            var blockHash = payload.GetProperty("blockHash").GetString()!;
            var stateRoot = payload.GetProperty("stateRoot").GetString()!;
            var payloadForNewPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;

            var newPayloadResult = await PostEngineAsync(
                "engine_newPayloadV3",
                new object[] { payloadForNewPayload, Array.Empty<object>(), parentBeaconBlockRoot });
            Assert.Equal(HttpStatusCode.OK, newPayloadResult.Status);
            var newPayloadResponse = newPayloadResult.Body.GetProperty("result");
            Assert.Equal("VALID", newPayloadResponse.GetProperty("status").GetString());
            Assert.Equal(blockHash, newPayloadResponse.GetProperty("latestValidHash").GetString());

            var adoptForkchoiceState = new
            {
                headBlockHash = blockHash,
                safeBlockHash = blockHash,
                finalizedBlockHash = blockHash
            };

            var adoptResult = await PostEngineAsync("engine_forkchoiceUpdatedV3", new object[] { adoptForkchoiceState, null! });
            Assert.Equal(HttpStatusCode.OK, adoptResult.Status);
            var adoptResponse = adoptResult.Body.GetProperty("result");
            Assert.Equal("VALID", adoptResponse.GetProperty("payloadStatus").GetProperty("status").GetString());

            return (blockHash, stateRoot);
        }

        [Fact]
        public async Task Given_ABlockBuiltOnGenesis_When_DebugSetHeadZeroIsCalled_Then_TheNodeBehavesAsFreshGenesisForTheNextBlock()
        {
            var genesisResult = await PostPlainAsync("eth_getBlockByNumber", new object[] { "0x0", false });
            var genesisBlock = genesisResult.Body.GetProperty("result");
            var genesisHash = genesisBlock.GetProperty("hash").GetString()!;
            var genesisStateRoot = genesisBlock.GetProperty("stateRoot").GetString()!;
            var genesisTimestamp = Convert.ToInt64(genesisBlock.GetProperty("timestamp").GetString(), 16);

            var blockNumberAtStart = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
            Assert.Equal(BigInteger.Zero, HexToBigInteger(blockNumberAtStart.Body.GetProperty("result").GetString()!));

            var nonceAtGenesisResult = await PostPlainAsync("eth_getTransactionCount", new object[] { SenderAddress, "latest" });
            var nonceAtGenesis = HexToBigInteger(nonceAtGenesisResult.Body.GetProperty("result").GetString()!);

            var (blockHash1, stateRoot1) = await BuildAndAdoptBlockOnGenesisAsync(genesisHash, genesisTimestamp);
            Assert.NotEqual(genesisStateRoot, stateRoot1);

            var blockNumberAfterFirstBlock = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
            Assert.Equal(BigInteger.One, HexToBigInteger(blockNumberAfterFirstBlock.Body.GetProperty("result").GetString()!));

            var latestAfterFirstBlockResult = await PostPlainAsync("eth_getBlockByNumber", new object[] { "latest", false });
            Assert.Equal(stateRoot1, latestAfterFirstBlockResult.Body.GetProperty("result").GetProperty("stateRoot").GetString());

            var setHeadResult = await PostPlainAsync("debug_setHead", new object[] { "0x0" });
            Assert.Equal(HttpStatusCode.OK, setHeadResult.Status);
            Assert.False(setHeadResult.Body.TryGetProperty("error", out _), setHeadResult.Body.GetRawText());

            var blockNumberAfterSetHead = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
            Assert.Equal(BigInteger.Zero, HexToBigInteger(blockNumberAfterSetHead.Body.GetProperty("result").GetString()!));

            var latestAfterSetHeadResult = await PostPlainAsync("eth_getBlockByNumber", new object[] { "latest", false });
            var latestAfterSetHead = latestAfterSetHeadResult.Body.GetProperty("result");
            Assert.Equal(genesisHash, latestAfterSetHead.GetProperty("hash").GetString());
            Assert.Equal(genesisStateRoot, latestAfterSetHead.GetProperty("stateRoot").GetString());

            var nonceAfterSetHeadResult = await PostPlainAsync("eth_getTransactionCount", new object[] { SenderAddress, "latest" });
            Assert.Equal(nonceAtGenesis, HexToBigInteger(nonceAfterSetHeadResult.Body.GetProperty("result").GetString()!));

            var (blockHash2, stateRoot2) = await BuildAndAdoptBlockOnGenesisAsync(genesisHash, genesisTimestamp);

            var blockNumberAfterSecondBlock = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
            Assert.Equal(BigInteger.One, HexToBigInteger(blockNumberAfterSecondBlock.Body.GetProperty("result").GetString()!));

            Assert.Equal(blockHash1, blockHash2);
            Assert.Equal(stateRoot1, stateRoot2);
        }
    }
}
