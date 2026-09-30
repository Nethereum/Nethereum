using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerStorageEmptyQuarantineTests
    {
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

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class RecordingSink : ISnapSyncSink
        {
            public Dictionary<string, byte[]> SlotsWritten { get; } = new();
            public int EndCount { get; private set; }
            public int AbortCount { get; private set; }

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct) => default;

            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => new(new Scope(this));

            private sealed class Scope : IStorageScope
            {
                private readonly RecordingSink _sink;
                public Scope(RecordingSink sink) => _sink = sink;
                public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
                { _sink.SlotsWritten[slotHash.ToHex()] = valueRlp; return default; }
                public ValueTask EndAsync(CancellationToken ct) { _sink.EndCount++; return default; }
                public ValueTask AbortAsync(CancellationToken ct) { _sink.AbortCount++; return default; }
            }

            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct) => default;
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct) => new(new byte[32]);
        }

        private sealed class DirectHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly byte[] _genesisHash;
            private readonly ulong _networkId;
            public DirectHandshakeWorker(byte[] genesisHash, ulong networkId)
            {
                _genesisHash = genesisHash;
                _networkId = networkId;
            }
            public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => await SyncPeerSession.ConnectAsync(enode, timeout, ct, _genesisHash, _networkId, minPeerLatestBlock);
        }

        private sealed class AlwaysEmptyStorageSnapHandler : ISnapRequestHandler
        {
            public int StorageCalls;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref StorageCalls);
                return Task.FromResult(new StorageRangesMessage
                {
                    RequestId = request.RequestId,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>(),
                    Proof = new List<byte[]>(),
                });
            }
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private sealed class CountingSnapHandler : ISnapRequestHandler
        {
            private readonly ISnapRequestHandler _inner;
            public int StorageCalls;
            public CountingSnapHandler(ISnapRequestHandler inner) => _inner = inner;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(request, ct);
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref StorageCalls);
                return _inner.GetStorageRangesAsync(request, ct);
            }
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(request, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(request, ct);
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => _inner.GetBlockAccessListsAsync(request, ct);
        }

        private static readonly byte[] GenesisHash = new byte[32];
        private const ulong NetworkId = 1;

        private static async Task<(PeerListener Listener, string Enode)> StartListenerAsync(ISnapRequestHandler handler)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new Nethereum.Model.BlockHeader { BlockNumber = 0 }, new byte[32]);
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
            var enode = $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            return (listener, enode);
        }

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)i });
                var value = new byte[] { (byte)(i & 0xff), 0xEF };
                entries.Add((key, value));
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();

            return (stateTrie.Root.GetHash(), storageRoot, entries);
        }

        [Fact]
        public async Task Storage_WhollyEmptyResponse_PeerQuarantinedAndRotatesToServingPeer_NoHang_NoSpuriousDefer()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x40;
            owner[31] = 0x02;
            var (stateRoot, storageRoot, entries) = BuildOwnerState(store, owner, count: 4, seed: 7);

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore()));
            var badHandler = new AlwaysEmptyStorageSnapHandler();

            var (badListener, badEnode) = await StartListenerAsync(badHandler);
            var (goodListener, goodEnode) = await StartListenerAsync(goodHandler);

            await using var _badListenerDisposal = badListener;
            await using var _goodListenerDisposal = goodListener;

            var pool = new PeerPoolManager(
                new DirectHandshakeWorker(GenesisHash, NetworkId),
                new PeerPoolOptions(TargetPeerCount: 2, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            await using var _poolDisposal = pool;

            pool.EnqueueCandidate(badEnode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "bad peer never became snap-capable");
            var badPeerId = pool.ActivePeers.OfType<SyncPeerSession>().Single().Id;

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            using (var primeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await scheduler.FetchStorageRangesAsync(
                        stateRoot, new List<byte[]> { owner }, new byte[32], FilledHash(0xff), 524_288UL, primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            var snapPeer = new SchedulerSnapPeer(scheduler);
            var sink = new RecordingSink();
            var client = new SnapSyncClient(snapPeer, sink, responseBytesBudget: 524_288UL);

            var page = new SnapSyncClient.AccountWorkerResult();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await client.FetchPageStorageAsync(
                stateRoot,
                new List<(byte[] Hash, byte[] Root)> { (owner, storageRoot) },
                page,
                accountsNeedingHeal,
                deferredStorageDebts,
                fetchPivotBlock: null,
                reqId: 1,
                markProductive: () => { },
                markFailure: () => { },
                taskSet: null,
                taskIndex: 0,
                cursoredWhalesSupported: false,
                getLiveRoot: () => stateRoot,
                testCts.Token);

            Assert.Empty(accountsNeedingHeal);
            Assert.Empty(deferredStorageDebts);

            Assert.Equal(1, sink.EndCount);
            Assert.Equal(0, sink.AbortCount);
            foreach (var (key, value) in entries)
                Assert.Equal(value, sink.SlotsWritten[key.ToHex()]);

            Assert.True(goodHandler.StorageCalls >= 1, "the serving peer must actually have been reached");
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the wholly-empty peer must be quarantined");
        }
    }
}
