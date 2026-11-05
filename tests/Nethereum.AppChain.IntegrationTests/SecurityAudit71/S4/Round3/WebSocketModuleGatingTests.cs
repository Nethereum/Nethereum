using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class WebSocketModuleGatingTests
    {
        private static async Task<string> RoundTripAsync(ClientWebSocket socket, string json, CancellationToken ct)
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
            var buffer = new byte[64 * 1024];
            var received = await socket.ReceiveAsync(buffer, ct);
            return Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Theory]
        [Trait("Finding", "S4-R3-sound")]
        [InlineData("net,web3")]
        [InlineData("web3")]
        public async Task Given_AnAppChainWithoutTheEthModule_When_ASocketSubscribesAndUnsubscribes_Then_BothAreMethodNotFoundAndNothingIsCreated(string modules)
        {
            await using var host = await AppChainRpcHost.StartAsync(config => config.Node.Rpc.Modules = modules);
            using var socket = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await socket.ConnectAsync(host.WebSocketUrl, timeout.Token);

            var subscribe = await RoundTripAsync(socket,
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}", timeout.Token);
            var unsubscribe = await RoundTripAsync(socket,
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"eth_unsubscribe\",\"params\":[\"0x1\"]}", timeout.Token);
            var blockNumber = await RoundTripAsync(socket,
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"eth_blockNumber\",\"params\":[]}", timeout.Token);

            Assert.Equal(-32601, ErrorCode(subscribe));
            Assert.Equal(-32601, ErrorCode(unsubscribe));
            Assert.Equal(-32601, ErrorCode(blockNumber));
        }

        [Fact]
        [Trait("Finding", "S4-R3-sound")]
        public async Task Given_AnAppChainWithTheEthModule_When_ASocketSubscribes_Then_ASubscriptionIdIsReturned()
        {
            await using var host = await AppChainRpcHost.StartAsync();
            using var socket = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await socket.ConnectAsync(host.WebSocketUrl, timeout.Token);

            var subscribe = await RoundTripAsync(socket,
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}", timeout.Token);

            Assert.Equal(0, ErrorCode(subscribe));
            Assert.Contains("result", subscribe);
        }
    }
}
