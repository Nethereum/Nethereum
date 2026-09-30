using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientPageStorageFollowsPivotTests
    {
        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class RecordingSink : ISnapSyncSink
        {
            public Dictionary<string, byte[]> SlotsWritten { get; } = new();
            public int BeginCount { get; private set; }
            public int EndCount { get; private set; }
            public int AbortCount { get; private set; }

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct) => default;

            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
            {
                BeginCount++;
                return new(new Scope(this));
            }

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

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)i });
                var value = new byte[] { (byte)(i & 0xff), 0xCD };
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

        private sealed class RotatingOnStaleRootPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly byte[] _staleRoot;
            private readonly byte[] _liveRoot;
            private readonly byte[][] _liveRootHolder;

            public int StaleRootStorageCalls;
            public int LiveRootStorageCalls;

            public RotatingOnStaleRootPeer(ISnapPeer inner, byte[] staleRoot, byte[] liveRoot, byte[][] liveRootHolder)
            {
                _inner = inner;
                _staleRoot = staleRoot;
                _liveRoot = liveRoot;
                _liveRootHolder = liveRootHolder;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                if (ByteUtil.AreEqual(r.RootHash, _staleRoot))
                {
                    Interlocked.Increment(ref StaleRootStorageCalls);
                    Volatile.Write(ref _liveRootHolder[0], _liveRoot);
                    return Task.FromResult(new StorageRangesMessage
                    {
                        RequestId = r.RequestId,
                        Slots = new List<List<StorageRangesMessage.SlotEntry>>(),
                        Proof = new List<byte[]>(),
                    });
                }

                Interlocked.Increment(ref LiveRootStorageCalls);
                return _inner.GetStorageRangesAsync(r, ct);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Storage_PivotRotatesMidBatch_PeerEmptyOnStaleRoot_RefetchesAtNewRoot_NoSpuriousDefer()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x30;
            owner[31] = 0x01;

            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 4, seed: 1);
            var (stateRootB, storageRootB, entriesB) = BuildOwnerState(store, owner, count: 4, seed: 2);
            Assert.False(ByteArrayComparer.Current.Equals(storageRootA, storageRootB),
                "test fixture assumption: the two roots must have different storage roots");

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var realPeer = new InProcessSnapPeer(handler);
            var liveRootHolder = new byte[][] { stateRootA };
            var peer = new RotatingOnStaleRootPeer(realPeer, stateRootA, stateRootB, liveRootHolder);

            var recordingSink = new RecordingSink();
            var client = new SnapSyncClient(peer, recordingSink, responseBytesBudget: 524_288UL);

            var page = new SnapSyncClient.AccountWorkerResult();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            await client.FetchPageStorageAsync(
                stateRootA,
                new List<(byte[] Hash, byte[] Root)> { (owner, storageRootA) },
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
                getLiveRoot: () => Volatile.Read(ref liveRootHolder[0]),
                CancellationToken.None);

            Assert.Equal(1, peer.StaleRootStorageCalls);
            Assert.True(peer.LiveRootStorageCalls >= 1,
                "the batch must be re-dispatched at the rotated live root, not abandoned after the stale-root empty response");

            Assert.Empty(accountsNeedingHeal);
            Assert.Empty(deferredStorageDebts);

            Assert.Equal(1, recordingSink.EndCount);
            Assert.Equal(0, recordingSink.AbortCount);
            foreach (var (key, value) in entriesB)
                Assert.Equal(value, recordingSink.SlotsWritten[key.ToHex()]);
        }
    }
}
