using System;
using System.Threading;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Phase2
{
    internal sealed class SnapPhase2Checkpointer
    {
        private readonly SnapPhase2State _state;
        private readonly SnapTaskSet _taskSet;
        private readonly SnapSyncState _resumeFrom;
        private readonly Action<SnapSyncClient.SnapSyncCheckpoint> _sink;
        private readonly ulong _bytesThreshold;
        private readonly Action _flushBulkFlat;
        private readonly object _lock = new object();
        private bool _sealed;

        public SnapPhase2Checkpointer(
            SnapPhase2State state,
            SnapTaskSet taskSet,
            SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> sink,
            ulong bytesThreshold,
            Action flushBulkFlat = null)
        {
            _state = state;
            _taskSet = taskSet;
            _resumeFrom = resumeFrom;
            _sink = sink;
            _bytesThreshold = bytesThreshold;
            _flushBulkFlat = flushBulkFlat;
        }

        public SnapSyncState BuildCheckpointState(SnapPhase phase, byte[] healTargetRoot)
        {
            var snapshot = _taskSet.GetCheckpointSnapshot();
            var p = _state.ReadProgressCounters();
            return new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = phase,
                PivotBlockNumber = _resumeFrom?.PivotBlockNumber ?? 0,
                PivotBlockHash = _resumeFrom?.PivotBlockHash ?? new byte[32],
                HealTargetRoot = healTargetRoot ?? _resumeFrom?.HealTargetRoot ?? new byte[32],
                Tasks = snapshot,
                Counters = new SnapSyncCounters
                {
                    AccountsSynced = p.AccountsSynced,
                    AccountBytes = p.AccountBytes,
                    StorageSlotsSynced = p.StorageSlotsSynced,
                    StorageBytes = p.StorageBytes,
                    BytecodesSynced = p.BytecodesSynced,
                    BytecodeBytes = p.BytecodeBytes,
                    TrieNodesHealed = _resumeFrom?.Counters?.TrieNodesHealed ?? 0,
                    TrieNodeBytesHealed = _resumeFrom?.Counters?.TrieNodeBytesHealed ?? 0,
                    BytecodesHealed = _resumeFrom?.Counters?.BytecodesHealed ?? 0,
                },
            };
        }

        public void MaybeCheckpoint(ulong addedBytes)
        {
            if (_sink == null || Volatile.Read(ref _sealed)) return;
            var newTotal = _state.AddCheckpointBytes(addedBytes);
            if (newTotal < (long)_bytesThreshold) return;
            if (_state.ExchangeResetCheckpointBytes() < (long)_bytesThreshold)
                return;
            Emit(SnapPhase.Phase2Running, healTargetRoot: null);
        }

        public void Emit(SnapPhase phase, byte[] healTargetRoot)
        {
            lock (_lock)
            {
                if (_sealed) return;
                EmitLocked(phase, healTargetRoot);
            }
        }

        public void EmitFinal(SnapPhase phase, byte[] healTargetRoot)
        {
            lock (_lock)
            {
                if (_sealed) return;
                try
                {
                    EmitLocked(phase, healTargetRoot);
                }
                finally
                {
                    Volatile.Write(ref _sealed, true);
                }
            }
        }

        private void EmitLocked(SnapPhase phase, byte[] healTargetRoot)
        {
            _taskSet.FlushBulkFlatThenPromoteDurability(_flushBulkFlat);
            _sink(new SnapSyncClient.SnapSyncCheckpoint(
                BuildCheckpointState(phase, healTargetRoot),
                _state.SnapshotDeferredStorageDebts(),
                _state.SnapshotDeferredCodeHashes()));
        }
    }
}
