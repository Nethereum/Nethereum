using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.CoreChain.Metrics
{
    public class TxPoolMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _txReceived;
        private readonly Counter<long> _txRejected;
        private readonly Counter<long> _txRemoved;
        private readonly Histogram<double> _txWaitTime;

        private int _pendingCount;
        private int _queuedCount;
        private long _receivedCount;
        private readonly ConcurrentDictionary<string, long> _rejectedByReason = new();

        public int PendingCountValue => Volatile.Read(ref _pendingCount);
        public int QueuedCountValue => Volatile.Read(ref _queuedCount);
        public long ReceivedCount => Interlocked.Read(ref _receivedCount);

        public long GetRejectedCount(string reason)
        {
            return _rejectedByReason.TryGetValue(reason, out var count) ? Interlocked.Read(ref count) : 0;
        }

        public TxPoolMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.CoreChain") ?? new Meter($"{name}.CoreChain");
            _detailedMeter = meterFactory?.Create($"{name}.CoreChain.Detailed") ?? new Meter($"{name}.CoreChain.Detailed");

            _txReceived = _meter.CreateCounter<long>(
                "corechain.txpool.received",
                unit: "{transaction}",
                description: "Total transactions received");

            _txRejected = _meter.CreateCounter<long>(
                "corechain.txpool.rejected",
                unit: "{transaction}",
                description: "Total transactions rejected");

            _txRemoved = _meter.CreateCounter<long>(
                "corechain.txpool.removed",
                unit: "{transaction}",
                description: "Total transactions removed from pool");

            _txWaitTime = _detailedMeter.CreateHistogram<double>(
                "corechain.txpool.wait.duration",
                unit: "s",
                description: "Time transaction spent in pool before inclusion"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0]
                }
#endif
                );

            _meter.CreateObservableGauge(
                "corechain.txpool.pending",
                () => new Measurement<int>(Volatile.Read(ref _pendingCount),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{transaction}",
                description: "Number of pending transactions");

            _meter.CreateObservableGauge(
                "corechain.txpool.queued",
                () => new Measurement<int>(Volatile.Read(ref _queuedCount),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{transaction}",
                description: "Number of queued transactions");
        }

        public void SetPendingCount(int count) => Volatile.Write(ref _pendingCount, count);
        public void SetQueuedCount(int count) => Volatile.Write(ref _queuedCount, count);

        public void RecordTxReceived()
        {
            _txReceived.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
            Interlocked.Increment(ref _receivedCount);
        }

        public void RecordTxRejected(string reason)
        {
            _txRejected.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "reason", reason } });
            _rejectedByReason.AddOrUpdate(reason, 1, (_, old) => old + 1);
        }

        public void RecordTxIncluded(double waitTimeSeconds)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _txWaitTime.Record(waitTimeSeconds, tags);
            _txRemoved.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "reason", "included" } });
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
