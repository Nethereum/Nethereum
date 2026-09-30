using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientOrphanedAttemptStalenessGuardTests
    {
        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class CountingSnapPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private int _storageRangeCalls;

            public CountingSnapPeer(ISnapPeer inner) => _inner = inner;

            public int StorageRangeCalls => Volatile.Read(ref _storageRangeCalls);

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _storageRangeCalls);
                return _inner.GetStorageRangesAsync(r, ct);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        private static (byte[] StateRoot, byte[] StorageRoot) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)i });
                var value = new byte[] { (byte)(i & 0xff), 0xCD };
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

            return (stateTrie.Root.GetHash(), storageRoot);
        }

        private static void PromoteWhale(SnapTaskSet set, byte[] owner, byte[] storageRoot, byte[] stateRootAtCreation)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(owner, storageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: owner, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(owner, SmallStorageResult.Large, new byte[32]) }, stateRootAtCreation);
        }

        [Fact]
        public async Task ProcessStorageSubtaskAsync_AttemptSupersededByFrozenRootMove_RevertsLeaseWithoutDispatching()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x60);
            var (stateRootA, storageRootA) = BuildOwnerState(store, owner, count: 20, seed: 30);
            var (stateRootB, _) = BuildOwnerState(store, owner, count: 20, seed: 31);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB),
                "test fixture assumption: the two roots must differ");

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, owner, storageRootA, stateRootA);

            var clientStore = new InMemoryContentNodeStore();
            var lease = (SnapFragment.StorageSubtask)set.LeaseNext();
            Assert.NotNull(lease);

            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();
            var frozenRootHolder = new byte[][] { stateRootB };

            await client.ProcessStorageSubtaskAsync(
                lease, set, clientStore, stateRootA,
                accountsNeedingHeal, deferredStorageDebts, fetchPivotBlock: null, CancellationToken.None,
                frozenRootHolder);

            Assert.Equal(0, peer.StorageRangeCalls);

            Assert.Empty(accountsNeedingHeal);
            Assert.Empty(deferredStorageDebts);

            var release = set.LeaseNext();
            Assert.NotNull(release);
            var releasedSubtask = Assert.IsType<SnapFragment.StorageSubtask>(release);
            Assert.Equal(owner, releasedSubtask.AccountHash, ByteArrayComparer.Current);
            Assert.Equal(lease.Next, releasedSubtask.Next, ByteArrayComparer.Current);
        }
    }
}
