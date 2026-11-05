using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerListenerQuietPeerIdleTests
    {
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(8);
        private static readonly byte[] GenesisHash = System.Array.ConvertAll(new byte[32], _ => (byte)1);
        private const ulong NetworkId = 1;

        private sealed class UnusedSnapHandler : ISnapRequestHandler
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private sealed class Served : IAsyncDisposable
        {
            public PeerListener Listener;
            public EthECKey Key;
            public TaskCompletionSource<string> Removed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public InMemoryChainStoreBundle Bundle;

            public async ValueTask DisposeAsync()
            {
                await Listener.DisposeAsync();
                await Bundle.DisposeAsync();
            }
        }

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

        private static async Task<Served> StartListenerAsync(bool serveSnap)
        {
            var served = new Served { Key = EthECKey.GenerateKey(), Bundle = InMemoryChainStoreBundle.Open() };
            await served.Bundle.Blocks.SaveAsync(new Nethereum.Model.BlockHeader { BlockNumber = 0 }, GenesisHash);
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = serveSnap,
                MirrorRemoteStatus = false,
                IdleTimeout = IdleTimeout,
                OnInboundPeerAdded = _ => { },
                OnInboundPeerRemoved = key => served.Removed.TrySetResult(key),
            };
            served.Listener = new PeerListener(served.Key, served.Bundle, options, Status(), snapHandler: new UnusedSnapHandler());
            await served.Listener.StartAsync();
            return served;
        }

        private static async Task<RlpxConnection> ConnectAndExchangeStatusAsync(Served served, int pingIntervalMs)
        {
            var client = new RlpxConnection(EthECKey.GenerateKey(), new DevP2PConfig
            {
                ClientId = "Nethereum.Sync.Tests.quiet/1",
                ConnectTimeoutMs = 5_000,
                HandshakeTimeoutMs = 5_000,
                ReadTimeoutMs = 5_000,
                PingIntervalMs = pingIntervalMs,
                NetworkId = NetworkId,
                GenesisHash = GenesisHash,
            });
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.ConnectAsync("127.0.0.1", served.Listener.Port, served.Key.GetPubKeyNoPrefix(), ct.Token);

            var ethOffset = client.GetCapabilityOffset("eth");
            var ethVersion = client.SharedCapabilities.Find(c => c.Name == "eth").Version;
            await client.SendMessageAsync(ethOffset + Eth68MessageIds.Status, EncodeStatus(ethVersion), ct.Token);
            var (msgId, _) = await client.ReceiveMessageAsync(ct.Token);
            Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
            return client;
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AnInboundPeerThatOnlyExchangesPings_When_NoEthOrSnapMessageArrivesForFourIdleTimeouts_Then_TheServerKeepsItConnected(bool serveSnap)
        {
            await using var served = await StartListenerAsync(serveSnap);
            using var client = await ConnectAndExchangeStatusAsync(served, pingIntervalMs: 500);
            client.StartDispatching();

            var completed = await Task.WhenAny(served.Removed.Task, Task.Delay(QuietWindow));

            Assert.NotSame(served.Removed.Task, completed);
            Assert.True(client.IsConnected);
            Assert.False(client.IsDisconnected);
            Assert.Equal(1, served.Listener.EthPeerCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AnInboundPeerThatSendsNoFrameAtAll_When_TheIdleTimeoutPasses_Then_TheServerDropsIt(bool serveSnap)
        {
            await using var served = await StartListenerAsync(serveSnap);
            using var client = await ConnectAndExchangeStatusAsync(served, pingIntervalMs: 600_000);
            var started = DateTime.UtcNow;

            var completed = await Task.WhenAny(served.Removed.Task, Task.Delay(QuietWindow));

            Assert.Same(served.Removed.Task, completed);
            Assert.True(DateTime.UtcNow - started < IdleTimeout * 2);
        }
    }
}
