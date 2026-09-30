using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Metrics;
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
    public class SnapSyncClientWhaleOverflowTests
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

        private sealed class Scenario
        {
            public byte[] StateRoot;
            public byte[] SmallAccountHash;
            public byte[] SmallStorageRoot;
            public byte[] SmallSlotValue;
            public byte[] WhaleAccountHash;
            public byte[] WhaleStorageRoot;
            public List<(byte[] Key, byte[] Value)> WhaleEntries;
            public InMemoryContentNodeStore ClientNodeStore;
            public SnapSyncClient.SyncResult Result;
            public List<SnapSyncClient.SnapSyncCheckpoint> Checkpoints;
        }

        private static async Task<Scenario> RunAsync(
            int whaleEntryCount = 36,
            ulong responseBytesBudget = 400UL,
            SnapSyncMetrics metrics = null,
            Func<ISnapPeer, ISnapPeer> wrapPeer = null,
            Action<SnapSyncClient> configure = null,
            ISnapFlatStateWriter flatWriter = null,
            Action<Scenario, SnapSyncClient.SnapSyncCheckpoint> onCheckpoint = null,
            Func<ITrieNodeStore, ITrieNodeStore> wrapNodeStore = null)
        {
            var keccak = new Sha3Keccack();
            var serverStore = new InMemoryContentNodeStore();

            var smallHash = Acc(0x10);
            var smallSlotKey = keccak.CalculateHash(new byte[] { 0xAA });
            var smallSlotValue = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x07 });
            var smallStorageTrie = new PatriciaTrie(serverStore, smallHash);
            smallStorageTrie.Put(smallSlotKey, smallSlotValue);
            smallStorageTrie.SaveDirtyNodesToStorage();
            var smallStorageRoot = smallStorageTrie.Root.GetHash();

            var whaleHash = Acc(0x90);
            var whaleStorageTrie = new PatriciaTrie(serverStore, whaleHash);
            var whaleEntries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < whaleEntryCount; i++)
            {
                var key = keccak.CalculateHash(new byte[] { 0x77, (byte)(i >> 8), (byte)(i & 0xff) });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD });
                whaleStorageTrie.Put(key, value);
                whaleEntries.Add((key, value));
            }
            whaleStorageTrie.SaveDirtyNodesToStorage();
            var whaleStorageRoot = whaleStorageTrie.Root.GetHash();
            whaleEntries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));

            var smallAccount = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = smallStorageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var whaleAccount = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)100,
                StateRoot = whaleStorageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(serverStore);
            stateTrie.Put(smallHash, new AccountEncoder().Encode(smallAccount));
            stateTrie.Put(whaleHash, new AccountEncoder().Encode(whaleAccount));
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new InMemoryBytecodeStore());
            ISnapPeer peer = new InProcessSnapPeer(handler);
            if (wrapPeer != null) peer = wrapPeer(peer);

            var clientNodeStore = new InMemoryContentNodeStore();
            var clientStateStore = new InMemoryStateStore();
            var sinkNodeStore = wrapNodeStore != null ? wrapNodeStore(clientNodeStore) : clientNodeStore;
            var sink = new TrieSnapSyncSink(sinkNodeStore, clientStateStore, flatWriter);
            var client = new SnapSyncClient(peer, sink, responseBytesBudget: responseBytesBudget, metrics: metrics)
            {
                AccountConcurrency = 1,
                CheckpointBytesThreshold = 1,
            };
            configure?.Invoke(client);

            var scenario = new Scenario
            {
                StateRoot = stateRoot,
                SmallAccountHash = smallHash,
                SmallStorageRoot = smallStorageRoot,
                SmallSlotValue = smallSlotValue,
                WhaleAccountHash = whaleHash,
                WhaleStorageRoot = whaleStorageRoot,
                WhaleEntries = whaleEntries,
                ClientNodeStore = clientNodeStore,
                Checkpoints = new List<SnapSyncClient.SnapSyncCheckpoint>(),
            };
            scenario.Result = await client.SyncStateWithCheckpointAsync(
                stateRoot, resumeFrom: null, checkpointSink: cp =>
                {
                    scenario.Checkpoints.Add(cp);
                    onCheckpoint?.Invoke(scenario, cp);
                });
            return scenario;
        }

        [Fact]
        public async Task Storage_RangeTruncated_CreatesCursoredSubTasks_NoDebtUntilDrain()
        {
            var s = await RunAsync();

            var creationCheckpoint = s.Checkpoints.First(cp =>
                cp.State.Tasks[0].SubTasks.ContainsKey(s.WhaleAccountHash));
            var subTasks = creationCheckpoint.State.Tasks[0].SubTasks[s.WhaleAccountHash];
            Assert.True(subTasks.Count > 1, "a whale must be chunked into multiple cursored subtasks");
            Assert.Equal(new byte[32], subTasks.OrderBy(t => t.Next, ByteArrayComparer.Current).First().Next,
                ByteArrayComparer.Current);
            Assert.Equal(SnapHashRanges.FilledHash(0xff),
                subTasks.OrderBy(t => t.Last, ByteArrayComparer.Current).Last().Last,
                ByteArrayComparer.Current);
            foreach (var st in subTasks)
                Assert.Equal(s.WhaleStorageRoot, st.StorageRoot, ByteArrayComparer.Current);

            Assert.Empty(creationCheckpoint.DeferredStorageDebts);

            Assert.True(s.Result.RootMatchesTarget);
        }

        [Fact]
        public async Task Storage_ChunkedWhale_SingleRootDrain_NoDebt()
        {
            var s = await RunAsync();

            Assert.DoesNotContain(s.Result.AccountsNeedingHeal,
                a => ByteArrayComparer.Current.Equals(a.AccountHash, s.WhaleAccountHash));

            var finalCheckpoint = s.Checkpoints[^1];
            Assert.DoesNotContain(finalCheckpoint.DeferredStorageDebts,
                d => ByteArrayComparer.Current.Equals(d.AccountHash, s.WhaleAccountHash));

            var finalTask = finalCheckpoint.State.Tasks[0];
            Assert.Contains(s.WhaleAccountHash, finalTask.StorageCompleted, ByteArrayComparer.Current);
            Assert.False(finalTask.SubTasks.ContainsKey(s.WhaleAccountHash)
                && finalTask.SubTasks[s.WhaleAccountHash].Count > 0);
        }

        [Fact]
        public async Task Storage_Whale_DrainsFullyThroughSubtasks_MatchesOracle()
        {
            var s = await RunAsync();

            Assert.True(s.Result.RootMatchesTarget);

            var reloaded = PatriciaTrie.LoadFromStorage(s.WhaleStorageRoot, s.ClientNodeStore, s.WhaleAccountHash);
            var stored = PatriciaRangeIterator.EnumerateRange(reloaded.Root, s.ClientNodeStore, new byte[32])
                .ToDictionary(e => e.KeyBytes.ToHex(), e => e.Value, System.StringComparer.Ordinal);
            Assert.Equal(s.WhaleEntries.Count, stored.Count);
            foreach (var (key, value) in s.WhaleEntries)
                Assert.Equal(value, stored[key.ToHex()]);

            var smallReloaded = PatriciaTrie.LoadFromStorage(s.SmallStorageRoot, s.ClientNodeStore, s.SmallAccountHash);
            Assert.Equal(s.SmallStorageRoot, smallReloaded.Root.GetHash(), ByteArrayComparer.Current);
            Assert.DoesNotContain(s.Checkpoints[^1].DeferredStorageDebts,
                d => ByteArrayComparer.Current.Equals(d.AccountHash, s.SmallAccountHash));
        }

        [Fact]
        public async Task Given_ACursoredWhaleSubtaskDownloadingPages_When_TheCheckpointedCountersAndMetricsAreRead_Then_SlotsAndBytesAdvanceWithEachAppliedPage()
        {
            var meterName = "WhaleProgress-" + Guid.NewGuid().ToString("N");
            using var listener = new SnapSyncMetricsWiringTests.CapturingMeterListener(meterName + ".SnapSync");
            using var metrics = new SnapSyncMetrics(meterName);

            PacedStoragePeer countingPeer = null;

            var s = await RunAsync(
                metrics: metrics,
                wrapPeer: inner => countingPeer = new PacedStoragePeer(inner, TimeSpan.Zero),
                configure: c => c.LargeContractConcurrency = 1);

            Assert.True(s.Result.RootMatchesTarget);
            Assert.True(countingPeer.StorageRequests > 3,
                $"the whale must span several pages for this test to be meaningful (requests={countingPeer.StorageRequests})");
            var expectedSlots = 1UL + (ulong)s.WhaleEntries.Count;
            var expectedBytes = (ulong)s.SmallSlotValue.Length + (ulong)s.WhaleEntries.Sum(e => e.Value.Length);

            lock (listener.Counters)
            {
                Assert.Equal((long)expectedSlots, listener.Counters.GetValueOrDefault("snap.phase2.storage_slots.synced"));
                Assert.Equal((long)expectedBytes, listener.Counters.GetValueOrDefault("snap.phase2.storage_bytes.synced"));
            }

            var final = s.Checkpoints[^1].State.Counters;
            Assert.Equal(expectedSlots, final.StorageSlotsSynced);
            Assert.Equal(expectedBytes, final.StorageBytes);

            var slotsWhileWhaleInFlight = s.Checkpoints
                .Where(cp => cp.State.Tasks[0].SubTasks.ContainsKey(s.WhaleAccountHash))
                .Select(cp => cp.State.Counters.StorageSlotsSynced)
                .ToList();
            Assert.True(slotsWhileWhaleInFlight.Count > 1,
                $"no checkpoint was emitted while the whale was downloading, so the progress reporter's counters stay frozen (checkpoints={slotsWhileWhaleInFlight.Count})");
            for (int i = 1; i < slotsWhileWhaleInFlight.Count; i++)
                Assert.True(slotsWhileWhaleInFlight[i] > slotsWhileWhaleInFlight[i - 1],
                    $"checkpointed slots froze while the whale was downloading: {string.Join(",", slotsWhileWhaleInFlight)}");
        }

        [Fact]
        public async Task Given_ACursoredWhaleIsTheOnlyWorkLeft_When_ItsPagesArriveSlowerThanTheStallTimeout_Then_TheLivenessSupervisorSeesProgressAndTheSyncCompletes()
        {
            var pageDelay = TimeSpan.FromMilliseconds(40);
            var stallTimeout = TimeSpan.FromMilliseconds(250);
            PacedStoragePeer slowPeer = null;

            var s = await RunAsync(
                whaleEntryCount: 160,
                wrapPeer: inner => slowPeer = new PacedStoragePeer(inner, pageDelay),
                configure: c =>
                {
                    c.LargeContractConcurrency = 1;
                    c.TaskSetStallTimeout = stallTimeout;
                });

            Assert.True(s.Result.RootMatchesTarget);
            Assert.True(slowPeer.StorageRequests * pageDelay > stallTimeout * 2,
                $"the whale must outlast the stall timeout for this test to be meaningful (requests={slowPeer.StorageRequests})");
        }

        [Fact]
        public async Task Given_ACheckpointPromotesAnInFlightWhaleCursor_When_TheStoresAreReadAtThatCheckpoint_Then_EverySlotBelowTheDurableCursorIsAlreadyPersisted()
        {
            var flat = new BufferedFlatWriter();
            var gaps = new List<string>();
            var inspected = 0;

            var s = await RunAsync(
                flatWriter: flat,
                configure: c =>
                {
                    c.LargeContractConcurrency = 1;
                    c.FlushBulkFlatBeforeCheckpoint = flat.Flush;
                },
                onCheckpoint: (scenario, cp) =>
                {
                    if (!cp.State.Tasks[0].SubTasks.TryGetValue(scenario.WhaleAccountHash, out var subtasks)) return;
                    if (subtasks.Count != 1)
                    {
                        gaps.Add($"expected one whale subtask, found {subtasks.Count}");
                        return;
                    }
                    var durableNext = subtasks[0].Next;
                    var durable = scenario.WhaleEntries
                        .Where(e => ByteArrayComparer.Current.Compare(e.Key, durableNext) < 0)
                        .ToList();
                    if (durable.Count == 0) return;
                    inspected++;
                    gaps.AddRange(MissingFromPersistedTrie(scenario, durable, durableNext));
                    foreach (var (key, _) in durable)
                        if (!flat.IsDurable(scenario.WhaleAccountHash, key))
                            gaps.Add($"flat row 0x{key.ToHex()} not durable at durable_next=0x{durableNext.ToHex()}");
                });

            Assert.True(s.Result.RootMatchesTarget);
            Assert.True(inspected > 1,
                $"a checkpoint must promote a mid-whale cursor for this test to be meaningful (inspected={inspected})");
            Assert.True(gaps.Count == 0,
                $"{gaps.Count} slots below a promoted cursor were not durable: {string.Join(" | ", gaps.Distinct().Take(4))}");
        }

        [Fact]
        public async Task Given_TheNodeStoreFlushIsWriteStalledDuringACheckpoint_When_AnInFlightWhaleCursorIsPromoted_Then_TheCheckpointCompletesAndPhase2Continues()
        {
            StallableNodeStore stallable = null;
            var stalledCheckpoints = 0;

            var s = await RunAsync(
                wrapNodeStore: inner => stallable = new StallableNodeStore(inner),
                configure: c =>
                {
                    c.LargeContractConcurrency = 1;
                    c.FlushBulkFlatBeforeCheckpoint = () => stallable.Stalled = true;
                },
                onCheckpoint: (scenario, cp) =>
                {
                    stallable.Stalled = false;
                    if (cp.State.Tasks[0].SubTasks.ContainsKey(scenario.WhaleAccountHash)) stalledCheckpoints++;
                });

            Assert.True(s.Result.RootMatchesTarget);
            Assert.True(stalledCheckpoints > 1,
                $"a write stall must hit a checkpoint that promotes a mid-whale cursor for this test to be meaningful (checkpoints={stalledCheckpoints})");
        }

        [Fact]
        public async Task Given_TheNodeStoreFlushIsWriteStalledWhileAWhaleIsInFlight_When_TheWhaleOwnerCompletes_Then_TheOwnerIsMarkedCompleteAndPhase2Continues()
        {
            StallableNodeStore stallable = null;
            var ownerCompletedWhileStalled = false;

            var s = await RunAsync(
                wrapNodeStore: inner => stallable = new StallableNodeStore(inner),
                configure: c => c.LargeContractConcurrency = 1,
                onCheckpoint: (scenario, cp) =>
                {
                    var whaleInFlight = cp.State.Tasks[0].SubTasks.ContainsKey(scenario.WhaleAccountHash);
                    if (whaleInFlight)
                    {
                        stallable.Stalled = true;
                        return;
                    }
                    if (stallable.Stalled
                        && cp.State.Tasks[0].StorageCompleted.Contains(scenario.WhaleAccountHash, ByteArrayComparer.Current))
                        ownerCompletedWhileStalled = true;
                    stallable.Stalled = false;
                });

            Assert.True(s.Result.RootMatchesTarget);
            Assert.True(ownerCompletedWhileStalled,
                "the whale owner must complete inside the write-stall window for this test to be meaningful");
        }

        [Fact]
        public async Task Given_TheNodeStoreFlushIsWriteStalledForAllOfPhase2_When_Phase2Finalises_Then_ItCompletesAndReturnsTheTargetRoot()
        {
            StallableNodeStore stallable = null;

            var s = await RunAsync(wrapNodeStore: inner => stallable = new StallableNodeStore(inner) { Stalled = true });

            Assert.True(s.Result.RootMatchesTarget);
            Assert.Equal(s.StateRoot, s.Result.ComputedRoot, ByteArrayComparer.Current);
            Assert.True(stallable.StalledFlushes > 0,
                "a flush must hit the write stall for this test to be meaningful");
        }

        private sealed class StallableNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            private int _stalledFlushes;

            public StallableNodeStore(ITrieNodeStore inner) => _inner = inner;

            public bool Stalled { get; set; }

            public int StalledFlushes => Volatile.Read(ref _stalledFlushes);

            public void Flush()
            {
                if (Stalled)
                {
                    Interlocked.Increment(ref _stalledFlushes);
                    throw new TransientFlushUnavailableException("writes transiently stopped");
                }
                _inner.Flush();
            }

            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);

            public byte[] Get(Node reference) => _inner.Get(reference);

            public bool Contains(Node reference) => _inner.Contains(reference);

            public void Clear() => _inner.Clear();

            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        }

        private static IEnumerable<string> MissingFromPersistedTrie(
            Scenario scenario, List<(byte[] Key, byte[] Value)> durable, byte[] durableNext)
        {
            var oracle = new PatriciaTrie();
            foreach (var (key, value) in durable) oracle.Put(key, value);
            var durableRoot = oracle.Root.GetHash();
            var missing = new List<string>();
            try
            {
                var persisted = PatriciaTrie.LoadFromStorage(durableRoot, scenario.ClientNodeStore, scenario.WhaleAccountHash);
                foreach (var (key, value) in durable)
                    if (!ByteUtil.AreEqual(persisted.Get(key), value))
                        missing.Add($"trie slot 0x{key.ToHex()} not persisted at durable_next=0x{durableNext.ToHex()}");
            }
            catch (Exception ex)
            {
                missing.Add($"trie for {durable.Count} slots not persisted at durable_next=0x{durableNext.ToHex()} ({ex.GetType().Name})");
            }
            return missing;
        }

        private sealed class BufferedFlatWriter : ISnapFlatStateWriter
        {
            private readonly object _gate = new();
            private readonly HashSet<string> _buffered = new();
            private readonly HashSet<string> _durable = new();

            public void Flush()
            {
                lock (_gate)
                {
                    _durable.UnionWith(_buffered);
                    _buffered.Clear();
                }
            }

            public bool IsDurable(byte[] accountHash, byte[] slotHash)
            {
                lock (_gate) return _durable.Contains(accountHash.ToHex() + ":" + slotHash.ToHex());
            }

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
                => throw new NotSupportedException();

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => Task.CompletedTask;

            public Task DeleteAccountByHashAsync(byte[] accountHash) => Task.CompletedTask;

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                lock (_gate) _buffered.Add(accountHash.ToHex() + ":" + slotKeccak.ToHex());
                return Task.CompletedTask;
            }
        }

        private sealed class PacedStoragePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly TimeSpan _delay;
            private int _storageRequests;

            public PacedStoragePeer(ISnapPeer inner, TimeSpan delay)
            {
                _inner = inner;
                _delay = delay;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public int StorageRequests => Volatile.Read(ref _storageRequests);

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _storageRequests);
                if (_delay > TimeSpan.Zero) await Task.Delay(_delay, ct);
                return await _inner.GetStorageRangesAsync(r, ct);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }
    }
}
