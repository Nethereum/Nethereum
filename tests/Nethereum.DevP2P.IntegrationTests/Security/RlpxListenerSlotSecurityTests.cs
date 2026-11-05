using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxListenerSlotSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SilentConnectionsHoldingEveryPendingSlot_When_TheyTimeOut_Then_ALegitimatePeerCompletesItsHandshake()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                MaxPendingPeers = 2,
                HandshakeTimeoutMs = 1000,
                ConnectTimeoutMs = 5000
            };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var silent = new List<TcpClient>();
            var legitimate = new RlpxConnection(EthECKey.GenerateKey(), new DevP2PConfig { HandshakeTimeoutMs = 8000, ConnectTimeoutMs = 5000 });
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var tcp = new TcpClient();
                    await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                    silent.Add(tcp);
                }
                await Task.Delay(300);
                Assert.True(listener.PendingHandshakeCount <= 2);

                await legitimate.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());

                await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(legitimate.IsConnected);
            }
            finally
            {
                legitimate.Dispose();
                foreach (var s in silent) { try { s.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }

        private static async Task ConnectIgnoringFailureAsync(RlpxConnection client, int port, byte[] serverPub)
        {
            try { await client.ConnectAsync("127.0.0.1", port, serverPub); }
            catch { }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ManyPeersCompletingHandshakesAtOnce_When_MaxPeersIsSmall_Then_EstablishedPeersNeverExceedMaxPeers()
        {
            const int maxPeers = 3;
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                MaxPeers = maxPeers,
                MaxInboundPerIP = 50,
                MaxInboundPerSubnet = 0,
                MaxPendingPeers = 50,
                HandshakeTimeoutMs = 8000,
                ConnectTimeoutMs = 5000
            };
            var listener = new RlpxListener(serverKey, config);
            var admitted = new List<RlpxConnection>();
            listener.PeerAccepted += (_, c) => { lock (admitted) admitted.Add(c); };
            listener.Start(0, IPAddress.Loopback);
            var clients = Enumerable.Range(0, 24)
                .Select(_ => new RlpxConnection(EthECKey.GenerateKey(), config)).ToList();
            try
            {
                using var barrier = new Barrier(clients.Count);
                var connects = new TaskCompletionSource<Task>[clients.Count];
                for (var i = 0; i < clients.Count; i++)
                {
                    var client = clients[i];
                    var started = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
                    connects[i] = started;
                    new Thread(() =>
                    {
                        barrier.SignalAndWait();
                        started.SetResult(ConnectIgnoringFailureAsync(client, listener.Port, serverKey.GetPubKeyNoPrefix()));
                    }) { IsBackground = true }.Start();
                }
                var launched = await Task.WhenAll(connects.Select(t => t.Task));
                await Task.WhenAll(launched);
                await Task.Delay(1000);

                int accepted;
                lock (admitted) accepted = admitted.Count;
                Assert.True(accepted <= maxPeers, $"{accepted} peers admitted with MaxPeers {maxPeers}");
                Assert.True(listener.ActivePeers <= maxPeers, $"ActivePeers {listener.ActivePeers} with MaxPeers {maxPeers}");
            }
            finally
            {
                foreach (var c in clients) { try { c.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AnEstablishedPeerFromAnIp_When_ItDisconnects_Then_ThatIpMayConnectAgain()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                MaxPeers = 20,
                MaxInboundPerIP = 1,
                IsLanAddress = _ => false,
                MaxInboundPerSubnet = 0,
                HandshakeTimeoutMs = 5000,
                ConnectTimeoutMs = 5000
            };
            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += (_, c) => c.StartDispatching();
            listener.Start(0, IPAddress.Loopback);
            var first = new RlpxConnection(EthECKey.GenerateKey(), config);
            var second = new RlpxConnection(EthECKey.GenerateKey(), config);
            try
            {
                await first.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                await WaitForAsync(() => listener.CountInboundForIp(IPAddress.Loopback) == 1);

                await first.DisconnectAsync();
                await WaitForAsync(() => listener.CountInboundForIp(IPAddress.Loopback) == 0);

                await second.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                Assert.True(second.IsConnected);
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AcceptFailingPersistently_When_ListenerRetries_Then_ItBacksOffInsteadOfSpinning()
        {
            var listener = new RlpxListener(EthECKey.GenerateKey(), new DevP2PConfig());
            var failures = 0;
            listener.PeerFailed += (_, e) => { if (e.Phase == "AcceptTcp") Interlocked.Increment(ref failures); };
            listener.Start(0, IPAddress.Loopback);
            try
            {
                var tcpListener = (TcpListener)typeof(RlpxListener)
                    .GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(listener)!;
                tcpListener.Stop();

                await Task.Delay(700);

                var observed = Volatile.Read(ref failures);
                Assert.InRange(observed, 1, 15);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AdmittedPeerSendingAnOversizedPing_When_TheConnectionFaults_Then_TheServerClosesItsSocketAndFreesTheSlot()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", MaxPeers = 5, HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            try
            {
                await attacker.SendFrameAsync(0, Nethereum.Model.P2P.HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                serverConn.StartDispatching();
                Assert.Equal(1, listener.ActivePeers);

                await attacker.SendFrameAsync(2, new byte[4096]);

                var buffer = new byte[1];
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var read = 0;
                try { read = await attacker.Stream.ReadAsync(buffer, cts.Token); }
                catch (System.IO.IOException) { }
                Assert.Equal(0, read);
                await WaitForAsync(() => listener.ActivePeers == 0);
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && !condition())
                await Task.Delay(50);
            Assert.True(condition());
        }
    }
}
