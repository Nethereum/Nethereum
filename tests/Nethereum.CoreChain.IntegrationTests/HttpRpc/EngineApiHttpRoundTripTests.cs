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
    public class EngineApiHttpRoundTripTests : IClassFixture<EngineApiHttpFixture>
    {
        private readonly EngineApiHttpFixture _fixture;
        private static readonly HttpClient Client = new HttpClient();
        private static int _nextId = 1;

        public EngineApiHttpRoundTripTests(EngineApiHttpFixture fixture)
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

        private Task<RpcHttpResult> PostEngineAsync(string method, object[] parameters, string? bearerToken) =>
            PostAsync(_fixture.EngineUrl, method, parameters, bearerToken);

        private Task<RpcHttpResult> PostPlainAsync(string method, object[] parameters) =>
            PostAsync(_fixture.Url, method, parameters, null);

        [Fact]
        public async Task Given_AFreshDevChainBehindTheEngineApi_When_TheBuildValidateAdoptLoopIsDrivenOverHttp_Then_TheHeadAdvancesAsValid()
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
            var payload = getPayloadResult.Body.GetProperty("result");

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

        [Fact]
        public async Task Given_ARequestWithNoAuthorizationHeader_When_AnyEngineMethodIsCalled_Then_TheResponseIsUnauthorizedAndThePlainPortIsUnaffected()
        {
            var result = await PostEngineAsync("engine_exchangeCapabilities", new object[] { Array.Empty<object>() }, null);

            Assert.Equal(HttpStatusCode.Unauthorized, result.Status);

            var plainChainIdResult = await PostPlainAsync("eth_chainId", Array.Empty<object>());
            Assert.Equal(HttpStatusCode.OK, plainChainIdResult.Status);
            Assert.False(string.IsNullOrEmpty(plainChainIdResult.Body.GetProperty("result").GetString()));
        }

        [Fact]
        public async Task Given_ATokenSignedWithTheWrongSecret_When_AnyEngineMethodIsCalled_Then_TheResponseIsUnauthorized()
        {
            var wrongSecret = new byte[32];
            var wrongToken = EngineApiHttpFixture.MintToken(wrongSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var result = await PostEngineAsync("engine_exchangeCapabilities", new object[] { Array.Empty<object>() }, wrongToken);

            Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
        }

        [Fact]
        public async Task Given_AnExpiredIat_When_AnyEngineMethodIsCalled_Then_TheResponseIsUnauthorized()
        {
            var secretHex = System.IO.File.ReadAllText(_fixture.JwtSecretPath).Trim();
            var secret = Convert.FromHexString(secretHex);
            var expiredToken = EngineApiHttpFixture.MintToken(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600);

            var result = await PostEngineAsync("engine_exchangeCapabilities", new object[] { Array.Empty<object>() }, expiredToken);

            Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
        }

        [Fact]
        public async Task Given_AValidToken_When_ExchangeCapabilitiesIsCalled_Then_ItListsTheEngineMethodsThisNodeServes()
        {
            var result = await PostEngineAsync("engine_exchangeCapabilities", new object[] { Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.OK, result.Status);
            var methods = result.Body.GetProperty("result");

            var found = new List<string>();
            foreach (var method in methods.EnumerateArray())
            {
                found.Add(method.GetString()!);
            }

            Assert.Contains("engine_newpayloadv3", found, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("engine_forkchoiceupdatedv3", found, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("engine_getpayloadv3", found, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("engine_getclientversionv1", found, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("engine_exchangecapabilities", found, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_AValidToken_When_GetClientVersionIsCalled_Then_ItReturnsTheNethereumClientIdentity()
        {
            var result = await PostEngineAsync("engine_getClientVersionV1", Array.Empty<object>());
            Assert.Equal(HttpStatusCode.OK, result.Status);
            var first = result.Body.GetProperty("result")[0];

            Assert.Equal("NE", first.GetProperty("code").GetString());
            Assert.Equal("Nethereum", first.GetProperty("name").GetString());
        }
    }
}
