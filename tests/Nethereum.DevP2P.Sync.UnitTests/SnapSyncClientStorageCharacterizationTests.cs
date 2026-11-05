using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientStorageCharacterizationTests
    {
        private static byte[] Owner(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private sealed class DiscardingFlatWriter : ISnapFlatStateWriter
        {
            public Task<Account> GetAccountByHashAsync(byte[] accountHash) => Task.FromResult<Account>(null);
            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => Task.CompletedTask;
            public Task DeleteAccountByHashAsync(byte[] accountHash) => Task.CompletedTask;
            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value) => Task.CompletedTask;
        }

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class ScriptedPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private int _storageCalls;

            public ScriptedPeer(ISnapPeer inner) => _inner = inner;

            public bool EnforceCallerVerification { get; init; }
            public Func<int, GetStorageRangesMessage, StorageRangesMessage, StorageRangesMessage> Storage { get; init; }
            public Func<GetAccountRangeMessage, CancellationToken, Task<AccountRangeMessage>> Account { get; init; }
            public int StorageCalls => Volatile.Read(ref _storageCalls);
            public int AccountCalls;

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref AccountCalls);
                if (Account != null) return await Account(request, ct);
                return await _inner.GetAccountRangeAsync(request, ct);
            }

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                var index = Interlocked.Increment(ref _storageCalls) - 1;
                var real = await _inner.GetStorageRangesAsync(request, ct);
                return Storage != null ? Storage(index, request, real) : real;
            }

            public async Task<StorageRangesMessage> GetStorageRangesAsync(
                GetStorageRangesMessage request, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct = default)
            {
                var response = await GetStorageRangesAsync(request, ct);
                if (EnforceCallerVerification && !verifyResponse(response))
                    throw new FetchRequestFailedException("response rejected by the caller's verification", null);
                return response;
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(request, ct);

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(request, ct);
        }

        private sealed class Served
        {
            public InMemoryContentNodeStore Store { get; init; }
            public byte[] Owner { get; init; }
            public byte[] StateRoot { get; init; }
            public byte[] StorageRoot { get; init; }
        }

        private static Served BuildOwnerState(byte ownerFirst, int count, int seed, InMemoryContentNodeStore store = null)
        {
            store ??= new InMemoryContentNodeStore();
            var owner = Owner(ownerFirst);
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD });
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveDirtyNodesToStorage();

            return new Served { Store = store, Owner = owner, StateRoot = stateTrie.Root.GetHash(), StorageRoot = storageRoot };
        }

        private static ISnapPeer ServingPeer(Served served)
            => new InProcessSnapPeer(new PatriciaSnapRequestHandler(served.Store, new NoCodes()));

        private static StorageRangesMessage WithFirstValueCorrupted(StorageRangesMessage real)
        {
            var slots = real.Slots.Select(list => list
                .Select(s => new StorageRangesMessage.SlotEntry { Hash = s.Hash, Data = (byte[])s.Data.Clone() })
                .ToList()).ToList();
            slots[0][0].Data[0] ^= 0xFF;
            return new StorageRangesMessage { RequestId = real.RequestId, Slots = slots, Proof = real.Proof };
        }

        private sealed class Outcome
        {
            public bool Completed;
            public ConcurrentBag<SnapSyncClient.AccountNeedingHeal> Heal { get; } = new();
            public ConcurrentDictionary<string, DeferredStorageDebt> Debts { get; } = new();
            public InMemoryContentNodeStore ClientStore { get; } = new();
            public SnapSyncClient.AccountWorkerResult Page { get; } = new();
        }

        [Fact]
        public async Task Given_ALiveRootThatMovedAndAnOwnerThePeerCannotResolve_When_StorageIsFetched_Then_ItKeepsRetryingTheReResolveUntilCancelled()
        {
            var served = BuildOwnerState(0x57, count: 5, seed: 8);
            var movedRoot = CharBytes.Hash32(0x70);
            var peer = new ScriptedPeer(ServingPeer(served))
            {
                Account = (request, ct) => Task.FromResult(new AccountRangeMessage
                {
                    RequestId = request.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>(),
                    Proof = new List<byte[]>(),
                }),
            };
            var outcome = new Outcome();
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(outcome.ClientStore, new InMemoryStateStore(), new DiscardingFlatWriter()));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchPageStorageAsync(
                served.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (served.Owner, served.StorageRoot) },
                outcome.Page, outcome.Heal, outcome.Debts,
                fetchPivotBlock: null, reqId: 1,
                markProductive: () => { }, markFailure: () => { },
                taskSet: null, taskIndex: 0, cursoredWhalesSupported: false,
                getLiveRoot: () => movedRoot, cts.Token));

            Assert.True(peer.AccountCalls >= 1);
            Assert.Equal(0, peer.StorageCalls);
            Assert.Empty(outcome.Heal);
        }

        [Fact]
        public async Task Given_AFrozenRootThatDivergedFromTheDispatchRoot_When_StorageIsFetched_Then_ItReturnsFalseWithoutRequestingStorage()
        {
            var served = BuildOwnerState(0x58, count: 5, seed: 9);
            var peer = new ScriptedPeer(ServingPeer(served));
            var outcome = new Outcome();
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(outcome.ClientStore, new InMemoryStateStore(), new DiscardingFlatWriter()));

            var completed = await client.FetchPageStorageAsync(
                served.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (served.Owner, served.StorageRoot) },
                outcome.Page, outcome.Heal, outcome.Debts,
                fetchPivotBlock: null, reqId: 1,
                markProductive: () => { }, markFailure: () => { },
                taskSet: null, taskIndex: 0, cursoredWhalesSupported: false,
                getLiveRoot: () => served.StateRoot, CancellationToken.None,
                frozenRootHolder: new[] { CharBytes.Hash32(0x71) });

            Assert.False(completed);
            Assert.Equal(0, peer.StorageCalls);
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
        public async Task Given_ARootRotationWhoseReproveIsAlreadyInFlightForTheOwner_When_AnotherSubtaskOfTheOwnerIsProcessed_Then_ItIsRevertedForALaterLeaseWithoutAnotherAccountFetch()
        {
            var store = new InMemoryContentNodeStore();
            var a = BuildOwnerState(0x59, count: 30, seed: 10, store);
            var b = BuildOwnerState(0x59, count: 30, seed: 20, store);
            Assert.NotEqual(a.StorageRoot, b.StorageRoot);

            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var inner = ServingPeer(b);
            var peer = new ScriptedPeer(inner)
            {
                Account = async (request, ct) =>
                {
                    started.TrySetResult();
                    await release.Task;
                    return await inner.GetAccountRangeAsync(request, ct);
                },
            };
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, a.Owner, a.StorageRoot, a.StateRoot);
            var clientStore = new InMemoryContentNodeStore();
            var heal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var debts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            var first = (SnapFragment.StorageSubtask)set.LeaseNext();
            var second = (SnapFragment.StorageSubtask)set.LeaseNext();
            var firstRun = client.ProcessStorageSubtaskAsync(first, set, clientStore, b.StateRoot, heal, debts, null, CancellationToken.None);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            await client.ProcessStorageSubtaskAsync(second, set, clientStore, b.StateRoot, heal, debts, null, CancellationToken.None);

            Assert.Equal(1, peer.AccountCalls);
            Assert.Equal(0, peer.StorageCalls);
            var relet = (SnapFragment.StorageSubtask)set.LeaseNext();
            Assert.Equal(second.Next, relet.Next, ByteArrayComparer.Current);
            Assert.Equal(second.Last, relet.Last, ByteArrayComparer.Current);

            release.SetResult();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(b.StorageRoot, set.Tasks[0].LargeContracts[a.Owner].StorageRoot, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_AStoragePageThatFailsVerification_When_AStorageSubtaskIsProcessed_Then_TheSubtaskIsRevertedAndLeasableAgain()
        {
            var store = new InMemoryContentNodeStore();
            var a = BuildOwnerState(0x5B, count: 30, seed: 12, store);
            var peer = new ScriptedPeer(ServingPeer(a))
            {
                Storage = (index, request, real) => WithFirstValueCorrupted(real),
            };
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            PromoteWhale(set, a.Owner, a.StorageRoot, a.StateRoot);
            var lease = (SnapFragment.StorageSubtask)set.LeaseNext();

            await client.ProcessStorageSubtaskAsync(
                lease, set, new InMemoryContentNodeStore(), a.StateRoot,
                new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(),
                new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);

            Assert.Equal(1, peer.StorageCalls);
            var leased = new List<SnapFragment.StorageSubtask>();
            SnapFragment next;
            while ((next = set.LeaseNext()) is SnapFragment.StorageSubtask subtask) leased.Add(subtask);
            Assert.Equal(4, leased.Count);
            Assert.Contains(leased, l => ByteArrayComparer.Current.Equals(l.Next, lease.Next));
            Assert.DoesNotContain(a.Owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_ALiveRootThatMovedAndAnOwnerThePeerInitiallyCannotResolve_When_StorageIsFetched_Then_ItRetriesAfterTheBackoffAndDispatchesAtTheLiveRootWithTheNewStorageRoot()
        {
            var store = new InMemoryContentNodeStore();
            var a = BuildOwnerState(0x5C, count: 5, seed: 13, store);
            var b = BuildOwnerState(0x5C, count: 5, seed: 14, store);
            var inner = ServingPeer(b);
            var storageRoots = new ConcurrentQueue<byte[]>();
            var accountCalls = 0;
            var peer = new ScriptedPeer(inner)
            {
                Account = (request, ct) => Interlocked.Increment(ref accountCalls) == 1
                    ? Task.FromResult(new AccountRangeMessage
                    {
                        RequestId = request.RequestId,
                        Accounts = new List<AccountRangeMessage.AccountEntry>(),
                        Proof = new List<byte[]>(),
                    })
                    : inner.GetAccountRangeAsync(request, ct),
                Storage = (index, request, real) => { storageRoots.Enqueue(request.RootHash); return real; },
            };
            var outcome = new Outcome();
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(outcome.ClientStore, new InMemoryStateStore(), new DiscardingFlatWriter()));

            var completed = await client.FetchPageStorageAsync(
                a.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (a.Owner, a.StorageRoot) },
                outcome.Page, outcome.Heal, outcome.Debts,
                fetchPivotBlock: null, reqId: 1,
                markProductive: () => { }, markFailure: () => { },
                taskSet: null, taskIndex: 0, cursoredWhalesSupported: false,
                getLiveRoot: () => b.StateRoot, CancellationToken.None);

            Assert.True(completed);
            Assert.Equal(2, peer.AccountCalls);
            var requestedRoot = Assert.Single(storageRoots);
            Assert.Equal(b.StateRoot, requestedRoot, ByteArrayComparer.Current);
            Assert.Empty(outcome.Heal);
            var reloaded = PatriciaTrie.LoadFromStorage(b.StorageRoot, outcome.ClientStore, b.Owner);
            Assert.Equal(b.StorageRoot, reloaded.Root.GetHash(), ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_ALiveRootInWhichTheOwnerNoLongerExists_When_StorageIsFetched_Then_TheOwnerIsDroppedAndNoStorageIsRequested()
        {
            var store = new InMemoryContentNodeStore();
            var a = BuildOwnerState(0x5D, count: 5, seed: 15, store);
            var otherTrie = new PatriciaTrie(store);
            otherTrie.Put(Owner(0xF0), new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            otherTrie.SaveDirtyNodesToStorage();
            var movedRoot = otherTrie.Root.GetHash();
            var peer = new ScriptedPeer(ServingPeer(a));
            var outcome = new Outcome();
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(outcome.ClientStore, new InMemoryStateStore(), new DiscardingFlatWriter()));

            var completed = await client.FetchPageStorageAsync(
                a.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (a.Owner, a.StorageRoot) },
                outcome.Page, outcome.Heal, outcome.Debts,
                fetchPivotBlock: null, reqId: 1,
                markProductive: () => { }, markFailure: () => { },
                taskSet: null, taskIndex: 0, cursoredWhalesSupported: false,
                getLiveRoot: () => movedRoot, CancellationToken.None);

            Assert.True(completed);
            Assert.Equal(1, peer.AccountCalls);
            Assert.Equal(0, peer.StorageCalls);
            Assert.Empty(outcome.Heal);
        }
    }
}
