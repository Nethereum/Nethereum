using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class SubscriptionCapTests
    {
        private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer, ct);
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);
            return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        }

        [Fact]
        [Trait("Finding", "S4-R3-03")]
        public async Task Given_AConnectionAtItsSubscriptionLimit_When_ItSubscribesOnceMore_Then_ItIsAnsweredWithAServerErrorAndAnUnsubscribeMakesRoom()
        {
            var limit = SubscriptionManager.DefaultMaxSubscriptionsPerConnection;
            await using var host = await AppChainRpcHost.StartAsync(config => config.Consensus.AllowEmptyBlocks = false);
            using var run = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(host.WebSocketUrl, run.Token);

            async Task<JsonElement> RequestAsync(string json)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, run.Token);
                while (true)
                {
                    var text = await ReceiveTextAsync(socket, run.Token);
                    if (text.Contains("eth_subscription")) continue;
                    return JsonDocument.Parse(text).RootElement.Clone();
                }
            }

            string first = "";
            for (var i = 0; i < limit; i++)
            {
                var answer = await RequestAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}");
                Assert.True(answer.TryGetProperty("result", out var id), "subscription " + (i + 1) + " was refused");
                if (i == 0) first = id.GetString()!;
            }

            var over = await RequestAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}");
            Assert.Equal(-32000, over.GetProperty("error").GetProperty("code").GetInt32());

            var removed = await RequestAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_unsubscribe\",\"params\":[\"" + first + "\"]}");
            Assert.True(removed.GetProperty("result").GetBoolean());

            var again = await RequestAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}");
            Assert.True(again.TryGetProperty("result", out _));
        }
        [Theory]
        [InlineData("\"address\":[ADDR]")]
        [InlineData("\"topics\":[[ADDR]]")]
        [InlineData("\"topics\":[null,null,null,null,null]")]
        [Trait("Finding", "S4-R3-04")]
        public async Task Given_ALogsSubscriptionWiderThanTheQueryLimit_When_ItIsRequested_Then_ItIsRefusedAsInvalidParams(string criteria)
        {
            var addresses = string.Join(",", Enumerable.Range(0, 1001).Select(i => "\"0x" + i.ToString("x40") + "\""));
            await using var host = await AppChainRpcHost.StartAsync(config => config.Consensus.AllowEmptyBlocks = false);
            using var run = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(host.WebSocketUrl, run.Token);
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"logs\",{" + criteria.Replace("ADDR", addresses) + "}]}";
            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, run.Token);

            var answer = JsonDocument.Parse(await ReceiveTextAsync(socket, run.Token)).RootElement;

            Assert.Equal(-32602, answer.GetProperty("error").GetProperty("code").GetInt32());
        }
    }
}
