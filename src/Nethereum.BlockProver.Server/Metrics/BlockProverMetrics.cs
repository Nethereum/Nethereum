using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.BlockProver.Server.Metrics
{
    public class BlockProverMetrics : IDisposable
    {
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Counter<long> _proofsCompleted;
        private readonly Counter<long> _proofsFailed;
        private readonly Counter<long> _retries;
        private readonly Histogram<double> _proofDuration;

        private long _lastProvenBlock;
        private long _failedCount;
        private int _queueDepth;

        public long LastProvenBlock => Interlocked.Read(ref _lastProvenBlock);
        public long FailedCount => Interlocked.Read(ref _failedCount);
        public int QueueDepth => Volatile.Read(ref _queueDepth);

        public BlockProverMetrics(string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _name = name;
            _meter = meterFactory?.Create($"{name}.BlockProver") ?? new Meter($"{name}.BlockProver");

            _proofsCompleted = _meter.CreateCounter<long>(
                "blockprover.proofs.completed", unit: "{proof}", description: "Total block proofs completed");
            _proofsFailed = _meter.CreateCounter<long>(
                "blockprover.proofs.failed", unit: "{proof}", description: "Total block proofs that failed");
            _retries = _meter.CreateCounter<long>(
                "blockprover.proofs.retries", unit: "{attempt}", description: "Total proof retry attempts");
            _proofDuration = _meter.CreateHistogram<double>(
                "blockprover.proof.duration", unit: "s", description: "Time to prove a block");

            _meter.CreateObservableGauge(
                "blockprover.block.last_proven",
                () => new Measurement<long>(Interlocked.Read(ref _lastProvenBlock),
                    new KeyValuePair<string, object?>("name", _name)),
                unit: "{block}", description: "Last proven block number");

            _meter.CreateObservableGauge(
                "blockprover.queue.depth",
                () => new Measurement<int>(Volatile.Read(ref _queueDepth),
                    new KeyValuePair<string, object?>("name", _name)),
                unit: "{block}", description: "Unproven-block queue depth");
        }

        public void UpdateQueueDepth(int depth) => Volatile.Write(ref _queueDepth, depth);

        public void RecordProofCompleted(long blockNumber, double durationSeconds, string proverMode, string? elfHashShort)
        {
            var tags = new TagList { { "name", _name }, { "mode", proverMode } };
            _proofsCompleted.Add(1, tags);
            _proofDuration.Record(durationSeconds, tags);
            Interlocked.Exchange(ref _lastProvenBlock, blockNumber);
        }

        public void RecordProofFailed(long blockNumber, string reason)
        {
            _proofsFailed.Add(1, new TagList { { "name", _name }, { "reason", reason } });
            Interlocked.Increment(ref _failedCount);
        }

        public void RecordRetry(long blockNumber, int attempt)
        {
            _retries.Add(1, new TagList { { "name", _name } });
        }

        public void Dispose() => _meter.Dispose();
    }
}
