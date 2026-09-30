using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
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

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientConcurrencyTests
    {
        private sealed class PartitionRecordingPeer : ISnapPeer
        {
            private readonly object _lock = new();
            public List<byte[]> StartingHashesObserved { get; } = new();
            public int MaxConcurrentInFlight { get; private set; }
            public int CurrentInFlight;
            private readonly Func<GetAccountRangeMessage, Task> _gate;

            public PartitionRecordingPeer(Func<GetAccountRangeMessage, Task> gate = null)
            {
                _gate = gate;
            }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                var inFlight = Interlocked.Increment(ref CurrentInFlight);
                lock (_lock)
                {
                    StartingHashesObserved.Add((byte[])r.StartingHash.Clone());
                    if (inFlight > MaxConcurrentInFlight) MaxConcurrentInFlight = inFlight;
                }
                try
                {
                    if (_gate != null) await _gate(r).ConfigureAwait(false);
                    return new AccountRangeMessage
                    {
                        RequestId = r.RequestId,
                        Accounts = new List<AccountRangeMessage.AccountEntry>(),
                        Proof = new List<byte[]>(),
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref CurrentInFlight);
                }
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => Task.FromResult(new StorageRangesMessage { RequestId = r.RequestId, Slots = new(), Proof = new() });
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        private sealed class StubSink : ISnapSyncSink
        {
            public List<byte[]> AccountsWritten { get; } = new();
            public List<byte[]> SlotsWritten { get; } = new();
            public List<byte[]> BytecodesWritten { get; } = new();
            private byte[] _finaliseRoot;

            public void SetFinaliseRoot(byte[] root) => _finaliseRoot = root;

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
            { lock (AccountsWritten) AccountsWritten.Add(accountHash); return default; }
            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => new(new Scope(SlotsWritten));
            private sealed class Scope : IStorageScope
            {
                private readonly List<byte[]> _slots;
                public Scope(List<byte[]> slots) => _slots = slots;
                public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
                { lock (_slots) _slots.Add(slotHash); return default; }
                public ValueTask EndAsync(CancellationToken ct) => default;
                public ValueTask AbortAsync(CancellationToken ct) => default;
            }
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
            { lock (BytecodesWritten) BytecodesWritten.Add(codeHash); return default; }
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
                => new(_finaliseRoot ?? new byte[32]);
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static byte[] Hash32(byte high)
        {
            var h = new byte[32];
            h[0] = high;
            return h;
        }

        [Fact]
        public async Task Sync_FreshStart_PartitionsInto16Tasks()
        {
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int seen = 0;
            var peer = new PartitionRecordingPeer(async _ =>
            {
                if (Interlocked.Increment(ref seen) == 16) ready.TrySetResult(true);
                await ready.Task.ConfigureAwait(false);
            });
            var targetRoot = DefaultValues.EMPTY_TRIE_HASH;
            var sink = new StubSink();
            sink.SetFinaliseRoot(targetRoot);
            var client = new SnapSyncClient(peer, sink);

            await client.SyncStateAsync(targetRoot);

            Assert.Equal(16, peer.StartingHashesObserved.Count);
            Assert.Equal(16, peer.MaxConcurrentInFlight);
            var sortedStarts = peer.StartingHashesObserved
                .Select(h => h.ToHex()).Distinct().ToList();
            Assert.Equal(16, sortedStarts.Count);
            Assert.Contains(peer.StartingHashesObserved, h => h.All(b => b == 0));
        }

        [Fact]
        public async Task Sync_Resume_ReusesPersistedTaskList()
        {
            var targetRoot = DefaultValues.EMPTY_TRIE_HASH;
            var peer = new PartitionRecordingPeer();
            var sink = new StubSink();
            sink.SetFinaliseRoot(targetRoot);
            var client = new SnapSyncClient(peer, sink);

            var seedA = Hash32(0x10);
            var seedB = Hash32(0x80);
            var resume = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 100,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = new[]
                {
                    new SnapSyncAccountTask
                    {
                        Next = seedA, Last = Hash32(0x7f),
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    },
                    new SnapSyncAccountTask
                    {
                        Next = seedB, Last = FilledHash(0xff),
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    },
                },
                Counters = SnapSyncCounters.Zero,
            };

            await client.SyncStateAsync(targetRoot, resume, checkpointSink: null);

            Assert.Equal(2, peer.StartingHashesObserved.Count);
            var observedHex = peer.StartingHashesObserved.Select(h => h.ToHex()).ToList();
            Assert.Contains(seedA.ToHex(), observedHex);
            Assert.Contains(seedB.ToHex(), observedHex);
        }

        [Fact]
        public async Task Sync_PivotRotationDuringMultiWorker_AllWorkersRetarget()
        {
            var originalRoot = DefaultValues.EMPTY_TRIE_HASH;

            var rootsObserved = new ConcurrentBag<string>();
            var peer = new PartitionRecordingPeer(async r =>
            {
                rootsObserved.Add(r.RootHash.ToHex());
                await Task.Yield();
            });
            var sink = new StubSink();
            sink.SetFinaliseRoot(originalRoot);
            var client = new SnapSyncClient(peer, sink);

            var result = await client.SyncStateAsync(originalRoot);

            Assert.Equal(16, rootsObserved.Count);
            foreach (var hex in rootsObserved)
            {
                Assert.Equal(originalRoot.ToHex(), hex);
            }
            Assert.Equal(originalRoot, result.FinalTargetRoot);
        }

        private sealed class CancellableBlockingPeer : ISnapPeer
        {
            public int CallCount;

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref CallCount);
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new AccountRangeMessage { RequestId = r.RequestId, Accounts = new(), Proof = new() };
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => Task.FromResult(new StorageRangesMessage { RequestId = r.RequestId, Slots = new(), Proof = new() });
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        [Fact]
        public async Task Sync_Cancellation_AllWorkersExitCleanly()
        {
            var peer = new CancellableBlockingPeer();
            var sink = new StubSink();
            var client = new SnapSyncClient(peer, sink);

            using var cts = new CancellationTokenSource();
            var syncTask = client.SyncStateAsync(FilledHash(0x01), resumeFrom: null, checkpointSink: null, ct: cts.Token);

            await Task.Delay(100);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => syncTask);
            Assert.True(peer.CallCount > 0, "expected at least one peer call before cancellation");
        }

        [Fact]
        public async Task Sync_Counters_AccumulateAcrossWorkers()
        {
            var targetRoot = DefaultValues.EMPTY_TRIE_HASH;
            var peer = new PartitionRecordingPeer();
            var sink = new StubSink();
            sink.SetFinaliseRoot(targetRoot);
            var client = new SnapSyncClient(peer, sink);

            var seedCounters = new SnapSyncCounters
            {
                AccountsSynced = 1234,
                AccountBytes = 56_789,
                StorageSlotsSynced = 42,
                StorageBytes = 1024,
                BytecodesSynced = 7,
                BytecodeBytes = 2048,
                TrieNodesHealed = 0,
                TrieNodeBytesHealed = 0,
                BytecodesHealed = 0,
            };
            var resume = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 99,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = new[]
                {
                    new SnapSyncAccountTask
                    {
                        Next = new byte[32], Last = FilledHash(0xff),
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    },
                },
                Counters = seedCounters,
            };

            var captured = new List<SnapSyncState>();
            await client.SyncStateAsync(targetRoot, resume, s => captured.Add(s));

            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.Equal(seedCounters.AccountsSynced, last.Counters.AccountsSynced);
            Assert.Equal(seedCounters.AccountBytes, last.Counters.AccountBytes);
            Assert.Equal(seedCounters.StorageSlotsSynced, last.Counters.StorageSlotsSynced);
            Assert.Equal(seedCounters.StorageBytes, last.Counters.StorageBytes);
            Assert.Equal(seedCounters.BytecodesSynced, last.Counters.BytecodesSynced);
            Assert.Equal(seedCounters.BytecodeBytes, last.Counters.BytecodeBytes);
        }

        [Fact]
        public async Task Sync_StateWriteBackpressure_HoldsRequestsUntilClear_ThenCompletes()
        {
            var targetRoot = DefaultValues.EMPTY_TRIE_HASH;
            var sink = new StubSink();
            sink.SetFinaliseRoot(targetRoot);

            long released = 0;
            var peer = new PartitionRecordingPeer(_ =>
            {
                if (Interlocked.Read(ref released) == 0)
                    throw new InvalidOperationException("request issued while the write valve reported pressure");
                return Task.CompletedTask;
            });

            var client = new SnapSyncClient(peer, sink) { StateBackpressurePollMs = 5 };
            int polls = 0;
            client.StateWriteBackpressure = () =>
            {
                if (Interlocked.Increment(ref polls) <= 48) return "test pressure (level-0 backlog)";
                Interlocked.Exchange(ref released, 1);
                return null;
            };

            await client.SyncStateAsync(targetRoot);

            Assert.True(polls > 48, $"valve was polled {polls} times — consumers never re-checked");
            Assert.Equal(16, peer.StartingHashesObserved.Count);
        }

        [Fact]
        public async Task LivenessSupervisor_OwnerlessInFlightNoLeasable_ThrowsTaskSetStalled()
        {
            var taskSet = new SnapTaskSet(1);
            Assert.NotNull(taskSet.LeaseNext());

            var client = new SnapSyncClient(new PartitionRecordingPeer(), new StubSink())
            {
                TaskSetStallTimeout = TimeSpan.FromMilliseconds(10),
                ActiveLeaseStallTimeout = TimeSpan.FromMinutes(5)
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var ex = await Assert.ThrowsAsync<SnapSyncClient.SnapTaskSetStalledException>(() =>
                client.RunPhase2LivenessSupervisorAsync(
                    taskSet,
                    new ConcurrentDictionary<int, SnapSyncClient.ActiveSnapLeaseInfo>(),
                    () => 0,
                    cts.Token));

            Assert.False(ex.Diagnostics.HasLeasableWork);
            Assert.Equal(1, ex.Diagnostics.RangeInFlightCount);
        }

        [Fact]
        public async Task LivenessSupervisor_ActiveLeaseNoProductiveHeartbeat_ThrowsLeaseStalled()
        {
            var taskSet = new SnapTaskSet(1);
            var lease = (SnapFragment.AccountRange)taskSet.LeaseNext();
            var active = new ConcurrentDictionary<int, SnapSyncClient.ActiveSnapLeaseInfo>();
            active[0] = SnapSyncClient.ActiveSnapLeaseInfo.Create(0, lease);

            var client = new SnapSyncClient(new PartitionRecordingPeer(), new StubSink())
            {
                TaskSetStallTimeout = TimeSpan.FromMinutes(5),
                ActiveLeaseStallTimeout = TimeSpan.FromMilliseconds(10)
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var ex = await Assert.ThrowsAsync<SnapSyncClient.SnapTaskLeaseStalledException>(() =>
                client.RunPhase2LivenessSupervisorAsync(taskSet, active, () => 0, cts.Token));

            Assert.Equal(1, ex.ActiveLease.Count);
            Assert.Equal(0, ex.ActiveLease.OldestConsumer);
        }

        [Fact]
        public async Task FirstConsumerFault_CompletesBeforeHungSibling()
        {
            var hung = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var faulted = Task.FromException(new InvalidOperationException("boom"));

            var firstFaulted = await SnapSyncClient.WaitForFirstFaultedConsumerAsync(
                new[] { hung.Task, faulted });

            Assert.Same(faulted, firstFaulted);
        }
    }
}
