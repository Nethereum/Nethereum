using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerListenerGethDeadlineTests
    {
        private static readonly byte[] GenesisHash = System.Array.ConvertAll(new byte[32], _ => (byte)1);
        private const ulong NetworkId = 1;
        private static readonly TimeSpan GethEthHandshakeTimeout = TimeSpan.FromSeconds(5);

        private static Eth68StatusMessage Status() => new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = GenesisHash,
            GenesisHash = GenesisHash,
            ForkHash = 0,
            ForkNext = 0,
        };

        private static async Task<(PeerListener Listener, EthECKey Key, InMemoryChainStoreBundle Bundle)> StartListenerAsync()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new Nethereum.Model.BlockHeader { BlockNumber = 0 }, GenesisHash);
            var key = EthECKey.GenerateKey();
            var listener = new PeerListener(key, bundle, new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = false,
                MirrorRemoteStatus = false,
            }, Status());
            await listener.StartAsync();
            return (listener, key, bundle);
        }

        private static async Task<RlpxConnection> ConnectPingingClientAsync(PeerListener listener, EthECKey serverKey)
        {
            var client = new RlpxConnection(EthECKey.GenerateKey(), new DevP2PConfig
            {
                ClientId = "Nethereum.Sync.Tests.status/1",
                ConnectTimeoutMs = 5_000,
                HandshakeTimeoutMs = 5_000,
                PingIntervalMs = 500,
                NetworkId = NetworkId,
                GenesisHash = GenesisHash,
            });
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);
            return client;
        }

        [Fact]
        public void Given_DefaultListenerOptions_Then_TheIdleAndHandshakeDeadlinesAreGeths()
        {
            var options = new PeerListenerOptions();

            Assert.Equal(TimeSpan.FromSeconds(30), options.IdleTimeout);
            Assert.Equal(5_000, options.HandshakeTimeoutMs);
        }

        [Fact]
        public async Task Given_AnInboundPeerThatPingsButNeverSendsStatus_When_TheEthHandshakeTimeoutPasses_Then_TheServerDropsIt()
        {
            var (listener, key, bundle) = await StartListenerAsync();
            try
            {
                using var client = await ConnectPingingClientAsync(listener, key);
                var dropped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                client.Disconnected += (_, _) => dropped.TrySetResult();
                client.StartDispatching();
                var started = DateTime.UtcNow;

                var completed = await Task.WhenAny(dropped.Task, Task.Delay(GethEthHandshakeTimeout * 3));

                Assert.Same(dropped.Task, completed);
                Assert.True(DateTime.UtcNow - started < GethEthHandshakeTimeout * 2);
            }
            finally
            {
                await listener.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AnInboundPeerThatSendsStatusInTime_When_TheEthHandshakeTimeoutPasses_Then_TheServerKeepsIt()
        {
            var (listener, key, bundle) = await StartListenerAsync();
            try
            {
                using var client = await ConnectPingingClientAsync(listener, key);
                var ethOffset = client.GetCapabilityOffset("eth");
                var ethVersion = client.SharedCapabilities.Find(c => c.Name == "eth").Version;
                await client.SendMessageAsync(ethOffset + Eth68MessageIds.Status, EncodeStatus(ethVersion));
                var (msgId, _) = await client.ReceiveMessageAsync();
                Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
                var dropped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                client.Disconnected += (_, _) => dropped.TrySetResult();
                client.StartDispatching();

                var completed = await Task.WhenAny(dropped.Task, Task.Delay(GethEthHandshakeTimeout * 2));

                Assert.NotSame(dropped.Task, completed);
                Assert.Equal(1, listener.EthPeerCount);
            }
            finally
            {
                await listener.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        private static byte[] EncodeStatus(int version)
        {
            var s = Status();
            if (version >= 69)
                return Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = version,
                    NetworkId = s.NetworkId,
                    GenesisHash = s.GenesisHash,
                    ForkHash = s.ForkHash,
                    ForkNext = s.ForkNext,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = s.BestHash,
                });
            s.ProtocolVersion = version;
            return Eth68StatusMessageEncoder.Encode(s);
        }
    }
}
