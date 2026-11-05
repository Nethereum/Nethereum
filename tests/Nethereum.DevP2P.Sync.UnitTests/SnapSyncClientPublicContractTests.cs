using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientPublicContractTests
    {
        private const int WhaleSlots = 40;
        private const ulong TruncatingBudget = 300UL;
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static byte[] Owner(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < WhaleSlots; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)seed, (byte)i });
                var value = new byte[] { (byte)(i & 0xff), (byte)seed, 0xCD };
                entries.Add((key, value));
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
            return (stateTrie.Root.GetHash(), storageRoot, entries);
        }

        private static async Task<bool> FetchAsync(
            SnapSyncClient client, byte[] stateRoot, byte[] owner, byte[] storageRoot,
            ConcurrentBag<SnapSyncClient.AccountNeedingHeal> bag, SnapTaskSet taskSet, bool cursoredWhalesSupported)
        {
            using var cts = new CancellationTokenSource(Bound);
            return await client.FetchPageStorageAsync(
                stateRoot,
                new List<(byte[] Hash, byte[] Root)> { (owner, storageRoot) },
                new SnapSyncClient.AccountWorkerResult(),
                bag,
                new ConcurrentDictionary<string, DeferredStorageDebt>(),
                fetchPivotBlock: null,
                reqId: 1,
                markProductive: () => { },
                markFailure: () => { },
                taskSet: taskSet,
                taskIndex: 0,
                cursoredWhalesSupported: cursoredWhalesSupported,
                getLiveRoot: () => stateRoot,
                cts.Token).WaitAsync(Bound);
        }

        private static void AssertWhaleStored(InMemorySnapSyncSink sink, byte[] owner, byte[] storageRoot, List<(byte[] Key, byte[] Value)> entries)
        {
            var stored = PatriciaTrie.LoadFromStorage(storageRoot, sink.TrieStorage, owner);
            Assert.Equal(storageRoot, stored.Root.GetHash(), ByteArrayComparer.Current);
            foreach (var (key, value) in entries)
                Assert.Equal(value, stored.Get(key));
        }

        [Fact]
        public async Task Given_ANullTaskSetAndAWhale_When_FetchPageStorageAsyncRuns_Then_TheWhaleCompletesInsideTheCall()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Owner(0x71);
            var (stateRoot, storageRoot, entries) = BuildOwnerState(store, owner, seed: 3);
            var sink = new InMemorySnapSyncSink();
            var client = new SnapSyncClient(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoCodes())), sink, responseBytesBudget: TruncatingBudget)
            { LargeContractConcurrency = 4 };
            var bag = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();

            var complete = await FetchAsync(client, stateRoot, owner, storageRoot, bag, taskSet: null, cursoredWhalesSupported: true);

            Assert.True(complete);
            Assert.Empty(bag);
            AssertWhaleStored(sink, owner, storageRoot, entries);
        }

        [Fact]
        public async Task Given_CursoredWhalesSupportedFalseAndACallersTaskSet_When_FetchPageStorageAsyncRuns_Then_TheWhaleCompletesAndTheCallersTaskSetHasNoSubtasks()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Owner(0x72);
            var (stateRoot, storageRoot, entries) = BuildOwnerState(store, owner, seed: 4);
            var sink = new InMemorySnapSyncSink();
            var client = new SnapSyncClient(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoCodes())), sink, responseBytesBudget: TruncatingBudget)
            { LargeContractConcurrency = 4 };
            var callersSet = new SnapTaskSet(1);

            var complete = await FetchAsync(client, stateRoot, owner, storageRoot, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), callersSet, cursoredWhalesSupported: false);

            Assert.True(complete);
            Assert.Empty(callersSet.Tasks[0].LargeContracts);
            AssertWhaleStored(sink, owner, storageRoot, entries);
        }

        [Fact]
        public async Task Given_CursoredWhalesSupportedTrueAndACallersTaskSet_When_FetchPageStorageAsyncRuns_Then_TheSubtasksAreCreatedInTheCallersTaskSet()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Owner(0x73);
            var (stateRoot, storageRoot, _) = BuildOwnerState(store, owner, seed: 5);
            var client = new SnapSyncClient(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoCodes())), new InMemorySnapSyncSink(), responseBytesBudget: TruncatingBudget)
            { LargeContractConcurrency = 4 };
            var callersSet = new SnapTaskSet(1) { LargeContractConcurrency = 4 };

            await FetchAsync(client, stateRoot, owner, storageRoot, new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(), callersSet, cursoredWhalesSupported: true);

            Assert.Contains(owner, callersSet.Tasks[0].LargeContracts.Keys, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_AChunkedOwnerWhoseDrainStraddlesARootRotation_When_AChunkedOwnerMismatches_Then_TheCallersAccountsNeedingHealBagHoldsIt()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Owner(0x74);
            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, seed: 6);
            var (stateRootB, storageRootB, _) = BuildOwnerState(store, owner, seed: 7);
            Assert.False(ByteArrayComparer.Current.Equals(storageRootA, storageRootB));
            var client = new SnapSyncClient(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoCodes())), responseBytesBudget: TruncatingBudget);
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            var range = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(range,
                new List<AccountClassification> { new(owner, storageRootA, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: owner, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(owner, SmallStorageResult.Large, new byte[32]) }, stateRootA);
            var bag = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var nodeStore = new InMemoryContentNodeStore();
            using var cts = new CancellationTokenSource(Bound);

            var currentRoot = stateRootA;
            SnapFragment.StorageSubtask lease;
            var rounds = 0;
            while ((lease = set.LeaseNext() as SnapFragment.StorageSubtask) != null)
            {
                Assert.True(++rounds <= WhaleSlots, "the owner never drained");
                await client.ProcessStorageSubtaskAsync(
                    lease, set, nodeStore, currentRoot, bag,
                    new ConcurrentDictionary<string, DeferredStorageDebt>(), null, cts.Token).WaitAsync(Bound);
                if (rounds == 1) currentRoot = stateRootB;
            }

            var held = Assert.Single(bag);
            Assert.Equal(owner, held.AccountHash, ByteArrayComparer.Current);
            Assert.Equal(storageRootB, held.ExpectedStorageRoot, ByteArrayComparer.Current);
        }
    }
}
