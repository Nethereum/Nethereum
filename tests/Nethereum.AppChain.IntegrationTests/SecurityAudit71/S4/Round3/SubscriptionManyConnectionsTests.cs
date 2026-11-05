using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class SubscriptionManyConnectionsTests
    {
        private readonly ITestOutputHelper _out;

        public SubscriptionManyConnectionsTests(ITestOutputHelper output) { _out = output; }

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
        public async Task Given_ManyConnectionsEachHoldingTheMaximumSubscriptions_When_BlocksAreProduced_Then_TheHeadNotificationsOfOtherClientsStayOnBlockTime()
        {
            const int Connections = 150;
            var perConnection = Nethereum.CoreChain.Rpc.Subscriptions.SubscriptionManager.DefaultMaxSubscriptionsPerConnection;
            await using var host = await AppChainRpcHost.StartAsync(config => config.Consensus.AllowEmptyBlocks = true);
            using var run = new CancellationTokenSource(TimeSpan.FromSeconds(150));

            using var observer = new ClientWebSocket();
            await observer.ConnectAsync(host.WebSocketUrl, run.Token);
            var subscribe = Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_subscribe\",\"params\":[\"newHeads\"]}");
            await observer.SendAsync(subscribe, WebSocketMessageType.Text, true, run.Token);

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

            var attackers = new List<ClientWebSocket>();
            var drains = new List<Task>();
            for (var c = 0; c < Connections; c++)
            {
                var socket = new ClientWebSocket();
                await socket.ConnectAsync(host.WebSocketUrl, run.Token);
                attackers.Add(socket);
                drains.Add(Task.Run(async () =>
                {
                    var buffer = new byte[64 * 1024];
                    try { while (!run.IsCancellationRequested) await ReceiveTextAsync(socket, buffer, run.Token); }
                    catch (Exception) { }
                }));
            }

            var attackStart = clock.ElapsedMilliseconds;
            foreach (var socket in attackers)
                for (var i = 0; i < perConnection; i++)
                    await socket.SendAsync(subscribe, WebSocketMessageType.Text, true, run.Token);
            _out.WriteLine($"{Connections} connections x {perConnection} subscriptions sent in {clock.ElapsedMilliseconds - attackStart} ms");

            await Task.Delay(12000);
            List<long> snapshot;
            lock (arrivals) snapshot = arrivals.ToList();
            var gaps = new List<long>();
            for (var i = 1; i < snapshot.Count; i++)
                if (snapshot[i] > attackStart + 2000) gaps.Add(snapshot[i] - snapshot[i - 1]);
            var average = gaps.Count == 0 ? 0 : (long)gaps.Average();
            _out.WriteLine("gaps after the subscriptions were in: " + string.Join(",", gaps) + ", average " + average + " ms");

            run.Cancel();
            await Task.WhenAny(Task.WhenAll(drains.Append(observerTask)), Task.Delay(5000));
            foreach (var socket in attackers) socket.Dispose();

            Assert.True(gaps.Count >= 3, "too few notifications arrived to judge the interval");
            Assert.True(average < 1400, $"{Connections} connections x {perConnection} subscriptions stretched the average block interval to {average} ms (block time 1000 ms)");
        }
    }
}
