using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("EngineApi")]
    public class HarnessGenesisJsonEngineApiRoundTripTests : IClassFixture<HarnessGenesisJsonFixture>
    {
        private readonly HarnessGenesisJsonFixture _fixture;
        private static readonly HttpClient Client = new HttpClient();
        private static int _nextId = 1;

        public HarnessGenesisJsonEngineApiRoundTripTests(HarnessGenesisJsonFixture fixture)
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

        [Fact]
        public async Task Given_ANodeBootedFromAStandardGenesisJson_When_TheChainIdAndAllocAreQueried_Then_TheyMatchTheGenesisDocument()
        {
            var chainIdResult = await PostPlainAsync("eth_chainId", Array.Empty<object>());
            var chainId = Convert.ToInt64(chainIdResult.Body.GetProperty("result").GetString(), 16);
            Assert.Equal(HarnessGenesisJsonFixture.ChainId, chainId);

            var balanceResult = await PostPlainAsync(
                "eth_getBalance", new object[] { _fixture.FundedAddress, "latest" });
            var balance = balanceResult.Body.GetProperty("result").GetString();
            Assert.NotEqual("0x0", balance);

            var genesisResult = await PostPlainAsync("eth_getBlockByNumber", new object[] { "0x0", false });
            var genesisBlock = genesisResult.Body.GetProperty("result");
            Assert.True(genesisBlock.TryGetProperty("parentBeaconBlockRoot", out _));
        }

        [Fact]
        public async Task Given_ANodeBootedFromAStandardGenesisJson_When_TheBuildValidateAdoptLoopIsDrivenOverTheEngineApi_Then_TheHeadAdvancesAsValid()
        {
            var genesisResult = await PostPlainAsync("eth_getBlockByNumber", new object[] { "0x0", false });
            var genesisBlock = genesisResult.Body.GetProperty("result");
            var genesisHash = genesisBlock.GetProperty("hash").GetString()!;
            var genesisTimestamp = Convert.ToInt64(genesisBlock.GetProperty("timestamp").GetString(), 16);

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
            Assert.True(getPayloadResult.Body.TryGetProperty("result", out var payload), getPayloadResult.Body.GetRawText());

            Assert.Equal(genesisHash, payload.GetProperty("parentHash").GetString());
            var blockHash = payload.GetProperty("blockHash").GetString()!;

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

            var blockNumberResult = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
            var headBlockNumber = Convert.ToInt64(blockNumberResult.Body.GetProperty("result").GetString(), 16);
            Assert.Equal(1, headBlockNumber);
        }
    }
}
