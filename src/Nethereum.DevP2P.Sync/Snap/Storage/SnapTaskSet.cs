using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public sealed class SnapTaskSet
    {
        private readonly object _gate = new();
        private readonly List<SnapAccountTask> _tasks;
        private readonly Queue<byte[]> _codeQueue = new();
        private readonly HashSet<byte[]> _codeSeen = new(ByteArrayComparer.Current);
        private readonly List<byte[]> _needHeal = new();
        private readonly byte[][] _pendingRangeNext;
        private readonly bool[] _pendingRangeDone;
        private long _leaseRound;
        private int _largeSubtaskRound;

        public int SmallBatchSize { get; set; } = 8;
        public int LargeContractConcurrency { get; set; } = 16;
        public int MaxCodeRequestCount { get; set; } = 84;

        public ulong MaxRequestSizeBytes { get; set; } = 512 * 1024;

        public int AccountRangeFairShareInterval { get; set; } = 2;

        public SnapTaskSet(int partitions)
            : this(SnapHashRanges.SplitHashRange(new byte[32], SnapHashRanges.FilledHash(0xff), partitions)) { }

        public SnapTaskSet(IReadOnlyList<(byte[] Next, byte[] Last)> partitions)
        {
            _tasks = partitions
                .Select(p => new SnapAccountTask { Next = p.Next, Last = p.Last, DurableNext = p.Next })
                .ToList();
            _pendingRangeNext = new byte[_tasks.Count][];
            _pendingRangeDone = new bool[_tasks.Count];
        }

        public SnapTaskSet(IReadOnlyList<SnapSyncAccountTask> persistedTasks)
        {
            _tasks = persistedTasks.Select(HydrateTask).ToList();
            _pendingRangeNext = new byte[_tasks.Count][];
            _pendingRangeDone = new bool[_tasks.Count];
        }

        private static SnapAccountTask HydrateTask(SnapSyncAccountTask persisted)
        {
            var task = new SnapAccountTask { Next = persisted.Next, Last = persisted.Last, DurableNext = persisted.Next };

            foreach (var account in persisted.StorageCompleted ?? Array.Empty<byte[]>())
            {
                task.StorageCompleted.Add(account);
                task.DurableStorageCompleted.Add(account);
            }

            foreach (var kv in persisted.SubTasks ?? new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>())
            {
                if (kv.Value.Count == 0) continue;
                if (task.StorageCompleted.Contains(kv.Key)) continue;
                var contract = new LargeContractStorage
                {
                    AccountHash = kv.Key,
                    StorageRoot = kv.Value[0].StorageRoot,
                    LastReprovedStateRoot = null,
                    Pending = kv.Value.Count,
                };
                contract.Subtasks.AddRange(kv.Value.Select(s => new Subtask { Next = s.Next, Last = s.Last, DurableNext = s.Next }));
                task.LargeContracts[kv.Key] = contract;
            }

            return task;
        }

        public IReadOnlyList<SnapAccountTask> Tasks => _tasks;

        public IReadOnlyList<byte[]> AccountsNeedingHeal
        {
            get { lock (_gate) return _needHeal.ToList(); }
        }

        public bool HasPendingCode
        {
            get { lock (_gate) return _codeQueue.Count > 0; }
        }

        public bool AllDone
        {
            get { lock (_gate) return _tasks.All(t => t.Done) && _codeQueue.Count == 0; }
        }

        public string DescribeNotDone(int maxTasks = 6)
        {
            static string Sh(byte[] b) => b == null ? "null"
                : System.BitConverter.ToString(b, 0, System.Math.Min(4, b.Length)).Replace("-", "").ToLowerInvariant();
            lock (_gate)
            {
                int done = _tasks.Count(t => t.Done);
                var parts = new List<string>();
                int shown = 0;
                for (int i = 0; i < _tasks.Count && shown < maxTasks; i++)
                {
                    var t = _tasks[i];
                    if (t.Done) continue;
                    shown++;
                    int largePending = t.LargeContracts.Values.Count(c => c.Pending > 0);
                    int largeInflight = t.LargeContracts.Values.Sum(c => c.Subtasks.Count(s => s.InFlight));
                    var whale = t.LargeContracts.FirstOrDefault(kv => kv.Value.Pending > 0);
                    string wtxt = whale.Key == null ? ""
                        : $" whale=0x{Sh(whale.Key)}(pending={whale.Value.Pending},inflight={whale.Value.Subtasks.Count(s => s.InFlight)},reproveInFlight={whale.Value.ReproveInFlight})";
                    parts.Add($"t{i}[rangeDone={(t.RangeDone ? "Y" : "N")} next=0x{Sh(t.Next)} state={t.StateTasks.Count} large={t.LargeContracts.Count} largePending={largePending} largeInflight={largeInflight}{wtxt}]");
                }
                return $"tasks={_tasks.Count} done={done} notdone={_tasks.Count - done} codeQueue={_codeQueue.Count} :: {string.Join(" ", parts)}";
            }
        }

        public SnapTaskSetDiagnostics GetDiagnostics()
        {
            lock (_gate)
            {
                int rangeDone = 0;
                int rangeInFlight = 0;
                int rangePending = 0;
                int stateTasks = 0;
                int largeContracts = 0;
                int largeSubtaskPending = 0;
                int largeSubtaskInFlight = 0;
                int largeSubtaskDone = 0;

                foreach (var t in _tasks)
                {
                    if (t.RangeDone) rangeDone++;
                    else if (t.RangeInFlight) rangeInFlight++;
                    else rangePending++;

                    stateTasks += t.StateTasks.Count;
                    largeContracts += t.LargeContracts.Count;
                    foreach (var c in t.LargeContracts.Values)
                    {
                        foreach (var s in c.Subtasks)
                        {
                            if (s.Done) largeSubtaskDone++;
                            else if (s.InFlight) largeSubtaskInFlight++;
                            else largeSubtaskPending++;
                        }
                    }
                }

                bool hasLeasableWork =
                    largeSubtaskPending > 0 ||
                    stateTasks > 0 ||
                    _codeQueue.Count > 0 ||
                    rangePending > 0;

                return new SnapTaskSetDiagnostics(
                    _tasks.Count,
                    rangeDone,
                    rangeInFlight,
                    rangePending,
                    stateTasks,
                    largeContracts,
                    largeSubtaskPending,
                    largeSubtaskInFlight,
                    largeSubtaskDone,
                    _codeQueue.Count,
                    _needHeal.Count,
                    _tasks.All(t => t.Done) && _codeQueue.Count == 0,
                    hasLeasableWork);
            }
        }

        public IReadOnlyList<SnapSyncAccountTask> GetCheckpointSnapshot()
        {
            lock (_gate) return BuildDurableSnapshotLocked(_tasks);
        }

        public IReadOnlyList<SnapSyncAccountTask> GetUnfinishedCheckpointSnapshot()
        {
            lock (_gate) return BuildDurableSnapshotLocked(_tasks.Where(t => !t.Done));
        }

        private List<SnapSyncAccountTask> BuildDurableSnapshotLocked(IEnumerable<SnapAccountTask> tasks)
        {
            var snapshot = new List<SnapSyncAccountTask>(_tasks.Count);
            foreach (var t in tasks)
            {
                var subTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current);
                foreach (var c in t.LargeContracts.Values)
                {
                    var unfinished = c.Subtasks
                        .Where(s => !s.DurableDone)
                        .Select(s => new SnapSyncStorageSubTask
                        {
                            AccountHash = c.AccountHash,
                            Next = s.DurableNext,
                            Last = s.Last,
                            StorageRoot = c.StorageRoot,
                        })
                        .ToList();
                    if (unfinished.Count > 0) subTasks[c.AccountHash] = unfinished;
                }

                snapshot.Add(new SnapSyncAccountTask
                {
                    Next = t.DurableNext,
                    Last = t.Last,
                    StorageCompleted = t.DurableStorageCompleted.ToList(),
                    SubTasks = subTasks,
                });
            }
            return snapshot;
        }

        public void FlushBulkFlatThenPromoteDurability(Action flushBulkFlat)
        {
            lock (_gate)
            {
                flushBulkFlat?.Invoke();
                PromoteFlatDurabilityLocked();
            }
        }

        public void PromoteFlatDurability()
        {
            lock (_gate) PromoteFlatDurabilityLocked();
        }

        private void PromoteFlatDurabilityLocked()
        {
            foreach (var t in _tasks)
            {
                t.DurableNext = t.Next;
                foreach (var a in t.PendingStorageCompletedPromotion) t.DurableStorageCompleted.Add(a);
                t.PendingStorageCompletedPromotion.Clear();
                foreach (var c in t.LargeContracts.Values)
                {
                    (c.Scope as ResumableStorageScope)?.CommitDirtyNodes();
                    foreach (var s in c.Subtasks)
                    {
                        s.DurableNext = s.Next;
                        s.DurableDone = s.Done;
                    }
                }
            }
        }

        public void CompleteOwnerStorageUnderGate(Action flushThenMarkComplete)
        {
            lock (_gate) flushThenMarkComplete();
        }

        private static void MarkStorageCompletedLocked(SnapAccountTask t, byte[] account)
        {
            if (t.StorageCompleted.Add(account)) t.PendingStorageCompletedPromotion.Add(account);
        }

        public SnapFragment LeaseNext()
        {
            lock (_gate)
            {
                var accountRangeTurn = _leaseRound++ % AccountRangeFairShareInterval == 0;
                SnapFragment fragment;

                if (accountRangeTurn && (fragment = TryLeaseAccountRange()) != null) return fragment;
                if ((fragment = TryLeaseLargeSubtask()) != null) return fragment;
                if ((fragment = TryLeaseSmallBatch()) != null) return fragment;
                if ((fragment = TryLeaseCode()) != null) return fragment;
                if (!accountRangeTurn && (fragment = TryLeaseAccountRange()) != null) return fragment;

                return null;
            }
        }

        private SnapFragment TryLeaseAccountRange()
        {
            for (int ti = 0; ti < _tasks.Count; ti++)
            {
                TryAdvanceGatedRangeLocked(ti);
                var t = _tasks[ti];
                if (t.RangeInFlight || t.RangeDone) continue;
                t.RangeInFlight = true;
                return new SnapFragment.AccountRange(ti, t.Next, t.Last);
            }
            return null;
        }

        private SnapFragment TryLeaseLargeSubtask()
        {
            int n = _tasks.Count;
            for (int off = 0; off < n; off++)
            {
                int ti = (_largeSubtaskRound + off) % n;
                foreach (var c in _tasks[ti].LargeContracts.Values)
                {
                    for (int si = 0; si < c.Subtasks.Count; si++)
                    {
                        var s = c.Subtasks[si];
                        if (s.InFlight || s.Done) continue;
                        s.InFlight = true;
                        _largeSubtaskRound = ti + 1;
                        return new SnapFragment.StorageSubtask(ti, c.AccountHash, c.StorageRoot, si, s.Next, s.Last);
                    }
                }
            }
            return null;
        }

        private SnapFragment TryLeaseSmallBatch()
        {
            for (int ti = 0; ti < _tasks.Count; ti++)
            {
                var t = _tasks[ti];
                if (t.StateTasks.Count == 0) continue;
                var items = t.StateTasks.Take(SmallBatchSize)
                    .Select(kv => new SmallBatchItem(kv.Key, kv.Value)).ToArray();
                foreach (var it in items) t.StateTasks.Remove(it.AccountHash);
                return new SnapFragment.SmallStorageBatch(ti, items);
            }
            return null;
        }

        private SnapFragment TryLeaseCode()
        {
            if (_codeQueue.Count == 0) return null;
            var chunk = new List<byte[]>();
            while (_codeQueue.Count > 0 && chunk.Count < MaxCodeRequestCount)
                chunk.Add(_codeQueue.Dequeue());
            return new SnapFragment.Bytecode(chunk.ToArray());
        }

        public void CompleteAccountRange(
            SnapFragment.AccountRange frag, IReadOnlyList<AccountClassification> accounts,
            byte[] lastHash, bool done)
        {
            lock (_gate)
            {
                var t = _tasks[frag.TaskIndex];
                foreach (var a in accounts)
                {
                    if (a.StorageRoot != null && !ByteUtil.AreEqual(a.StorageRoot, DefaultValues.EMPTY_TRIE_HASH))
                        t.StateTasks[a.Hash] = a.StorageRoot;
                    if (a.CodeHash != null && !ByteUtil.AreEqual(a.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                        if (_codeSeen.Add(a.CodeHash)) _codeQueue.Enqueue(a.CodeHash);
                }

                var next = done ? t.Last : SnapHashRanges.IncrementHash(lastHash);
                if (t.HasPendingStorage)
                {
                    _pendingRangeNext[frag.TaskIndex] = next;
                    _pendingRangeDone[frag.TaskIndex] = done;
                    return;
                }

                t.RangeInFlight = false;
                if (done) t.RangeDone = true;
                t.Next = next;
            }
        }

        private void TryAdvanceGatedRangeLocked(int taskIndex)
        {
            var pendingNext = _pendingRangeNext[taskIndex];
            if (pendingNext == null) return;

            var t = _tasks[taskIndex];
            if (t.HasPendingStorage) return;

            t.Next = pendingNext;
            if (_pendingRangeDone[taskIndex]) t.RangeDone = true;
            t.RangeInFlight = false;
            _pendingRangeNext[taskIndex] = null;
        }

        public void TryAdvanceGatedRange(int taskIndex)
        {
            lock (_gate) TryAdvanceGatedRangeLocked(taskIndex);
        }

        public void DiscardParkedRangeAdvances()
        {
            lock (_gate)
            {
                for (int i = 0; i < _tasks.Count; i++)
                {
                    _pendingRangeNext[i] = null;
                    _pendingRangeDone[i] = false;
                }
            }
        }

        public void CompleteSmallBatch(
            SnapFragment.SmallStorageBatch frag, IReadOnlyList<SmallStorageOutcome> outcomes, byte[] stateRootAtCreation)
        {
            lock (_gate)
            {
                var t = _tasks[frag.TaskIndex];
                var roots = frag.Items.ToDictionary(i => i.AccountHash, i => i.StorageRoot, ByteArrayComparer.Current);
                foreach (var o in outcomes)
                {
                    switch (o.Result)
                    {
                        case SmallStorageResult.Done:
                            MarkStorageCompletedLocked(t, o.Account);
                            break;
                        case SmallStorageResult.Heal:
                            _needHeal.Add(o.Account);
                            break;
                        case SmallStorageResult.Large:
                            CreateLargeContractLocked(
                                frag.TaskIndex, o.Account,
                                roots.TryGetValue(o.Account, out var sr) ? sr : null,
                                o.ResumeFrom, stateRootAtCreation, pageSlotCount: 0, lastSlotHash: null);
                            break;
                    }
                }
                TryAdvanceGatedRangeLocked(frag.TaskIndex);
            }
        }

        public void CreateLargeContract(
            int taskIndex, byte[] accountHash, byte[] storageRoot, byte[] stateRootAtCreation,
            int pageSlotCount = 0, byte[] lastSlotHash = null)
        {
            lock (_gate) CreateLargeContractLocked(
                taskIndex, accountHash, storageRoot, new byte[32], stateRootAtCreation, pageSlotCount, lastSlotHash);
        }

        private void CreateLargeContractLocked(
            int taskIndex, byte[] accountHash, byte[] storageRoot, byte[] resumeFrom, byte[] stateRootAtCreation,
            int pageSlotCount, byte[] lastSlotHash)
        {
            var owner = _tasks[taskIndex];
            if (owner.StorageCompleted.Contains(accountHash) || owner.LargeContracts.ContainsKey(accountHash))
                return;

            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                LargeContractConcurrency, MaxRequestSizeBytes, pageSlotCount, lastSlotHash);
            var subs = SnapHashRanges
                .SplitHashRange(resumeFrom, SnapHashRanges.FilledHash(0xff), chunks)
                .Select(r => new Subtask { Next = r.Start, Last = r.End })
                .ToList();
            var contract = new LargeContractStorage
            {
                AccountHash = accountHash,
                StorageRoot = storageRoot,
                LastReprovedStateRoot = stateRootAtCreation,
                Pending = subs.Count,
            };
            contract.Subtasks.AddRange(subs);
            _tasks[taskIndex].LargeContracts[accountHash] = contract;
        }

        public bool CompleteSubtask(
            SnapFragment.StorageSubtask frag, bool moreRemaining, byte[] nextCursor)
        {
            lock (_gate)
            {
                var c = _tasks[frag.TaskIndex].LargeContracts[frag.AccountHash];
                var st = c.Subtasks[frag.SubtaskIndex];
                st.InFlight = false;

                if (st.Done) return c.Pending == 0;

                if (moreRemaining)
                {
                    st.Next = nextCursor;
                    return false;
                }
                st.Done = true;
                c.Pending--;
                return c.Pending == 0;
            }
        }

        public enum ReproveClaim { NotNeeded, Claimed, InFlight }

        public ReproveClaim TryClaimReprove(int taskIndex, byte[] accountHash, byte[] currentStateRoot)
        {
            lock (_gate)
            {
                var c = _tasks[taskIndex].LargeContracts[accountHash];
                if (ByteUtil.AreEqual(c.LastReprovedStateRoot, currentStateRoot)) return ReproveClaim.NotNeeded;
                if (c.ReproveInFlight) return ReproveClaim.InFlight;
                c.ReproveInFlight = true;
                return ReproveClaim.Claimed;
            }
        }

        public byte[] GetStorageRoot(int taskIndex, byte[] accountHash)
        {
            lock (_gate) return _tasks[taskIndex].LargeContracts[accountHash].StorageRoot;
        }

        public void CommitReproveFound(int taskIndex, byte[] accountHash, byte[] newStateRoot, byte[] newStorageRoot)
        {
            lock (_gate)
            {
                var c = _tasks[taskIndex].LargeContracts[accountHash];
                c.StorageRoot = newStorageRoot;
                c.LastReprovedStateRoot = newStateRoot;
                c.ReproveInFlight = false;
            }
        }

        public void CommitReproveAbsentOrEmpty(int taskIndex, byte[] accountHash, byte[] newStateRoot)
        {
            lock (_gate)
            {
                var c = _tasks[taskIndex].LargeContracts[accountHash];
                DrainOwnerBookkeepingLocked(c);
                c.LastReprovedStateRoot = newStateRoot;
                c.ReproveInFlight = false;
                c.StorageRoot = null;
            }
        }

        public void AbandonReprove(int taskIndex, byte[] accountHash)
        {
            lock (_gate) _tasks[taskIndex].LargeContracts[accountHash].ReproveInFlight = false;
        }

        public object GetOrCreateStorageScope(int taskIndex, byte[] accountHash, Func<object> createScope)
        {
            lock (_gate)
            {
                var c = _tasks[taskIndex].LargeContracts[accountHash];
                return c.Scope ??= createScope();
            }
        }

        public void ClearStorageScope(int taskIndex, byte[] accountHash)
        {
            lock (_gate)
            {
                if (_tasks[taskIndex].LargeContracts.TryGetValue(accountHash, out var c))
                    c.Scope = null;
            }
        }

        public void MarkStorageCompleted(byte[] account)
        {
            lock (_gate)
                for (int ti = 0; ti < _tasks.Count; ti++)
                {
                    if (!_tasks[ti].LargeContracts.ContainsKey(account)) continue;
                    MarkStorageCompletedLocked(_tasks[ti], account);
                    TryAdvanceGatedRangeLocked(ti);
                    return;
                }
        }

        public bool CompleteOwnerViaLocalRoot(int taskIndex, byte[] accountHash)
        {
            lock (_gate)
            {
                var t = _tasks[taskIndex];
                bool addedNow = t.StorageCompleted.Add(accountHash);
                if (addedNow) t.PendingStorageCompletedPromotion.Add(accountHash);
                bool clearedPending = t.LargeContracts.TryGetValue(accountHash, out var c) && c.Pending > 0;
                if (clearedPending) DrainOwnerBookkeepingLocked(c);
                return addedNow || clearedPending;
            }
        }

        private static void DrainOwnerBookkeepingLocked(LargeContractStorage c)
        {
            foreach (var s in c.Subtasks) { s.Done = true; s.InFlight = false; }
            c.Pending = 0;
        }

        public void MarkNeedHeal(byte[] account)
        {
            lock (_gate) _needHeal.Add(account);
        }

        public void RevertAllInFlightLeases()
        {
            lock (_gate)
            {
                foreach (var t in _tasks)
                {
                    t.RangeInFlight = false;
                    foreach (var c in t.LargeContracts.Values)
                    {
                        c.ReproveInFlight = false;
                        foreach (var s in c.Subtasks)
                            s.InFlight = false;
                    }
                }
            }
        }

        public void Revert(SnapFragment frag)
        {
            lock (_gate)
            {
                switch (frag)
                {
                    case SnapFragment.AccountRange f:
                        _tasks[f.TaskIndex].RangeInFlight = false;
                        break;
                    case SnapFragment.StorageSubtask f:
                        _tasks[f.TaskIndex].LargeContracts[f.AccountHash].Subtasks[f.SubtaskIndex].InFlight = false;
                        break;
                    case SnapFragment.SmallStorageBatch f:
                        foreach (var it in f.Items) _tasks[f.TaskIndex].StateTasks[it.AccountHash] = it.StorageRoot;
                        break;
                    case SnapFragment.Bytecode f:
                        foreach (var h in f.Hashes) _codeQueue.Enqueue(h);
                        break;
                }
            }
        }
    }

    public sealed record SnapTaskSetDiagnostics(
        int TaskCount,
        int RangeDoneCount,
        int RangeInFlightCount,
        int RangePendingCount,
        int StateTaskCount,
        int LargeContractCount,
        int LargeSubtaskPendingCount,
        int LargeSubtaskInFlightCount,
        int LargeSubtaskDoneCount,
        int CodeQueueCount,
        int NeedHealCount,
        bool AllDone,
        bool HasLeasableWork);
}
