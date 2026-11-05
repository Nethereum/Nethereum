using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    public sealed class AppChainFixture : IAsyncLifetime
    {
        public AppChainRpcHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await AppChainRpcHost.StartAsync();

        public async Task DisposeAsync() => await Host.DisposeAsync();
    }

    [CollectionDefinition("S4AppChain")]
    public class S4AppChainCollection : ICollectionFixture<AppChainFixture> { }

    [Collection("S4AppChain")]
    public class AppChainExposureTests
    {
        private readonly AppChainRpcHost _host;

        public AppChainExposureTests(AppChainFixture fixture) { _host = fixture.Host; }

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        private async Task<string> WebSocketExchangeAsync(string request, string? origin = null)
        {
            using var socket = new ClientWebSocket();
            if (origin != null) socket.Options.SetRequestHeader("Origin", origin);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await socket.ConnectAsync(_host.WebSocketUrl, timeout.Token);
            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[65536];
            var received = await socket.ReceiveAsync(buffer, timeout.Token);
            return Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        [Fact]
        [Trait("Finding", "S4-R1-01")]
        public async Task Given_ADefaultAppChainFollower_When_AStrangerAddsAnArbitraryPeerOverTheRpcPort_Then_TheAdminMethodIsNotFound()
        {
            var body = await _host.CallAsync("admin_addPeer", "[\"enode://" + new string('a', 128) + "@203.0.113.9:30303\"]");

            Assert.Equal(-32601, ErrorCode(body));
        }

        [Fact]
        [Trait("Finding", "S4-R1-02")]
        public async Task Given_ADefaultAppChain_When_DebugTraceCallIsCalledWithoutAnyOptIn_Then_TheMethodIsNotFound()
        {
            var body = await _host.CallAsync("debug_traceCall", "[{\"to\":\"0x1000000000000000000000000000000000000001\"},\"latest\"]");

            Assert.Equal(-32601, ErrorCode(body));
        }

        [Fact]
        [Trait("Finding", "S4-R1-03")]
        public async Task Given_ABrowserFormPostWithTextPlainContentType_When_ItIsSentToAnAppChain_Then_ItIsRefusedAsUnsupportedMediaType()
        {
            var result = await _host.PostAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", "text/plain");

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, result.Status);
        }

        [Fact]
        [Trait("Finding", "S4-R1-04")]
        public async Task Given_ARequestCarryingAForeignHostHeader_When_ItReachesAnAppChainOnLoopback_Then_TheHostIsRefused()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _host.Url)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "application/json")
            };
            request.Headers.Host = "rebound.evil.example";

            using var response = await _host.Http.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Finding", "S4-R1-05")]
        public async Task Given_AWebSocketUpgradeFromAForeignBrowserOrigin_When_ItIsOpenedOnAnAppChain_Then_TheHandshakeIsRefused()
        {
            var connect = async () => await WebSocketExchangeAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", "http://evil.example");

            await Assert.ThrowsAnyAsync<WebSocketException>(connect);
        }

        [Fact]
        [Trait("Finding", "S4-R1-06")]
        public async Task Given_ASubscribeRequestWithANonStringKind_When_ItIsSentOverTheWebSocket_Then_TheErrorCarriesNoFrameworkExceptionText()
        {
            var response = await WebSocketExchangeAsync("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_subscribe\",\"params\":[5]}");

            Assert.DoesNotContain("requested operation", response);
            Assert.Equal(-32602, ErrorCode(response));
        }

        [Fact]
        [Trait("Finding", "S4-R1-06")]
        public async Task Given_ARequestRejectedWithInvalidParams_When_ItIsSentOverTheWebSocket_Then_TheCodeAndTheIdAreKept()
        {
            var response = await WebSocketExchangeAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_getLogs\",\"params\":[{\"fromBlock\":\"0x5\",\"toBlock\":\"0x1\"}]}");

            using var document = JsonDocument.Parse(response);
            Assert.Equal(-32602, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            Assert.Equal(7, document.RootElement.GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Finding", "S4-R1-06")]
        public async Task Given_ABatchArray_When_ItIsSentOverTheWebSocket_Then_ItIsAnsweredAsABatch()
        {
            var response = await WebSocketExchangeAsync(
                "[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]},{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"eth_blockNumber\",\"params\":[]}]");

            using var document = JsonDocument.Parse(response);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(2, document.RootElement.GetArrayLength());
        }

        [Fact]
        [Trait("Finding", "S4-R1-07")]
        public async Task Given_ARequestLargerThanOneReceiveBuffer_When_ItIsSentOverTheWebSocket_Then_ItIsHandledAsOneMessage()
        {
            var padding = new string('a', 6000);
            var response = await WebSocketExchangeAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[],\"pad\":\"" + padding + "\"}");

            using var document = JsonDocument.Parse(response);
            Assert.True(document.RootElement.TryGetProperty("result", out _));
        }
    }
}
