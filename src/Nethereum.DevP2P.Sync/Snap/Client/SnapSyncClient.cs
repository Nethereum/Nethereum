#pragma warning disable CS0618
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public class SnapSyncClient
    {
        private readonly StateBackpressureGate _backpressureGate;

        internal ISnapPeer Peer { get; }
        internal ISnapSyncSink Sink { get; }
        internal ulong ResponseBytesBudget { get; }
        internal ILogger Logger { get; }
        internal SnapSyncMetrics Metrics { get; }
        internal SnapPeerRequests Requests { get; }

        public SnapSyncClient(ISnapPeer peer, int accountsPerRequest = 256, ulong responseBytesBudget = 524_288UL)
            : this(peer, sink: null, accountsPerRequest, responseBytesBudget) { }

        public SnapSyncClient(
            ISnapPeer peer,
            ISnapSyncSink sink,
            int accountsPerRequest = 256,
            ulong responseBytesBudget = 524_288UL,
            ILogger logger = null,
            SnapSyncMetrics metrics = null)
        {
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
            Sink = sink ?? new InMemorySnapSyncSink();
            ResponseBytesBudget = responseBytesBudget;
            Logger = logger ?? NullLogger.Instance;
            Metrics = metrics;
            Requests = new SnapPeerRequests(Peer, ResponseBytesBudget);
            _backpressureGate = new StateBackpressureGate(this);
        }

        public Func<CancellationToken, Task<byte[]>> PivotRefresher { get; set; }

        public Action OnPivotRolled { get; set; }

        public Action<byte[]> OnPhase2CycleFrozen { get; set; }

        public Func<IReadOnlyList<SnapSyncAccountTask>, CancellationToken, Task<byte[]>> PivotCatchUp { get; set; }

        public Func<string> StateWriteBackpressure { get; set; }

        public int BytecodeDeadEndNoProgressRounds { get; set; } = 600;

        public int StateBackpressurePollMs { get; set; } = 5_000;

        public TimeSpan StateBackpressureWarnAfter { get; set; } = TimeSpan.FromMinutes(10);

        public TimeSpan Phase2DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public int RootRefreshIntervalMs { get; set; } = (int)PivotTiming.PollInterval.TotalMilliseconds;

        public int AccountConcurrency { get; set; } = 16;

        public TimeSpan TaskSetStallTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan ActiveLeaseStallTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public int ActiveLeaseSameStageFailureBudget { get; set; } = 16;

        public Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default)
            => SyncStateAsync(targetRoot, resumeFrom: null, checkpointSink: null, ct);

        public ulong CheckpointBytesThreshold { get; set; } = 8UL * 1024 * 1024;

        public Action FlushBulkFlatBeforeCheckpoint { get; set; }

        public async Task<SyncResult> SyncStateAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncState> checkpointSink,
            CancellationToken ct = default)
            => await SyncStateWithCheckpointAsync(
                targetRoot,
                resumeFrom,
                checkpointSink == null ? null : cp => checkpointSink(cp.State),
                ct).ConfigureAwait(false);

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SnapSyncClient.SyncStateWithCheckpointAsync — resumable Phase-2 stream")]
        public async Task<SyncResult> SyncStateWithCheckpointAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncCheckpoint> checkpointSink,
            CancellationToken ct = default)
        {
            var result = await StreamLeavesAsync(targetRoot, resumeFrom, checkpointSink, ct).ConfigureAwait(false);
            ThrowIfRootMismatched(result, PivotCatchUp != null);
            return result;
        }

        internal static void ThrowIfRootMismatched(SyncResult result, bool pivotCatchUpActive)
        {
            if (!result.RootMatchesTarget && !pivotCatchUpActive)
                throw new SnapRootMismatchException(
                    $"Snap-sync result root {result.ComputedRoot.ToHex()} does not match target {result.FinalTargetRoot.ToHex()} — peer returned tampered or incomplete data",
                    result.AccountsNeedingHeal,
                    result.CodeHashesNeedingHeal);
        }

        internal async Task<SyncResult> StreamLeavesAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncCheckpoint> checkpointSink,
            CancellationToken ct)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));
            if (PivotCatchUp != null && checkpointSink == null)
                throw new InvalidOperationException(
                    "A snap/2 pivot catch-up persists the durable task frontier before it runs; Phase 2 needs a checkpoint sink.");

            await Sink.BeginAsync(targetRoot, ct).ConfigureAwait(false);

            var concurrency = Math.Max(1, AccountConcurrency);
            var largeContractConcurrency = Math.Max(1, LargeContractConcurrency);

            var seedTasks = SnapPhase2Seed.Build(resumeFrom, concurrency, SnapPhase2Seed.UndersizedResumeWarning(Logger));

            Logger.LogInformation(
                "snap.phase2.concurrency account_workers={AccountWorkers} whale_partitions={WhalePartitions}",
                concurrency, largeContractConcurrency);

            var state = new SnapPhase2State(resumeFrom?.Counters, Metrics);

            var taskSet = new SnapTaskSet(seedTasks)
            {
                MaxRequestSizeBytes = ResponseBytesBudget,
                LargeContractConcurrency = largeContractConcurrency,
            };
            if (Sink is Nethereum.DevP2P.Sync.Snap.Sinks.IStorageRootSource rootSource)
                rootSource.OnStorageRootCompleted = (account, local, expected) => taskSet.MarkNeedHealIfRootMismatch(account, local, expected);

            var checkpointer = new SnapPhase2Checkpointer(
                state, taskSet, resumeFrom, checkpointSink, CheckpointBytesThreshold, FlushBulkFlatBeforeCheckpoint);

            var run = new SnapPhase2Run(this, targetRoot, resumeFrom, checkpointSink, seedTasks, state, taskSet, checkpointer);
            return await run.RunAsync(ct);
        }

        public static Task<Task> WaitForFirstFaultedConsumerAsync(IReadOnlyList<Task> consumers)
            => Phase2Watchdog.WaitForFirstFaultedConsumerAsync(consumers);

        public Task RunPhase2LivenessSupervisorAsync(
            SnapTaskSet taskSet,
            ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeAccountRangeLeases,
            Func<long> getProgressSnapshot,
            CancellationToken ct)
            => new Phase2Watchdog(this).RunSupervisorAsync(taskSet, activeAccountRangeLeases, getProgressSnapshot, ct);

        public Task WaitWhileStateBackpressuredAsync(int consumerIdx, CancellationToken ct)
            => _backpressureGate.WaitAsync(consumerIdx, ct);

        public Task DrainPhase2AttemptAsync(Task consumersTask, Exception supervisorFailure, CancellationToken outerCt)
            => new Phase2Watchdog(this).DrainAttemptAsync(consumersTask, supervisorFailure, outerCt);

        public int LargeContractConcurrency { get; set; } = 16;

        public async Task<bool> FetchPageStorageAsync(
            byte[] stateRoot,
            List<(byte[] Hash, byte[] Root)> storageAccounts,
            AccountWorkerResult page,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            ulong reqId,
            Action markProductive,
            Action markFailure,
            SnapTaskSet taskSet,
            int taskIndex,
            bool cursoredWhalesSupported,
            Func<byte[]> getLiveRoot,
            CancellationToken ct,
            byte[][] frozenRootHolder = null)
        {
            var inline = !cursoredWhalesSupported || taskSet == null;
            var set = inline
                ? new SnapTaskSet(1) { MaxRequestSizeBytes = ResponseBytesBudget, LargeContractConcurrency = LargeContractConcurrency }
                : taskSet;
            var frozen = FrozenRoot.Wrap(frozenRootHolder);
            var alreadyMarked = new HashSet<byte[]>(set.AccountsNeedingHeal, ByteArrayComparer.Current);

            var complete = await new BatchStorageFetch(this, new StorageFetchContext
            {
                Page = page,
                TaskSet = set,
                TaskIndex = inline ? 0 : taskIndex,
                StorageNodeStore = StorageNodeStoreOrNull(),
                GetLiveRoot = getLiveRoot,
                Frozen = frozen,
                MarkProductive = markProductive,
                MarkFailure = markFailure,
                ReqIds = new ulong[] { reqId },
            }).RunAsync(stateRoot, storageAccounts, ct).ConfigureAwait(false);

            if (inline)
                await DrainLargeContractsInlineAsync(set, stateRoot, page, frozen, markFailure, ct).ConfigureAwait(false);

            foreach (var account in set.AccountsNeedingHeal)
            {
                if (alreadyMarked.Contains(account)) continue;
                var root = storageAccounts.Find(a => ByteArrayComparer.Current.Equals(a.Hash, account)).Root ?? new byte[32];
                accountsNeedingHeal.Add(new AccountNeedingHeal(account, root));
            }
            return complete;
        }

        public async Task ProcessStorageSubtaskAsync(
            SnapFragment.StorageSubtask frag,
            SnapTaskSet taskSet,
            ITrieNodeStore storageNodeStore,
            byte[] stateRoot,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            CancellationToken ct,
            byte[][] frozenRootHolder = null,
            AccountWorkerResult page = null)
        {
            var alreadyMarked = new HashSet<byte[]>(taskSet.AccountsNeedingHeal, ByteArrayComparer.Current);
            await new CursoredStorageSubtask(this, taskSet, storageNodeStore, FrozenRoot.Wrap(frozenRootHolder))
                .RunAsync(frag, stateRoot, page, ct).ConfigureAwait(false);
            foreach (var account in taskSet.AccountsNeedingHeal)
                if (!alreadyMarked.Contains(account))
                    accountsNeedingHeal.Add(new AccountNeedingHeal(account, frag.StorageRoot));
        }

        private async Task DrainLargeContractsInlineAsync(
            SnapTaskSet set, byte[] stateRoot, AccountWorkerResult page, FrozenRoot frozen, Action markFailure, CancellationToken ct)
        {
            CursoredStorageSubtask subtask = null;
            while (set.LeaseLargeSubtask() is SnapFragment.StorageSubtask frag)
            {
                subtask ??= new CursoredStorageSubtask(this, set, RequireCursoredStorage(), frozen, markFailure);
                await subtask.RunAsync(frag, stateRoot, page, ct).ConfigureAwait(false);
            }
        }

        internal ITrieNodeStore StorageNodeStoreOrNull()
            => (Sink as TrieSnapSyncSink)?.NodeStore ?? (Sink as InMemorySnapSyncSink)?.NodeStore;

        internal ITrieNodeStore RequireCursoredStorage()
        {
            var store = StorageNodeStoreOrNull();
            var flatWriter = (Sink as TrieSnapSyncSink)?.FlatWriter ?? (Sink as FlatSnapSyncSink)?.FlatWriter;
            if (store == null && flatWriter == null)
                throw new NotSupportedException(
                    $"snap sink {Sink.GetType().Name} keeps neither a trie node store nor a flat writer; " +
                    "large contracts are pulled as cursored subtasks, which need one of them");
            return store;
        }

        public class SyncResult
        {
            public ISnapSyncSink Sink { get; set; }
            public byte[] ComputedRoot { get; set; }
            public bool RootMatchesTarget { get; set; }
            public int AccountCount { get; set; }

            public PatriciaTrie StateTrie { get; set; }
            public InMemoryContentNodeStore TrieStorage { get; set; }
            public Dictionary<string, byte[]> BytecodeByHash { get; set; } = new();

            public byte[] FinalTargetRoot { get; set; }

            public IReadOnlyList<AccountNeedingHeal> AccountsNeedingHeal { get; set; }
                = Array.Empty<AccountNeedingHeal>();

            public IReadOnlyList<byte[]> CodeHashesNeedingHeal { get; set; }
                = Array.Empty<byte[]>();
        }

        public sealed record AccountNeedingHeal(byte[] AccountHash, byte[] ExpectedStorageRoot);

        public sealed record SnapSyncCheckpoint(
            SnapSyncState State,
            [property: Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
            IReadOnlyList<DeferredStorageDebt> DeferredStorageDebts,
            IReadOnlyList<byte[]> DeferredCodeHashes)
        {
            public IReadOnlyList<AccountNeedingHeal> AccountsNeedingHeal { get; init; } = Array.Empty<AccountNeedingHeal>();
        }

        public sealed class SnapRootMismatchException : InvalidOperationException
        {
            public IReadOnlyList<AccountNeedingHeal> AccountsNeedingHeal { get; }
            public IReadOnlyList<byte[]> CodeHashesNeedingHeal { get; }
            public SnapRootMismatchException(
                string message,
                IReadOnlyList<AccountNeedingHeal> accountsNeedingHeal,
                IReadOnlyList<byte[]> codeHashesNeedingHeal = null)
                : base(message)
            {
                AccountsNeedingHeal = accountsNeedingHeal ?? System.Array.Empty<AccountNeedingHeal>();
                CodeHashesNeedingHeal = codeHashesNeedingHeal ?? System.Array.Empty<byte[]>();
            }
        }

        public sealed class SnapTaskSetStalledException : InvalidOperationException
        {
            public SnapTaskSetDiagnostics Diagnostics { get; }
            public int Consumer { get; }
            public TimeSpan IdleFor { get; }
            public long ProgressSnapshot { get; }

            public SnapTaskSetStalledException(int consumer, TimeSpan idleFor, long progressSnapshot, SnapTaskSetDiagnostics diagnostics)
                : base($"Snap Phase 2 task set stalled: consumer={consumer} ownerless_idle_sec={(long)idleFor.TotalSeconds} progress={progressSnapshot} all_done={diagnostics?.AllDone} has_leasable={diagnostics?.HasLeasableWork} range_inflight={diagnostics?.RangeInFlightCount} range_pending={diagnostics?.RangePendingCount}")
            {
                Consumer = consumer;
                IdleFor = idleFor;
                ProgressSnapshot = progressSnapshot;
                Diagnostics = diagnostics;
            }
        }

        public sealed class SnapPhase2UndrainedException : InvalidOperationException
        {
            public TimeSpan DrainTimeout { get; }
            public SnapPhase2UndrainedException(string message, TimeSpan drainTimeout, Exception inner)
                : base(message, inner) => DrainTimeout = drainTimeout;
        }

        public sealed class SnapTaskLeaseStalledException : InvalidOperationException
        {
            public SnapTaskSetDiagnostics Diagnostics { get; }
            public ActiveLeaseSnapshot ActiveLease { get; }
            public TimeSpan IdleFor { get; }

            public SnapTaskLeaseStalledException(TimeSpan idleFor, ActiveLeaseSnapshot activeLease, SnapTaskSetDiagnostics diagnostics)
                : base($"Snap Phase 2 active lease stalled: active_idle_sec={(long)idleFor.TotalSeconds} active_leases={activeLease?.Count} oldest_task={activeLease?.OldestTaskIndex} oldest_origin={activeLease?.OldestOrigin} oldest_limit={activeLease?.OldestLimit} all_done={diagnostics?.AllDone} has_leasable={diagnostics?.HasLeasableWork}")
            {
                IdleFor = idleFor;
                ActiveLease = activeLease;
                Diagnostics = diagnostics;
            }
        }

        public sealed class ActiveSnapLeaseInfo
        {
            public int Consumer { get; }
            public int TaskIndex { get; }
            public string Origin { get; }
            public string Limit { get; }
            public long StartedTick { get; }
            private long _lastActivityTick;
            private long _lastProductiveTick;

            private ActiveSnapLeaseInfo(int consumer, SnapFragment.AccountRange fragment)
            {
                Consumer = consumer;
                TaskIndex = fragment.TaskIndex;
                Origin = ShortHash(fragment.Origin);
                Limit = ShortHash(fragment.Limit);
                StartedTick = Environment.TickCount64;
                _lastActivityTick = StartedTick;
                _lastProductiveTick = StartedTick;
            }

            public long LastActivityTick => Interlocked.Read(ref _lastActivityTick);
            public long LastProductiveTick => Interlocked.Read(ref _lastProductiveTick);

            public void MarkActivity() => Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);
            public void MarkProductive()
            {
                var now = Environment.TickCount64;
                Interlocked.Exchange(ref _lastActivityTick, now);
                Interlocked.Exchange(ref _lastProductiveTick, now);
            }

            public static ActiveSnapLeaseInfo Create(int consumer, SnapFragment.AccountRange fragment)
                => new ActiveSnapLeaseInfo(consumer, fragment);

            private static string ShortHash(byte[] value)
            {
                if (value == null || value.Length == 0) return string.Empty;
                var hex = value.ToHex();
                return hex.Length <= 16 ? hex : hex.Substring(0, 16);
            }
        }

        public sealed record ActiveLeaseSnapshot(
            int Count,
            int OldestConsumer,
            int OldestTaskIndex,
            string OldestOrigin,
            string OldestLimit,
            long OldestAgeSeconds);

        public sealed class AccountWorkerResult
        {
            public ulong AccountsSyncedDelta;
            public ulong AccountBytesDelta;
            public ulong StorageSlotsSyncedDelta;
            public ulong StorageBytesDelta;
            public List<AccountNeedingHeal> AccountsNeedingHeal = new();
            private readonly object _storageGate = new();

            public void AddStorageSlot(byte[] value)
            {
                lock (_storageGate)
                {
                    StorageSlotsSyncedDelta++;
                    if (value != null) StorageBytesDelta += (ulong)value.Length;
                }
            }
        }
    }
}
