using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientStorageRotationReproveTests
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
            private int _accountRangeCalls;
            private int _storageRangeCalls;

            public CountingSnapPeer(ISnapPeer inner) => _inner = inner;

            public int AccountRangeCalls => Volatile.Read(ref _accountRangeCalls);
            public int StorageRangeCalls => Volatile.Read(ref _storageRangeCalls);

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _accountRangeCalls);
                return _inner.GetAccountRangeAsync(r, ct);
            }

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

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] key, byte[] value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] key, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD };
                entries.Add((key, value));
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.key, b.key));
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

        private static byte[] BuildAbsentOwnerStateRoot(InMemoryContentNodeStore store)
        {
            var stateTrie = new PatriciaTrie(store);
            stateTrie.SaveDirtyNodesToStorage();
            return stateTrie.Root.GetHash();
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
        public async Task Storage_PivotMovesMidLargeStorage_ReprovesOwnerKeepsCursorAndUpdatesStorageRoot()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x50);
            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 30, seed: 10);
            var (stateRootB, storageRootB, _) = BuildOwnerState(store, owner, count: 30, seed: 20);
            Assert.False(ByteArrayComparer.Current.Equals(storageRootA, storageRootB),
                "test fixture assumption: the two roots must have different storage roots");

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var untouchedBefore = set.Tasks[0].LargeContracts[owner].Subtasks
                .Select(s => (s.Next, s.Last)).ToList();

            var clientStore = new InMemoryContentNodeStore();

            var lease0 = (SnapFragment.StorageSubtask)set.LeaseNext();
            await client.ProcessStorageSubtaskAsync(lease0, set, clientStore, stateRootA, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);
            Assert.Equal(0, peer.AccountRangeCalls);
            Assert.Equal(storageRootA, set.Tasks[0].LargeContracts[owner].StorageRoot, ByteArrayComparer.Current);

            var lease1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            await client.ProcessStorageSubtaskAsync(lease1, set, clientStore, stateRootB, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

            Assert.Equal(1, peer.AccountRangeCalls);
            var owned = set.Tasks[0].LargeContracts[owner];
            Assert.Equal(storageRootB, owned.StorageRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, owned.LastReprovedStateRoot, ByteArrayComparer.Current);

            var stillUntouched = new[] { owned.Subtasks[2], owned.Subtasks[3] };
            Assert.Equal(untouchedBefore[2].Next, stillUntouched[0].Next, ByteArrayComparer.Current);
            Assert.Equal(untouchedBefore[2].Last, stillUntouched[0].Last, ByteArrayComparer.Current);
            Assert.Equal(untouchedBefore[3].Next, stillUntouched[1].Next, ByteArrayComparer.Current);
            Assert.Equal(untouchedBefore[3].Last, stillUntouched[1].Last, ByteArrayComparer.Current);

            Assert.True(set.Tasks[0].LargeContracts.ContainsKey(owner));
            Assert.DoesNotContain(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);

            var callsBeforeSecondLease = peer.AccountRangeCalls;
            var lease2 = (SnapFragment.StorageSubtask)set.LeaseNext();
            await client.ProcessStorageSubtaskAsync(lease2, set, clientStore, stateRootB, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);
            Assert.Equal(callsBeforeSecondLease, peer.AccountRangeCalls);
        }

        [Fact]
        public async Task Storage_WhaleDrainsAcrossRotation_PagesAfterRotationVerifyAgainstNewRoot()
        {
            const int total = 40;
            const int pageSize = 6;
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x51);
            var (stateRootA, storageRootA, entriesA) = BuildOwnerState(store, owner, total, seed: 30);
            var (stateRootB, storageRootB, entriesB) = BuildOwnerState(store, owner, total, seed: 40);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: (ulong)(pageSize * 40));

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();

            var currentStateRoot = stateRootA;
            var rounds = 0;
            byte[] cursorAtRotation = null;
            object observedScope = null;
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                rounds++;
                Assert.True(rounds <= total,
                    "subtask never drained -- pre-fix bug: a page after rotation fails verification " +
                    "against the stale StorageRoot and the subtask reverts and retries forever");

                await client.ProcessStorageSubtaskAsync(lease, set, clientStore, currentStateRoot, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

                if (rounds == 1)
                {
                    cursorAtRotation = set.Tasks[0].LargeContracts[owner].Subtasks[0].Next;
                    currentStateRoot = stateRootB;
                }

                var scopeAfter = set.Tasks[0].LargeContracts[owner].Scope;
                if (scopeAfter != null) observedScope = scopeAfter;
            }

            Assert.True(rounds > 2, "the oracle must not fit in one or two pages for this test to be meaningful");
            Assert.True(set.AllDone);
            Assert.Equal(1, peer.AccountRangeCalls);
            Assert.Equal(storageRootB, set.Tasks[0].LargeContracts[owner].StorageRoot, ByteArrayComparer.Current);

            Assert.NotNull(observedScope);
            var drainedScope = (ResumableStorageScope)observedScope;

            var afterRotation = entriesB.Where(e => ByteArrayComparer.Current.Compare(e.key, cursorAtRotation) >= 0).ToList();
            Assert.True(afterRotation.Count > 0, "test fixture assumption: some oracle-B entries fall after the rotation cursor");
            foreach (var (key, value) in afterRotation)
                Assert.Equal(value, drainedScope.Get(key));

            var beforeRotation = entriesA.Where(e => ByteArrayComparer.Current.Compare(e.key, cursorAtRotation) < 0).ToList();
            foreach (var (key, value) in beforeRotation)
                Assert.Equal(value, drainedScope.Get(key));
        }

        [Fact]
        public async Task Storage_WhaleDrainsAcrossRotation_MixedRootDrain_RecordsDeferredStorageDebt()
        {
            const int total = 40;
            const int pageSize = 6;
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x56);
            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, total, seed: 90);
            var (stateRootB, storageRootB, _) = BuildOwnerState(store, owner, total, seed: 100);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: (ulong)(pageSize * 40));

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            var currentStateRoot = stateRootA;
            var rounds = 0;
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                rounds++;
                Assert.True(rounds <= total, "subtask never drained -- see the sibling rotation test for the pre-fix failure mode");

                await client.ProcessStorageSubtaskAsync(
                    lease, set, clientStore, currentStateRoot,
                    accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);

                if (rounds == 1) currentStateRoot = stateRootB;
            }

            Assert.True(set.AllDone);
            Assert.Equal(1, peer.AccountRangeCalls);
            Assert.Equal(storageRootB, set.Tasks[0].LargeContracts[owner].StorageRoot, ByteArrayComparer.Current);
            Assert.Contains(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);

            Assert.Contains(accountsNeedingHeal, a => ByteArrayComparer.Current.Equals(a.AccountHash, owner));
            var debt = Assert.Single(deferredStorageDebts.Values,
                d => ByteArrayComparer.Current.Equals(d.AccountHash, owner));
            Assert.True(debt.IsOpen);
            Assert.Equal(DeferredStorageReason.BigAccountChunked, debt.Reason);
            Assert.Equal(StorageCompleteness.DeferredBigAccount, debt.Status);
        }

        [Fact]
        public async Task Storage_OwnerAbsentAtRotatedRoot_SubtasksDroppedNotRetriedForever()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x52);
            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 20, seed: 50);
            var absentStateRoot = BuildAbsentOwnerStateRoot(store);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 3 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();

            var lease = (SnapFragment.StorageSubtask)set.LeaseNext();
            await client.ProcessStorageSubtaskAsync(lease, set, clientStore, absentStateRoot, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

            Assert.Equal(1, peer.AccountRangeCalls);
            Assert.Equal(0, peer.StorageRangeCalls);

            var owned = set.Tasks[0].LargeContracts[owner];
            Assert.All(owned.Subtasks, s => Assert.True(s.Done));
            Assert.Equal(0, owned.Pending);
            Assert.Contains(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);

            Assert.Null(set.LeaseNext());
        }

        private sealed class ByzantineAccountRangePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            public ByzantineAccountRangePeer(ISnapPeer inner) => _inner = inner;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => Task.FromResult(new AccountRangeMessage
                {
                    RequestId = r.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>
                    {
                        new AccountRangeMessage.AccountEntry { Hash = r.StartingHash, Body = new byte[] { 0x01 } }
                    },
                    Proof = new List<byte[]>(),
                });

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Storage_ByzantinePeerAccountResponse_DoesNotWedgeReproveClaim_ResolvesIndeterminate()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x54);
            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 20, seed: 60);
            var stateRootB = BuildAbsentOwnerStateRoot(store);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new ByzantineAccountRangePeer(new InProcessSnapPeer(handler));
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();

            var lease = (SnapFragment.StorageSubtask)set.LeaseNext();

            await client.ProcessStorageSubtaskAsync(lease, set, clientStore, stateRootB, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

            Assert.Equal(SnapTaskSet.ReproveClaim.Claimed, set.TryClaimReprove(0, owner, stateRootB));
        }

        private sealed class FixedRootStorageRangePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly byte[] _fixedStateRoot;
            public FixedRootStorageRangePeer(ISnapPeer inner, byte[] fixedStateRoot)
            {
                _inner = inner;
                _fixedStateRoot = fixedStateRoot;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = r.RequestId,
                    RootHash = _fixedStateRoot,
                    AccountHashes = r.AccountHashes,
                    StartingHash = r.StartingHash,
                    LimitHash = r.LimitHash,
                    ResponseBytes = r.ResponseBytes,
                }, ct);

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Storage_ReproveDropsOwnerConcurrently_InFlightPageWriteIsSkipped()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x55);
            var (stateRootA, storageRootA, entries) = BuildOwnerState(store, owner, count: 30, seed: 70);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new FixedRootStorageRangePeer(new InProcessSnapPeer(handler), stateRootA);
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();

            var lease = (SnapFragment.StorageSubtask)set.LeaseNext();

            var droppedAtRoot = BuildAbsentOwnerStateRoot(store);
            set.CommitReproveAbsentOrEmpty(0, owner, droppedAtRoot);
            Assert.Null(set.GetStorageRoot(0, owner));

            await client.ProcessStorageSubtaskAsync(lease, set, clientStore, droppedAtRoot, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

            var scope = (ResumableStorageScope)set.GetOrCreateStorageScope(
                0, owner, () => ResumableStorageScope.Open(clientStore, owner));
            foreach (var (key, _) in entries)
                Assert.Null(scope.Get(key));
        }

        [Fact]
        public void Storage_ConcurrentSubtasksOfSameOwner_ReproveClaimedExactlyOnce()
        {
            var storageRootA = new byte[32];
            storageRootA[0] = 0xAB;
            var stateRootA = new byte[32];
            stateRootA[0] = 0xCD;
            var stateRootB = new byte[32];
            stateRootB[0] = 0xEF;
            var owner = Acc(0x53);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 8 };
            PromoteWhale(set, owner, storageRootA, stateRootA);

            var claims = new SnapTaskSet.ReproveClaim[8];
            Parallel.For(0, 8, i => claims[i] = set.TryClaimReprove(0, owner, stateRootB));

            Assert.Equal(1, claims.Count(c => c == SnapTaskSet.ReproveClaim.Claimed));
            Assert.Equal(7, claims.Count(c => c == SnapTaskSet.ReproveClaim.InFlight));

            set.CommitReproveFound(0, owner, stateRootB, storageRootA);
            Assert.Equal(SnapTaskSet.ReproveClaim.NotNeeded, set.TryClaimReprove(0, owner, stateRootB));
        }

        private sealed class TimingOutStorageRangePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            public TimingOutStorageRangePeer(ISnapPeer inner) => _inner = inner;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => throw new FetchRequestFailedException("simulated peer timeout serving storage-range", null);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Storage_FirstPeerCannotServeWhale_ReQueuesToServingPeer_CompletesWithoutHeal()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x60);
            var (stateRootA, storageRootA, entries) = BuildOwnerState(store, owner, count: 30, seed: 80);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var badPeer = new TimingOutStorageRangePeer(new InProcessSnapPeer(handler));
            var goodPeer = new CountingSnapPeer(new InProcessSnapPeer(handler));
            var badClient = new SnapSyncClient(badPeer, responseBytesBudget: 300UL);
            var goodClient = new SnapSyncClient(goodPeer, responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, storageRootA, stateRootA);
            var clientStore = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            const int badDispatches = 40;
            int rounds = 0;
            object observedScope = null;
            SnapFragment.StorageSubtask lease;
            while ((lease = set.LeaseNext() as SnapFragment.StorageSubtask) != null)
            {
                Assert.True(++rounds <= 5000, "subtask never drained even against a serving peer");
                var client = rounds <= badDispatches ? badClient : goodClient;
                await client.ProcessStorageSubtaskAsync(
                    lease, set, clientStore, stateRootA, accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);
                var scopeAfter = set.Tasks[0].LargeContracts[owner].Scope;
                if (scopeAfter != null) observedScope = scopeAfter;
            }

            Assert.True(rounds > badDispatches,
                "owner was abandoned to heal on the un-servable peer instead of patiently re-queuing (regression)");
            Assert.True(set.AllDone);
            Assert.Equal(0, set.Tasks[0].LargeContracts[owner].Pending);
            Assert.Contains(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);

            Assert.DoesNotContain(accountsNeedingHeal, a => ByteArrayComparer.Current.Equals(a.AccountHash, owner));
            Assert.DoesNotContain(deferredStorageDebts.Values,
                d => ByteArrayComparer.Current.Equals(d.AccountHash, owner));

            Assert.NotNull(observedScope);
            var drainedScope = (ResumableStorageScope)observedScope;
            foreach (var (key, value) in entries)
                Assert.Equal(value, drainedScope.Get(key));
        }

    }
}
