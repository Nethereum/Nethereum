using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Phase2
{
    internal sealed class SnapPhase2State
    {
        private readonly SnapSyncMetrics _metrics;

        private long _accountsSynced;
        private long _accountBytes;
        private long _storageSlotsSynced;
        private long _storageBytes;
        private long _bytecodesSynced;
        private long _bytecodeBytes;
        private long _bytesSinceLastCheckpoint;

        public ConcurrentDictionary<string, DeferredStorageDebt> DeferredStorageDebts { get; }

        public ConcurrentDictionary<byte[], byte> CodeHashesNeedingHeal { get; }

        public SnapPhase2State(SnapSyncCounters resumeCounters, SnapSyncMetrics metrics)
        {
            _metrics = metrics;
            _accountsSynced = (long)(resumeCounters?.AccountsSynced ?? 0);
            _accountBytes = (long)(resumeCounters?.AccountBytes ?? 0);
            _storageSlotsSynced = (long)(resumeCounters?.StorageSlotsSynced ?? 0);
            _storageBytes = (long)(resumeCounters?.StorageBytes ?? 0);
            _bytecodesSynced = (long)(resumeCounters?.BytecodesSynced ?? 0);
            _bytecodeBytes = (long)(resumeCounters?.BytecodeBytes ?? 0);
            _bytesSinceLastCheckpoint = 0;
            DeferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();
            CodeHashesNeedingHeal = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Current);
        }

        public void PublishAccountDeltas(SnapSyncClient.AccountWorkerResult deltas)
        {
            Interlocked.Add(ref _accountsSynced, (long)deltas.AccountsSyncedDelta);
            Interlocked.Add(ref _accountBytes, (long)deltas.AccountBytesDelta);
            Interlocked.Add(ref _storageSlotsSynced, (long)deltas.StorageSlotsSyncedDelta);
            Interlocked.Add(ref _storageBytes, (long)deltas.StorageBytesDelta);
            _metrics?.RecordPhase2AccountsSynced(
                (long)deltas.AccountsSyncedDelta, (long)deltas.AccountBytesDelta);
            _metrics?.RecordPhase2StorageSynced(
                (long)deltas.StorageSlotsSyncedDelta, (long)deltas.StorageBytesDelta);
        }

        public void PublishBytecodes(int count, ulong bytes)
        {
            if (count <= 0) return;
            Interlocked.Add(ref _bytecodesSynced, count);
            Interlocked.Add(ref _bytecodeBytes, (long)bytes);
            _metrics?.RecordPhase2BytecodesSynced(count);
        }

        public void PublishDeferredCode(IReadOnlyList<byte[]> hashes)
        {
            if (hashes == null) return;
            foreach (var h in hashes) CodeHashesNeedingHeal.TryAdd(h, 0);
        }

        public long ProgressSnapshot() =>
            Interlocked.Read(ref _accountsSynced)
            + Interlocked.Read(ref _accountBytes)
            + Interlocked.Read(ref _storageSlotsSynced)
            + Interlocked.Read(ref _storageBytes)
            + Interlocked.Read(ref _bytecodesSynced)
            + Interlocked.Read(ref _bytecodeBytes);

        public (ulong AccountsSynced, ulong AccountBytes, ulong StorageSlotsSynced,
                ulong StorageBytes, ulong BytecodesSynced, ulong BytecodeBytes) ReadProgressCounters()
            => ((ulong)Interlocked.Read(ref _accountsSynced),
                (ulong)Interlocked.Read(ref _accountBytes),
                (ulong)Interlocked.Read(ref _storageSlotsSynced),
                (ulong)Interlocked.Read(ref _storageBytes),
                (ulong)Interlocked.Read(ref _bytecodesSynced),
                (ulong)Interlocked.Read(ref _bytecodeBytes));

        public long AddCheckpointBytes(ulong addedBytes)
            => Interlocked.Add(ref _bytesSinceLastCheckpoint, (long)addedBytes);

        public long ExchangeResetCheckpointBytes()
            => Interlocked.Exchange(ref _bytesSinceLastCheckpoint, 0);

        public IReadOnlyList<DeferredStorageDebt> SnapshotDeferredStorageDebts()
        {
            if (DeferredStorageDebts.IsEmpty) return Array.Empty<DeferredStorageDebt>();
            var snapshot = new List<DeferredStorageDebt>(DeferredStorageDebts.Count);
            foreach (var debt in DeferredStorageDebts.Values)
                snapshot.Add(debt.Clone());
            return snapshot;
        }

        public IReadOnlyList<byte[]> SnapshotDeferredCodeHashes()
        {
            if (CodeHashesNeedingHeal.IsEmpty) return Array.Empty<byte[]>();
            return new List<byte[]>(CodeHashesNeedingHeal.Keys);
        }
    }
}
