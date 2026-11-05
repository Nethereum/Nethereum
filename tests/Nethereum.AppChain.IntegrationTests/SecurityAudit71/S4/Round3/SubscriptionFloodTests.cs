using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class SubscriptionFloodTests
    {
        private readonly ITestOutputHelper _out;

        public SubscriptionFloodTests(ITestOutputHelper output) { _out = output; }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, byte[] buffer, CancellationToken ct)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer, ct);
                if (received.MessageType == WebSocketMessageType.Close) return "";
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);
            return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        }

        [Fact]
        [Trait("Finding", "S4-R3-03")]
        public async Task Given_OneConnectionHoldingManyNewHeadsSubscriptions_When_BlocksAreProduced_Then_TheHeadNotificationsOfOtherClientsStayOnBlockTime()
        {
            const int Subscriptions = 60_000;
            await using var host = await AppChainRpcHost.StartAsync(config => config.Consensus.AllowEmptyBlocks = true);
            using var run = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            using var observer = new ClientWebSocket();
            await observer.ConnectAsync(host.WebSocketUrl, run.Token);
            await observer.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}"),
                WebSocketMessageType.Text, true, run.Token);

            var arrivals = new List<long>();
            var clock = Stopwatch.StartNew();
            var observerTask = Task.Run(async () =>
            {
                var buffer = new byte[64 * 1024];
                try
                {
                    while (!run.IsCancellationRequested)
                    {
                        var text = await ReceiveTextAsync(observer, buffer, run.Token);
                        if (text.Contains("eth_subscription")) lock (arrivals) arrivals.Add(clock.ElapsedMilliseconds);
                    }
                }
                catch (Exception) { }
            });

            await Task.Delay(4000);
            int baseline;
            lock (arrivals) baseline = arrivals.Count;
            _out.WriteLine("baseline notifications in 4s: " + baseline);

            using var attacker = new ClientWebSocket();
            await attacker.ConnectAsync(host.WebSocketUrl, run.Token);
            var drain = Task.Run(async () =>
            {
                var buffer = new byte[64 * 1024];
                try
                {
                    while (!run.IsCancellationRequested) await ReceiveTextAsync(attacker, buffer, run.Token);
                }
                catch (Exception) { }
            });

            var subscribe = Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}");
            var sendStart = clock.ElapsedMilliseconds;
            for (var i = 0; i < Subscriptions; i++)
                await attacker.SendAsync(subscribe, WebSocketMessageType.Text, true, run.Token);
            _out.WriteLine("sent " + Subscriptions + " subscribes in " + (clock.ElapsedMilliseconds - sendStart) + " ms");

            await Task.Delay(12000);
            List<long> snapshot;
            lock (arrivals) snapshot = arrivals.ToList();
            var gaps = new List<long>();
            for (var i = 1; i < snapshot.Count; i++)
                if (snapshot[i] > sendStart) gaps.Add(snapshot[i] - snapshot[i - 1]);
            var maxGap = gaps.Count == 0 ? 0 : (long)gaps.Average();
            _out.WriteLine("notifications after attack start: " + gaps.Count + ", max gap " + maxGap + " ms, gaps: " + string.Join(",", gaps));

            run.Cancel();
            await Task.WhenAny(Task.WhenAll(observerTask, drain), Task.Delay(5000));

            Assert.True(maxGap < 1400, "one connection with " + Subscriptions + " newHeads subscriptions stretched the average block interval to " + maxGap + " ms (block time 1000 ms)");
        }
    }
}
