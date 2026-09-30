using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class RlpxListenerLifecycleTests
    {
        private sealed class SingleThreadSynchronizationContext : SynchronizationContext
        {
            private readonly BlockingCollection<(SendOrPostCallback cb, object? state)> _queue =
                new BlockingCollection<(SendOrPostCallback, object?)>();

            public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

            public void RunOnCurrentThread()
            {
                foreach (var (cb, state) in _queue.GetConsumingEnumerable())
                    cb(state);
            }

            public void Complete() => _queue.CompleteAdding();
        }

        [Fact]
        public async System.Threading.Tasks.Task FastFailingHandshakes_When_Drained_Then_NoHandshakeTasksLeak()
        {
            const int connections = 250;
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                MaxInboundPerIP = connections + 10,
                MaxInboundPerSubnet = 0,
                HandshakeTimeoutMs = 1000
            };
            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            for (int i = 0; i < connections; i++)
            {
                var tcp = new TcpClient();
                tcp.Connect(IPAddress.Loopback, listener.Port);
                tcp.LingerState = new LingerOption(true, 0);
                tcp.Close();
            }

            await System.Threading.Tasks.Task.Delay(400);
            await listener.StopAsync();

            Assert.Equal(0, listener.PendingHandshakeCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task ThrowingPeerFailedSubscriber_When_InboundRejected_Then_ListenerKeepsAccepting()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                MaxInboundPerIP = 1,
                MaxInboundPerSubnet = 0,
                HandshakeTimeoutMs = 30000,
                ReadTimeoutMs = 30000
            };
            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            var rejected = new ManualResetEventSlim(false);
            listener.PeerFailed += (_, e) =>
            {
                if (e.Phase == "InboundPerIPCap")
                {
                    rejected.Set();
                    throw new InvalidOperationException("boom from a buggy subscriber");
                }
            };
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var s1 = new TcpClient();
            var s2 = new TcpClient();
            var s3 = new TcpClient();
            try
            {
                s1.Connect(IPAddress.Loopback, listener.Port);
                Assert.True(await WaitUntil(() => listener.CountInboundForIp(IPAddress.Loopback) >= 1,
                    TimeSpan.FromSeconds(5)), "socket 1 was not accepted");

                s2.Connect(IPAddress.Loopback, listener.Port);
                Assert.True(rejected.Wait(TimeSpan.FromSeconds(5)),
                    "the per-IP rejection did not fire");

                s1.Close();
                Assert.True(await WaitUntil(() => listener.CountInboundForIp(IPAddress.Loopback) == 0,
                    TimeSpan.FromSeconds(5)), "socket 1's slot was not released");

                s3.Connect(IPAddress.Loopback, listener.Port);
                bool acceptedAfterThrow = await WaitUntil(
                    () => listener.CountInboundForIp(IPAddress.Loopback) >= 1, TimeSpan.FromSeconds(5));

                Assert.True(acceptedAfterThrow,
                    "the accept loop stopped after a throwing PeerFailed subscriber");
            }
            finally
            {
                try { s1.Close(); } catch { }
                try { s2.Close(); } catch { }
                try { s3.Close(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ThrowingPeerAcceptedSubscriber_When_Raised_Then_ActivePeersNotLeaked()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                HandshakeTimeoutMs = 5000,
                ConnectTimeoutMs = 5000
            };
            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += (_, __) =>
                throw new InvalidOperationException("boom from a buggy consumer");
            var handshakeReported = new ManualResetEventSlim(false);
            listener.PeerFailed += (_, e) => { if (e.Phase == "Handshake") handshakeReported.Set(); };
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, config);
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());

                Assert.True(handshakeReported.Wait(TimeSpan.FromSeconds(5)),
                    "the throwing PeerAccepted failure was not reported");

                Assert.True(
                    await WaitUntil(() => listener.ActivePeers == 0, TimeSpan.FromSeconds(2)),
                    $"ActivePeers leaked after a throwing PeerAccepted subscriber: {listener.ActivePeers}");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ConcurrentStopAndDispose_When_Racing_Then_DoesNotThrow()
        {
            var listener = new RlpxListener(EthECKey.GenerateKey(),
                new DevP2PConfig { ClientId = "Nethereum/test" });
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var racers = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
            for (int i = 0; i < 32; i++)
                racers.Add(System.Threading.Tasks.Task.Run(() => listener.StopAsync()));
            racers.Add(System.Threading.Tasks.Task.Run(() => listener.Dispose()));

            await System.Threading.Tasks.Task.WhenAll(racers);
        }

        private static async System.Threading.Tasks.Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await System.Threading.Tasks.Task.Delay(20);
            }
            return condition();
        }

        [Fact]
        public void Dispose_OnSingleThreadedSyncContext_CompletesWithoutDeadlock()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                HandshakeTimeoutMs = 30000,
                ReadTimeoutMs = 30000
            };
            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var raw = new TcpClient();
            raw.Connect(IPAddress.Loopback, listener.Port);

            var acceptedBy = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (listener.CountInboundForIp(IPAddress.Loopback) < 1 && DateTime.UtcNow < acceptedBy)
                Thread.Sleep(10);
            Assert.True(listener.CountInboundForIp(IPAddress.Loopback) >= 1,
                "listener did not accept the raw socket in time");

            var completed = new ManualResetEventSlim(false);
            var pumpThread = new Thread(() =>
            {
                var ctx = new SingleThreadSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(ctx);
                ctx.Post(_ =>
                {
                    try { listener.Dispose(); completed.Set(); }
                    finally { ctx.Complete(); }
                }, null);
                ctx.RunOnCurrentThread();
            })
            { IsBackground = true };
            pumpThread.Start();

            try
            {
                Assert.True(
                    completed.Wait(TimeSpan.FromSeconds(20)),
                    "Dispose deadlocked on the single-threaded SynchronizationContext");
            }
            finally
            {
                try { raw.Close(); } catch { }
                pumpThread.Join(TimeSpan.FromSeconds(2));
            }
        }
    }
}
