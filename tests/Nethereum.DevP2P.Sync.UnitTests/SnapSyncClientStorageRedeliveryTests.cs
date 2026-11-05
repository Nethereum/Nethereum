using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
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
    public class SnapSyncClientStorageRedeliveryTests
    {
        private const int PrefillBudget = 512_000;
        private const int TruncatingBudget = 400;
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class DiscardingFlatWriter : ISnapFlatStateWriter
        {
            public Task<Account> GetAccountByHashAsync(byte[] accountHash) => Task.FromResult<Account>(null);
            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => Task.CompletedTask;
            public Task DeleteAccountByHashAsync(byte[] accountHash) => Task.CompletedTask;
            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value) => Task.CompletedTask;
        }

        private sealed class Served
        {
            public InMemoryContentNodeStore Store { get; init; }
            public byte[] Owner { get; init; }
            public byte[] StateRoot { get; init; }
            public byte[] StorageRoot { get; init; }
        }

        private static byte[] Owner()
        {
            var a = new byte[32];
            a[0] = 0x61;
            a[31] = 0x11;
            return a;
        }

        private static Served BuildOwnerState(int count, int seed)
        {
            var store = new InMemoryContentNodeStore();
            var owner = Owner();
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

        private static async Task<SnapTaskSet> CompleteAtAsync(Served served, InMemoryContentNodeStore clientStore)
        {
            var sink = new TrieSnapSyncSink(clientStore, new InMemoryStateStore(), new DiscardingFlatWriter());
            var client = new SnapSyncClient(ServingPeer(served), sink, responseBytesBudget: PrefillBudget) { LargeContractConcurrency = 2 };
            using var cts = new CancellationTokenSource(Bound);
            var completed = await client.FetchPageStorageAsync(
                served.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (served.Owner, served.StorageRoot) },
                new SnapSyncClient.AccountWorkerResult(),
                new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(),
                new ConcurrentDictionary<string, DeferredStorageDebt>(),
                fetchPivotBlock: null, reqId: 1,
                markProductive: () => { }, markFailure: () => { },
                taskSet: null, taskIndex: 0, cursoredWhalesSupported: false,
                getLiveRoot: () => served.StateRoot, cts.Token);
            Assert.True(completed);

            var taskSet = new SnapTaskSet(1);
            taskSet.CreateLargeContract(0, served.Owner, served.StorageRoot, served.StateRoot);
            taskSet.MarkStorageCompleted(served.Owner);
            return taskSet;
        }

        private static async Task RedeliverTruncatedAsync(Served redelivered, InMemoryContentNodeStore clientStore, SnapTaskSet taskSet)
        {
            var sink = new TrieSnapSyncSink(clientStore, new InMemoryStateStore(), new DiscardingFlatWriter());
            var client = new SnapSyncClient(ServingPeer(redelivered), sink, responseBytesBudget: TruncatingBudget) { LargeContractConcurrency = 2 };
            using var cts = new CancellationTokenSource(Bound);
            await client.FetchPageStorageAsync(
                redelivered.StateRoot,
                new List<(byte[] Hash, byte[] Root)> { (redelivered.Owner, redelivered.StorageRoot) },
                new SnapSyncClient.AccountWorkerResult(),
                new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(),
                new ConcurrentDictionary<string, DeferredStorageDebt>(),
                fetchPivotBlock: null, reqId: 2,
                markProductive: () => { }, markFailure: () => { },
                taskSet: taskSet, taskIndex: 0, cursoredWhalesSupported: true,
                getLiveRoot: () => redelivered.StateRoot, cts.Token);
        }

        [Fact]
        public async Task Given_ALargeAccountCompletedAtOneRoot_When_ItIsRedeliveredTruncatedWithAnotherStorageRoot_Then_ItIsMarkedNeedHeal()
        {
            var completedAt = BuildOwnerState(count: 40, seed: 1);
            var redelivered = BuildOwnerState(count: 41, seed: 2);
            Assert.False(ByteArrayComparer.Current.Equals(completedAt.StorageRoot, redelivered.StorageRoot));
            var clientStore = new InMemoryContentNodeStore();
            var taskSet = await CompleteAtAsync(completedAt, clientStore);

            await RedeliverTruncatedAsync(redelivered, clientStore, taskSet);

            var marked = Assert.Single(taskSet.AccountsNeedingHeal);
            Assert.Equal(completedAt.Owner, marked, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_ALargeAccountCompletedAtOneRoot_When_ItIsRedeliveredTruncatedWithTheSameStorageRoot_Then_ItIsNotMarkedNeedHeal()
        {
            var completedAt = BuildOwnerState(count: 40, seed: 1);
            var clientStore = new InMemoryContentNodeStore();
            var taskSet = await CompleteAtAsync(completedAt, clientStore);

            await RedeliverTruncatedAsync(completedAt, clientStore, taskSet);

            Assert.Empty(taskSet.AccountsNeedingHeal);
        }
    }
}
