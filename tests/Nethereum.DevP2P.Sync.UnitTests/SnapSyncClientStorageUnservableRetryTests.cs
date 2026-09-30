using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Storage;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientStorageUnservableRetryTests
    {
        private static readonly TimeSpan UnservableWindow = TimeSpan.FromMilliseconds(600);
        private const int MinimumFailedCalls = 3;

        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static void PromoteWhale(SnapTaskSet set, byte[] account, byte[] storageRoot, byte[] stateRoot)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(account, storageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: account, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(account, SmallStorageResult.Large, new byte[32]) }, stateRoot);
        }

        private sealed class UnservableForAWhileStoragePeer : ISnapPeer
        {
            private readonly Stopwatch _sinceFirstCall = new();
            private readonly TimeSpan _unservableFor;
            private readonly Func<byte[], StorageRangesMessage> _respond;

            public UnservableForAWhileStoragePeer(TimeSpan unservableFor, Func<byte[], StorageRangesMessage> respond)
            {
                _unservableFor = unservableFor;
                _respond = respond;
            }

            public int FailedCalls { get; private set; }
            public int ServedCalls { get; private set; }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                if (!_sinceFirstCall.IsRunning) _sinceFirstCall.Start();
                if (FailedCalls < MinimumFailedCalls || _sinceFirstCall.Elapsed < _unservableFor)
                {
                    FailedCalls++;
                    return Task.FromException<StorageRangesMessage>(new FetchRequestFailedException("no peer can serve this root right now", null));
                }
                ServedCalls++;
                return Task.FromResult(_respond(r.StartingHash));
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => throw new NotSupportedException("the pivot never moves in this test, so no reprove is expected");

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        [Fact]
        public async Task Given_AWhaleSubtaskUnservableForSeveralAttempts_When_PeersStartServingAgain_Then_TheOwnerCompletesInPhase2WithNoDebt()
        {
            var keccak = new Sha3Keccack();
            var oracleStorage = new InMemoryContentNodeStore();
            var oracle = new PatriciaTrie(oracleStorage);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < 12; i++)
            {
                var key = keccak.CalculateHash(new byte[] { 0x0B, (byte)i });
                var value = new byte[] { (byte)(i + 1), 0xCD };
                entries.Add((key, value));
                oracle.Put(key, value);
            }
            oracle.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));
            var storageRoot = oracle.Root.GetHash();
            var stateRoot = keccak.CalculateHash(new byte[] { 0x57, 0xA7, 0xE0 });

            StorageRangesMessage Serve(byte[] start)
            {
                var page = entries.Where(e => ByteArrayComparer.Current.Compare(e.Key, start) >= 0).ToList();
                return new StorageRangesMessage
                {
                    RequestId = 0,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        page.Select(e => new StorageRangesMessage.SlotEntry { Hash = e.Key, Data = e.Value }).ToList()
                    },
                    Proof = PatriciaRangeProofGenerator.GenerateProof(oracle.Root, oracleStorage, start, page[^1].Key),
                };
            }

            var owner = Acc(0x62);
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, storageRoot, stateRoot);

            var peer = new UnservableForAWhileStoragePeer(UnservableWindow, Serve);
            var client = new SnapSyncClient(peer);
            var store = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                await client.ProcessStorageSubtaskAsync(
                    lease, set, store, stateRoot, accountsNeedingHeal, deferredStorageDebts,
                    fetchPivotBlock: 4242UL, cts.Token);
            }

            Assert.True(peer.FailedCalls >= MinimumFailedCalls, $"the subtask must have been retried while unservable; failed={peer.FailedCalls}");
            Assert.True(peer.ServedCalls > 0, "the subtask must have been fetched once peers served again");
            Assert.Empty(deferredStorageDebts);
            Assert.Empty(accountsNeedingHeal);
            Assert.True(set.AllDone);
            Assert.Contains(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);
            Assert.Null(set.Tasks[0].LargeContracts[owner].Scope);

            var downloaded = PatriciaTrie.LoadFromStorage(storageRoot, store);
            foreach (var (key, value) in entries)
                Assert.Equal(value, downloaded.Get(key));
        }
    }
}
