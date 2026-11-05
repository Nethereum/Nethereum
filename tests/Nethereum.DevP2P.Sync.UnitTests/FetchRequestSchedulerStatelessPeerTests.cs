using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerStatelessPeerTests
    {
        private static readonly byte[] GenesisHash = System.Array.ConvertAll(new byte[32], _ => (byte)1);
        private const ulong NetworkId = 1;
        private static readonly TimeSpan FailFastBound = TimeSpan.FromSeconds(3);

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }
            return condition();
        }

        private static byte[] Root(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class TrieNodeHandler : ISnapRequestHandler
        {
            private readonly ISnapRequestHandler _inner;
            private int _calls;
            public TrieNodeHandler(ISnapRequestHandler inner) => _inner = inner;
            public int TrieCalls => Volatile.Read(ref _calls);
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _calls);
                if (_inner == null)
                    return Task.FromResult(new TrieNodesMessage { RequestId = request.RequestId, Nodes = new List<byte[]>() });
                return _inner.GetTrieNodesAsync(request, ct);
            }
        }

        private sealed class DirectHandshakeWorker : IPeerHandshakeWorker
        {
            public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => await SyncPeerSession.ConnectAsync(enode, timeout, ct, GenesisHash, NetworkId, minPeerLatestBlock);
        }

        private static async Task<(PeerListener Listener, string Enode)> StartListenerAsync(ISnapRequestHandler handler)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0 }, GenesisHash);
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = true,
                MirrorRemoteStatus = false,
            };
            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = GenesisHash,
                GenesisHash = GenesisHash,
                ForkHash = 0,
                ForkNext = 0,
            };
            var key = EthECKey.GenerateKey();
            var listener = new PeerListener(key, bundle, options, status, snapHandler: handler);
            await listener.StartAsync();
            return (listener, $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}");
        }

        private static async Task<SyncPeerSession> JoinAsync(PeerPoolManager pool, string enode, int expectedSnapPeers)
        {
            pool.EnqueueCandidate(enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= expectedSnapPeers, TimeSpan.FromSeconds(20)),
                "the peer never became snap-capable");
            return pool.ActivePeers.OfType<SyncPeerSession>().Single(p => ((IEthPeer)p).Enode == enode);
        }

        private static PeerPoolManager NewPool(int target, string trustedEnode = null)
            => new PeerPoolManager(new DirectHandshakeWorker(), new PeerPoolOptions(TargetPeerCount: target, MinPeerLatestBlock: 0),
                trustedDialKeys: trustedEnode == null ? null : new[] { trustedEnode });

        private static List<List<byte[]>> RootPath()
            => new List<List<byte[]>> { new List<byte[]> { PatriciaPathWalker.NibblesToCompact(Array.Empty<byte>()) } };

        private static async Task<(TimeSpan Elapsed, Exception Failure)> FetchAsync(FetchRequestScheduler scheduler, byte[] root)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await scheduler.FetchTrieNodesAsync(root, RootPath(), 524_288UL, cts.Token);
                return (sw.Elapsed, null);
            }
            catch (Exception ex)
            {
                return (sw.Elapsed, ex);
            }
        }

        [Fact]
        public async Task Given_APeerThatAnswersATrieNodeRequestWithNoNodes_When_TheSameRootIsRequestedAgain_Then_ThePeerIsNotAskedAgainAndTheRequestFailsFast()
        {
            var empty = new TrieNodeHandler(null);
            var (listener, enode) = await StartListenerAsync(empty);
            await using var _listener = listener;
            await using var pool = NewPool(1);
            await pool.StartAsync(CancellationToken.None);
            await JoinAsync(pool, enode, 1);
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var root = Root(0x51);

            var first = await FetchAsync(scheduler, root);
            var second = await FetchAsync(scheduler, root);

            Assert.Equal(1, empty.TrieCalls);
            Assert.IsType<FetchRequestFailedException>(first.Failure);
            Assert.IsType<FetchRequestFailedException>(second.Failure);
            Assert.True(first.Elapsed < FailFastBound, $"after the only snap peer turned stateless the request parked for {first.Elapsed}");
            Assert.True(second.Elapsed < FailFastBound, $"with every snap peer stateless for the root the request parked for {second.Elapsed}");
        }

        [Fact]
        public async Task Given_APeerStatelessForARoot_When_ThatNodeDisconnectsAndReconnects_Then_TheReconnectedPeerIsAskedOnceForThatRoot()
        {
            var empty = new TrieNodeHandler(null);
            var (listener, enode) = await StartListenerAsync(empty);
            await using var _listener = listener;
            await using var pool = NewPool(1, enode);
            await pool.StartAsync(CancellationToken.None);
            var first = await JoinAsync(pool, enode, 1);
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var root = Root(0x53);

            await FetchAsync(scheduler, root);
            Assert.Equal(1, empty.TrieCalls);

            await pool.DropAsync(first.Id, "test reconnect", CancellationToken.None);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.Id != first.Id && p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "the trusted node did not reconnect after the drop");
            var second = pool.ActivePeers.OfType<SyncPeerSession>().Single();
            Assert.NotEqual(first.Id, second.Id);

            var afterRejoin = await FetchAsync(scheduler, root);
            var afterThat = await FetchAsync(scheduler, root);

            Assert.Equal(2, empty.TrieCalls);
            Assert.IsType<FetchRequestFailedException>(afterRejoin.Failure);
            Assert.IsType<FetchRequestFailedException>(afterThat.Failure);
            Assert.True(afterThat.Elapsed < FailFastBound, $"the reconnected peer, stateless again, parked the request for {afterThat.Elapsed}");
        }

        [Fact]
        public async Task Given_APeerStatelessForARoot_When_ADifferentRootIsRequestedOrTheTargetRootChanges_Then_ThePeerIsAskedAgain()
        {
            var empty = new TrieNodeHandler(null);
            var (listener, enode) = await StartListenerAsync(empty);
            await using var _listener = listener;
            await using var pool = NewPool(1);
            await pool.StartAsync(CancellationToken.None);
            await JoinAsync(pool, enode, 1);
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            await FetchAsync(scheduler, Root(0x51));
            Assert.Equal(1, empty.TrieCalls);

            scheduler.OnTargetRootChanged();
            await FetchAsync(scheduler, Root(0x51));
            Assert.Equal(2, empty.TrieCalls);

            await FetchAsync(scheduler, Root(0x52));
            Assert.Equal(3, empty.TrieCalls);
        }

        [Fact]
        public async Task Given_APeerThatAnswersEmptyTrieNodesAtManyRoots_When_EachRootIsRequested_Then_ThePeerIsNeitherQuarantinedNorDropped()
        {
            var empty = new TrieNodeHandler(null);
            var (listener, enode) = await StartListenerAsync(empty);
            await using var _listener = listener;
            await using var pool = NewPool(1);
            await pool.StartAsync(CancellationToken.None);
            var peer = await JoinAsync(pool, enode, 1);
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            for (byte r = 1; r <= 5; r++)
            {
                await FetchAsync(scheduler, Root(r));
                scheduler.OnTargetRootChanged();
            }
            await Task.Delay(500);

            Assert.Equal(5, empty.TrieCalls);
            Assert.False(scheduler.IsSnapStateQuarantined(peer.Id));
            Assert.Equal(0, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
            Assert.Contains(pool.ActivePeers, p => p.Id == peer.Id);
        }

        [Fact]
        public async Task Given_APeerStatelessForARoot_When_ASnapCapablePeerJoins_Then_OnlyTheNewPeerIsAskedForThatRoot()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            trie.Put(Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 }), new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            trie.SaveDirtyNodesToStorage();
            var root = trie.Root.GetHash();

            var stateless = new TrieNodeHandler(null);
            var serving = new TrieNodeHandler(new PatriciaSnapRequestHandler(store, new NullBytecodes()));
            var (statelessListener, statelessEnode) = await StartListenerAsync(stateless);
            var (servingListener, servingEnode) = await StartListenerAsync(serving);
            await using var _stateless = statelessListener;
            await using var _serving = servingListener;
            await using var pool = NewPool(2);
            await pool.StartAsync(CancellationToken.None);
            await JoinAsync(pool, statelessEnode, 1);
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            await FetchAsync(scheduler, root);
            Assert.Equal(1, stateless.TrieCalls);

            await JoinAsync(pool, servingEnode, 2);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchTrieNodesAsync(root, RootPath(), 524_288UL, cts.Token);

            Assert.Single(result.Nodes);
            Assert.Equal(root, Sha3Keccack.Current.CalculateHash(result.Nodes[0]));
            Assert.Equal(1, serving.TrieCalls);
            Assert.Equal(1, stateless.TrieCalls);
        }
    }
}
