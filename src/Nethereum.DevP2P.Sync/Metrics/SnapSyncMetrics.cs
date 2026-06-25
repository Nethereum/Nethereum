using System;
using System.Diagnostics.Metrics;
using System.Threading;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Metrics
{
    public sealed class SnapSyncMetrics : IDisposable
    {
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;

        private readonly Counter<long> _phaseEntered;
        private readonly Counter<long> _phase1BlocksPersisted;
        private readonly Counter<long> _phase2AccountsSynced;
        private readonly Counter<long> _phase2AccountBytes;
        private readonly Counter<long> _phase2StorageSlotsSynced;
        private readonly Counter<long> _phase2StorageBytes;
        private readonly Counter<long> _phase2BytecodesSynced;
        private readonly Counter<long> _phase3NodesHealed;
        private readonly Counter<long> _phase3PivotRotations;
        private readonly Counter<long> _fetchFailed;
        private readonly Counter<long> _resumeTotal;

        private readonly Histogram<double> _phase1BatchDuration;
        private readonly Histogram<double> _phase2ChunkDuration;
        private readonly Histogram<double> _phase2StatePersistDuration;
        private readonly Histogram<double> _phase3RoundDuration;

        private long _currentPhase;
        private long _phase3QueueDepth;
        private long _peersSnapCapable;
        private long _peersTotal;

        public SnapPhase CurrentPhase => (SnapPhase)Interlocked.Read(ref _currentPhase);
        public long Phase3QueueDepth => Interlocked.Read(ref _phase3QueueDepth);
        public long PeersSnapCapable => Interlocked.Read(ref _peersSnapCapable);
        public long PeersTotal => Interlocked.Read(ref _peersTotal);

        public SnapSyncMetrics(string name = "Nethereum", IMeterFactory meterFactory = null)
        {
            _name = name;
            _meter = meterFactory?.Create($"{name}.SnapSync") ?? new Meter($"{name}.SnapSync");
            _detailedMeter = meterFactory?.Create($"{name}.SnapSync.Detailed")
                ?? new Meter($"{name}.SnapSync.Detailed");

            _phaseEntered = _meter.CreateCounter<long>("snap.phase.entered.total", "{entry}");
            _phase1BlocksPersisted = _meter.CreateCounter<long>("snap.phase1.blocks.persisted", "{block}");
            _phase2AccountsSynced = _meter.CreateCounter<long>("snap.phase2.accounts.synced", "{account}");
            _phase2AccountBytes = _meter.CreateCounter<long>("snap.phase2.account_bytes.synced", "By");
            _phase2StorageSlotsSynced = _meter.CreateCounter<long>("snap.phase2.storage_slots.synced", "{slot}");
            _phase2StorageBytes = _meter.CreateCounter<long>("snap.phase2.storage_bytes.synced", "By");
            _phase2BytecodesSynced = _meter.CreateCounter<long>("snap.phase2.bytecodes.synced", "{bytecode}");
            _phase3NodesHealed = _meter.CreateCounter<long>("snap.phase3.nodes.healed", "{node}");
            _phase3PivotRotations = _meter.CreateCounter<long>("snap.phase3.pivot.rotations", "{rotation}");
            _fetchFailed = _meter.CreateCounter<long>("snap.fetch.failed.total", "{failure}");
            _resumeTotal = _meter.CreateCounter<long>("snap.resume.total", "{resume}");

            _meter.CreateObservableGauge(
                "snap.phase.current",
                () => new Measurement<long>(
                    Interlocked.Read(ref _currentPhase),
                    new System.Collections.Generic.KeyValuePair<string, object>("name", _name)),
                "{phase}");

            _meter.CreateObservableGauge(
                "snap.phase3.queue.depth",
                () => new Measurement<long>(
                    Interlocked.Read(ref _phase3QueueDepth),
                    new System.Collections.Generic.KeyValuePair<string, object>("name", _name)),
                "{node}");

            _meter.CreateObservableGauge(
                "snap.peers.snap_capable",
                () => new Measurement<long>(
                    Interlocked.Read(ref _peersSnapCapable),
                    new System.Collections.Generic.KeyValuePair<string, object>("name", _name)),
                "{peer}");

            _meter.CreateObservableGauge(
                "snap.peers.total",
                () => new Measurement<long>(
                    Interlocked.Read(ref _peersTotal),
                    new System.Collections.Generic.KeyValuePair<string, object>("name", _name)),
                "{peer}");

            _phase1BatchDuration = _detailedMeter.CreateHistogram<double>("snap.phase1.batch.duration", "s");
            _phase2ChunkDuration = _detailedMeter.CreateHistogram<double>("snap.phase2.chunk.duration", "s");
            _phase2StatePersistDuration = _detailedMeter.CreateHistogram<double>("snap.phase2.state.persist.duration", "s");
            _phase3RoundDuration = _detailedMeter.CreateHistogram<double>("snap.phase3.round.duration", "s");
        }

        public void SetPhase(SnapPhase phase)
        {
            var prev = Interlocked.Exchange(ref _currentPhase, (long)phase);
            if (prev != (long)phase)
            {
                _phaseEntered.Add(1, new System.Collections.Generic.KeyValuePair<string, object>("phase", phase.ToString()));
            }
        }

        public void RecordResume(SnapPhase fromPhase)
        {
            _resumeTotal.Add(1, new System.Collections.Generic.KeyValuePair<string, object>("from_phase", fromPhase.ToString()));
        }

        public void RecordPhase1BlocksPersisted(long count) => _phase1BlocksPersisted.Add(count);
        public void RecordPhase1BatchDuration(double seconds) => _phase1BatchDuration.Record(seconds);

        public void RecordPhase2AccountsSynced(long accounts, long bytes)
        {
            _phase2AccountsSynced.Add(accounts);
            _phase2AccountBytes.Add(bytes);
        }

        public void RecordPhase2StorageSynced(long slots, long bytes)
        {
            _phase2StorageSlotsSynced.Add(slots);
            _phase2StorageBytes.Add(bytes);
        }

        public void RecordPhase2BytecodesSynced(long count) => _phase2BytecodesSynced.Add(count);

        public void RecordPhase2ChunkDuration(string kind, double seconds)
        {
            _phase2ChunkDuration.Record(seconds, new System.Collections.Generic.KeyValuePair<string, object>("kind", kind));
        }

        public void RecordPhase2StatePersistDuration(double seconds)
            => _phase2StatePersistDuration.Record(seconds);

        public void RecordPhase3NodesHealed(long count) => _phase3NodesHealed.Add(count);
        public void RecordPhase3PivotRotation() => _phase3PivotRotations.Add(1);
        public void RecordPhase3RoundDuration(double seconds) => _phase3RoundDuration.Record(seconds);
        public void SetPhase3QueueDepth(long depth) => Interlocked.Exchange(ref _phase3QueueDepth, depth);

        public void SetPeerCounts(long total, long snapCapable)
        {
            Interlocked.Exchange(ref _peersTotal, total);
            Interlocked.Exchange(ref _peersSnapCapable, snapCapable);
        }

        public void RecordFetchFailed(string phase, string reason)
        {
            _fetchFailed.Add(1,
                new System.Collections.Generic.KeyValuePair<string, object>("phase", phase),
                new System.Collections.Generic.KeyValuePair<string, object>("reason", reason));
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
