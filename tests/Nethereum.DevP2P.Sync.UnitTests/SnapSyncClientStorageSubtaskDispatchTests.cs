using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
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
    public class SnapSyncClientStorageSubtaskDispatchTests
    {
        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static byte[] Filled(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] key, byte[] value)> entries)
            BuildOracle(int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] key, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD };
                entries.Add((key, value));
                trie.Put(key, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.key, b.key));
            return (trie, storage, entries);
        }

        private sealed class PagingSnapPeer : ISnapPeer
        {
            private readonly Func<byte[], StorageRangesMessage> _respond;
            public int CallCount { get; private set; }

            public PagingSnapPeer(Func<byte[], StorageRangesMessage> respond) => _respond = respond;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                CallCount++;
                return Task.FromResult(_respond(r.StartingHash));
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static void PromoteWhale(SnapTaskSet set, byte[] account, byte[] storageRoot, byte[] stateRootAtCreation)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(account, storageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: account, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(account, SmallStorageResult.Large, new byte[32]) }, stateRootAtCreation);
        }

        [Fact]
        public async Task Storage_SubtaskDispatch_DrainsWhaleAcrossMultiplePages_ThenFlushesSharedScope()
        {
            const int total = 25;
            const int pageSize = 8;
            var (oracle, oracleStorage, entries) = BuildOracle(total, seed: 1);
            var finalRoot = oracle.Root.GetHash();
            var owner = Acc(0x20);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, finalRoot, finalRoot);
            var subtask = set.Tasks[0].LargeContracts[owner].Subtasks[0];
            Assert.Equal(new byte[32], subtask.Next, ByteArrayComparer.Current);
            Assert.Equal(Filled(0xff), subtask.Last, ByteArrayComparer.Current);

            StorageRangesMessage Respond(byte[] start)
            {
                var page = entries.Where(e => ByteArrayComparer.Current.Compare(e.key, start) >= 0)
                    .Take(pageSize).ToList();
                var lastKey = page[^1].key;
                return new StorageRangesMessage
                {
                    RequestId = 0,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        page.Select(e => new StorageRangesMessage.SlotEntry { Hash = e.key, Data = e.value }).ToList()
                    },
                    Proof = PatriciaRangeProofGenerator.GenerateProof(oracle.Root, oracleStorage, start, lastKey),
                };
            }

            var peer = new PagingSnapPeer(Respond);
            var client = new SnapSyncClient(peer);
            var store = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            var rounds = 0;
            object observedScope = null;
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                rounds++;
                Assert.True(rounds <= total, "subtask never drained");
                await client.ProcessStorageSubtaskAsync(
                    lease, set, store, finalRoot, accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);

                var scopeAfter = set.Tasks[0].LargeContracts[owner].Scope;
                if (scopeAfter != null)
                {
                    if (observedScope != null) Assert.Same(observedScope, scopeAfter);
                    observedScope = scopeAfter;
                }
            }

            Assert.True(rounds > 1, "the oracle must not fit in a single page for this test to be meaningful");
            Assert.True(peer.CallCount >= rounds);
            Assert.True(set.AllDone);
            Assert.Null(set.Tasks[0].LargeContracts[owner].Scope);
            Assert.Empty(deferredStorageDebts);
            Assert.Empty(accountsNeedingHeal);

            Assert.NotNull(observedScope);
            var drainedScope = (ResumableStorageScope)observedScope;
            foreach (var (key, value) in entries)
                Assert.Equal(value, drainedScope.Get(key));
            Assert.Equal(finalRoot, drainedScope.CurrentRootHash, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Storage_SubtaskPageCrossesIntoSiblingRange_CompletesOnlyThatSubtask_SharesOwnerScope()
        {
            const int pageSize = 3;
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] key, byte[] value)>();
            byte[] KeyWithFirstByte(byte b) { var k = new byte[32]; k[0] = b; return k; }
            foreach (var b in new byte[] { 0x05, 0x10, 0x90, 0xA0, 0xB0 })
            {
                var key = KeyWithFirstByte(b);
                var value = new byte[] { b };
                trie.Put(key, value);
                entries.Add((key, value));
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.key, b.key));
            var finalRoot = trie.Root.GetHash();
            var owner = Acc(0x30);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 2 };
            PromoteWhale(set, owner, finalRoot, finalRoot);

            var sub1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            var sub2 = (SnapFragment.StorageSubtask)set.LeaseNext();
            Assert.True(ByteArrayComparer.Current.Compare(sub1.Last, sub2.Next) < 0);
            Assert.True(ByteArrayComparer.Current.Compare(KeyWithFirstByte(0x90), sub1.Last) > 0,
                "test fixture assumption: the third entry must fall in subtask 2's half");

            StorageRangesMessage Respond(byte[] start)
            {
                var page = entries.Where(e => ByteArrayComparer.Current.Compare(e.key, start) >= 0)
                    .Take(pageSize).ToList();
                var lastKey = page[^1].key;
                return new StorageRangesMessage
                {
                    RequestId = 0,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        page.Select(e => new StorageRangesMessage.SlotEntry { Hash = e.key, Data = e.value }).ToList()
                    },
                    Proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, start, lastKey),
                };
            }

            var peer = new PagingSnapPeer(Respond);
            var client = new SnapSyncClient(peer);
            var store = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            await client.ProcessStorageSubtaskAsync(
                sub1, set, store, finalRoot, accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);

            var large = set.Tasks[0].LargeContracts[owner];
            Assert.True(large.Subtasks[0].Done, "subtask 1 must complete once its page crosses its own Last");
            Assert.False(large.Subtasks[1].Done, "subtask 2 must remain unprocessed");
            Assert.NotNull(large.Scope);

            var sharedScope = (ResumableStorageScope)large.Scope;

            await client.ProcessStorageSubtaskAsync(
                sub2, set, store, finalRoot, accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);

            Assert.True(large.Subtasks[1].Done);
            Assert.True(set.AllDone);
            Assert.Null(large.Scope);

            foreach (var (key, value) in entries)
                Assert.Equal(value, sharedScope.Get(key));
            Assert.Equal(finalRoot, sharedScope.CurrentRootHash, ByteArrayComparer.Current);
            Assert.Empty(deferredStorageDebts);
            Assert.Empty(accountsNeedingHeal);
        }

        [Fact]
        public async Task Storage_SubtaskDispatch_RealHandler_DrainsIncludingProofBearingTerminalPage()
        {
            const int total = 25;
            var (oracle, oracleStorage, entries) = BuildOracle(total, seed: 5);
            var finalRoot = oracle.Root.GetHash();
            var owner = Acc(0x40);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, finalRoot, finalRoot);

            var handler = new PatriciaSnapRequestHandler(
                oracleStorage, new InMemoryBytecodeStore(), accountStateRootProvider: _ => finalRoot);
            var peer = new InProcessSnapPeer(handler);
            var client = new SnapSyncClient(peer, responseBytesBudget: 300UL);
            var store = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            var rounds = 0;
            object observedScope = null;
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                rounds++;
                Assert.True(rounds <= total,
                    "subtask never drained -- terminal proof-less page rejected forever by the client verifier");
                await client.ProcessStorageSubtaskAsync(
                    lease, set, store, finalRoot, accountsNeedingHeal, deferredStorageDebts, null, CancellationToken.None);

                var scopeAfter = set.Tasks[0].LargeContracts[owner].Scope;
                if (scopeAfter != null) observedScope = scopeAfter;
            }

            Assert.True(rounds > 1, "the oracle must not fit in a single page for this test to be meaningful");
            Assert.True(set.AllDone);
            Assert.Null(set.Tasks[0].LargeContracts[owner].Scope);

            Assert.NotNull(observedScope);
            var drainedScope = (ResumableStorageScope)observedScope;
            foreach (var (key, value) in entries)
                Assert.Equal(value, drainedScope.Get(key));
            Assert.Equal(finalRoot, drainedScope.CurrentRootHash, ByteArrayComparer.Current);
            Assert.Empty(deferredStorageDebts);
            Assert.Empty(accountsNeedingHeal);
        }
    }
}
