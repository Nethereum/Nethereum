using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Nethereum.ChainNode.Hosting.Rpc;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    public class RpcPolicyTests
    {
        [Theory]
        [InlineData("localhost:8545", true)]
        [InlineData("LOCALHOST", true)]
        [InlineData("127.0.0.1:8545", true)]
        [InlineData("[::1]:8545", true)]
        [InlineData("203.0.113.9", true)]
        [InlineData("rebound.evil.example", false)]
        [InlineData("rebound.evil.example:8545", false)]
        public void Given_TheDefaultHostPolicy_When_AHostHeaderIsChecked_Then_OnlyLocalhostAndIpAddressesPass(string host, bool allowed)
        {
            var policy = new RpcHostPolicy(null, "127.0.0.1");

            Assert.Equal(allowed, policy.IsAllowed(host));
        }

        [Fact]
        public void Given_AConfiguredVirtualHost_When_ItsHeaderIsChecked_Then_ItPassesAndOthersStillFail()
        {
            var policy = new RpcHostPolicy(new[] { "appchain.internal" }, null);

            Assert.True(policy.IsAllowed("appchain.internal:8545"));
            Assert.False(policy.IsAllowed("other.internal"));
        }

        [Fact]
        public void Given_AWildcardVirtualHost_When_AnyNameIsChecked_Then_ItPasses()
        {
            Assert.True(new RpcHostPolicy(new[] { "*" }, null).IsAllowed("anything.example"));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("http://evil.example", false)]
        [InlineData("http://localhost:3000", false)]
        public void Given_NoAllowedOrigins_When_AnOriginHeaderIsChecked_Then_OnlyRequestsWithoutOriginPass(string? origin, bool allowed)
        {
            Assert.Equal(allowed, new RpcOriginPolicy(null).IsAllowed(origin));
        }

        [Theory]
        [InlineData("http://dapp.example", true)]
        [InlineData("https://dapp.example", false)]
        [InlineData("http://dapp.example:8080", true)]
        [InlineData("http://evil.example", false)]
        public void Given_AnAllowedOriginWithAScheme_When_OriginsAreChecked_Then_SchemeAndHostMustMatch(string origin, bool allowed)
        {
            Assert.Equal(allowed, new RpcOriginPolicy(new[] { "http://dapp.example" }).IsAllowed(origin));
        }

        [Fact]
        public void Given_AWildcardOrigin_When_AnyOriginIsChecked_Then_ItPasses()
        {
            Assert.True(new RpcOriginPolicy(new[] { "*" }).IsAllowed("http://evil.example"));
        }
    }

    [Collection("Sequential")]
    public class SharedEndpointTests
    {
        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Fact]
        [Trait("Finding", "S4-R1-04")]
        public async Task Given_ADevChainWithAConfiguredVirtualHost_When_ThatHostAndAForeignHostAreSent_Then_OnlyTheConfiguredOneIsServed()
        {
            await using var host = await DevChainRpcHost.StartAsync(config => config.Node.Rpc.AllowedHosts = "chain.internal");

            using var allowed = new HttpRequestMessage(HttpMethod.Post, host.Url)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "application/json")
            };
            allowed.Headers.Host = "chain.internal:8545";
            using var foreign = new HttpRequestMessage(HttpMethod.Post, host.Url)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "application/json")
            };
            foreign.Headers.Host = "rebound.evil.example";

            using var allowedResponse = await host.Http.SendAsync(allowed);
            using var foreignResponse = await host.Http.SendAsync(foreign);

            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, foreignResponse.StatusCode);
        }

        [Fact]
        [Trait("Finding", "S4-R1-04")]
        public async Task Given_ADefaultDevChain_When_AForeignHostHeaderIsSent_Then_ItIsServedBecauseDevChainHasNoHostCheckByDefault()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, host.Url)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "application/json")
            };
            request.Headers.Host = "container-name.internal";

            using var response = await host.Http.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("Finding", "S4-R1-09")]
        public async Task Given_ANotificationPostedToTheDevChain_When_ItIsProcessed_Then_NoContentIsReturned()
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var result = await host.PostAsync("{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[]}");

            Assert.Equal(HttpStatusCode.NoContent, result.Status);
        }

        [Fact]
        [Trait("Finding", "S4-R1-18")]
        public async Task Given_ABatchWhoseAnswersExceedTheCap_When_ItIsPosted_Then_TheRemainingMembersAreAnsweredWithResponseTooLargeAndKeepTheirIds()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var contract = "0x1000000000000000000000000000000000000003";
            await host.CallAsync("hardhat_setCode", "[\"" + contract + "\",\"0x" + new string('6', 2 * 24_000) + "\"]");
            var members = Enumerable.Range(1, 1000).Select(id =>
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"eth_getCode\",\"params\":[\"" + contract + "\",\"latest\"]}");

            var result = await host.PostAsync("[" + string.Join(",", members) + "]");

            using var document = JsonDocument.Parse(result.Body);
            var entries = document.RootElement.EnumerateArray().ToList();
            Assert.Equal(1000, entries.Count);
            var firstRefused = entries.FindIndex(e => e.TryGetProperty("error", out _));
            Assert.InRange(firstRefused, 1, 999);
            Assert.All(entries.Skip(firstRefused), e =>
                Assert.Equal(-32003, e.GetProperty("error").GetProperty("code").GetInt32()));
            for (var i = 0; i < entries.Count; i++)
                Assert.Equal(i + 1, entries[i].GetProperty("id").GetInt32());
            Assert.True(result.Body.Length <= 25_000_000);
        }

        [Theory]
        [Trait("Finding", "S4-R1-10")]
        [InlineData("[10,10]")]
        [InlineData("[50,10]")]
        [InlineData("[-1]")]
        [InlineData("[101]")]
        public async Task Given_RewardPercentilesThatAreNotStrictlyAscendingWithinZeroAndAHundred_When_FeeHistoryIsCalled_Then_InvalidParamsIsReturned(string percentiles)
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var body = await host.CallAsync("eth_feeHistory", "[\"0x1\",\"latest\"," + percentiles + "]");

            Assert.Equal(-32602, ErrorCode(body));
        }

        [Fact]
        [Trait("Finding", "S4-R1-10")]
        public async Task Given_AscendingRewardPercentiles_When_FeeHistoryIsCalled_Then_TheHistoryIsReturned()
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var body = await host.CallAsync("eth_feeHistory", "[\"0x1\",\"latest\",[25,50,75]]");

            Assert.Equal(0, ErrorCode(body));
        }
    }

    [Collection("S4AppChain")]
    public class AppChainConfiguredExposureTests
    {
        private static async Task<string> ExchangeAsync(AppChainRpcHost host, string request, string? origin)
        {
            using var socket = new ClientWebSocket();
            if (origin != null) socket.Options.SetRequestHeader("Origin", origin);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await socket.ConnectAsync(host.WebSocketUrl, timeout.Token);
            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[4096];
            var received = await socket.ReceiveAsync(buffer, timeout.Token);
            return Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        [Fact]
        [Trait("Finding", "S4-R1-01")]
        public async Task Given_AnAppChainStartedWithTheDebugAndAdminModules_When_TheirMethodsAreCalled_Then_TheyAreServed()
        {
            await using var host = await AppChainRpcHost.StartAsync(config => config.Node.Rpc.Modules = "eth,net,web3,debug,admin");

            var trace = await host.CallAsync("debug_traceCall", "[{\"to\":\"0x1000000000000000000000000000000000000001\"},\"latest\"]");
            var nodeInfo = await host.CallAsync("admin_nodeInfo", "[]");

            Assert.NotEqual(-32601, ErrorCodeOf(trace));
            Assert.NotEqual(-32601, ErrorCodeOf(nodeInfo));
        }

        [Fact]
        [Trait("Finding", "S4-R1-05")]
        public async Task Given_AnAppChainWithOneAllowedBrowserOrigin_When_ThatOriginAndAnotherConnect_Then_OnlyTheAllowedOneIsServed()
        {
            await using var host = await AppChainRpcHost.StartAsync(config => config.Node.Rpc.AllowedOrigins = "http://dapp.example");
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}";

            var allowed = await ExchangeAsync(host, request, "http://dapp.example");
            var foreign = async () => await ExchangeAsync(host, request, "http://evil.example");

            Assert.Contains("\"result\"", allowed);
            await Assert.ThrowsAnyAsync<WebSocketException>(foreign);
        }

        private static int ErrorCodeOf(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }
    }

    [Collection("S4AppChain")]
    public class AppChainSocketPolicyTests
    {
        private readonly AppChainRpcHost _host;

        public AppChainSocketPolicyTests(AppChainFixture fixture) { _host = fixture.Host; }

        [Fact]
        [Trait("Finding", "S4-R1-07")]
        public async Task Given_AMessageLargerThanTheWebSocketLimit_When_ItIsSentInFrames_Then_TheServerClosesWithMessageTooBig()
        {
            using var socket = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await socket.ConnectAsync(_host.WebSocketUrl, timeout.Token);
            var frame = new byte[1024 * 1024];
            Array.Fill(frame, (byte)'a');

            var closedByServer = false;
            try
            {
                for (var sent = 0; sent < 34; sent++)
                    await socket.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: false, timeout.Token);
                await socket.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
                var buffer = new byte[256];
                var received = await socket.ReceiveAsync(buffer, timeout.Token);
                closedByServer = received.MessageType == WebSocketMessageType.Close
                    && received.CloseStatus == WebSocketCloseStatus.MessageTooBig;
            }
            catch (WebSocketException)
            {
                closedByServer = true;
            }

            Assert.True(closedByServer);
        }

        [Fact]
        [Trait("Finding", "S4-R1-05")]
        public async Task Given_ASocketWithoutAnOriginHeader_When_ItIsOpenedOnAnAppChain_Then_ItIsServed()
        {
            using var socket = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await socket.ConnectAsync(_host.WebSocketUrl, timeout.Token);
            await socket.SendAsync(
                Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}"),
                WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[4096];
            var received = await socket.ReceiveAsync(buffer, timeout.Token);

            Assert.Contains("\"result\"", Encoding.UTF8.GetString(buffer, 0, received.Count));
        }
    }
}
