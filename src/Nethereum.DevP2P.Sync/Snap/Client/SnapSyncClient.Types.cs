using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
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
            IReadOnlyList<DeferredStorageDebt> DeferredStorageDebts,
            IReadOnlyList<byte[]> DeferredCodeHashes);

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
        }

        public sealed record ActiveLeaseSnapshot(
            int Count,
            int OldestConsumer,
            int OldestTaskIndex,
            string OldestOrigin,
            string OldestLimit,
            long OldestAgeSeconds);

        private sealed record StorageFetchResult(
            bool Completed,
            bool NeedsHeal,
            DeferredStorageReason? Reason);

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
