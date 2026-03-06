using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Sequencer.Metrics
{
    public class HAMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _failoversTotal;
        private readonly Counter<long> _takeoverAttempts;
        private readonly Counter<long> _splitBrainDetected;
        private readonly Histogram<double> _heartbeatLatency;
        private readonly Histogram<double> _failoverDuration;
        private readonly Histogram<double> _recoveryDuration;

        private long _replicationLagBlocks;
        private long _lastAnchorBlock;
        private int _primaryHealthy;
        private long _failoversCount;
        private long _dataLossWindow;
        private long _splitBrainCount;
        private readonly ConcurrentDictionary<string, long> _takeoversByResult = new();

        public long FailoversCount => Interlocked.Read(ref _failoversCount);
        public long ReplicationLagBlocks => Interlocked.Read(ref _replicationLagBlocks);
        public long LastAnchorBlock => Interlocked.Read(ref _lastAnchorBlock);
        public bool PrimaryHealthy => Volatile.Read(ref _primaryHealthy) == 1;
        public long DataLossWindow => Interlocked.Read(ref _dataLossWindow);
        public long SplitBrainCount => Interlocked.Read(ref _splitBrainCount);

        public long GetTakeoverCount(string result)
        {
            return _takeoversByResult.TryGetValue(result, out var count) ? Interlocked.Read(ref count) : 0;
        }

        public HAMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.Sequencer") ?? new Meter($"{name}.Sequencer");
            _detailedMeter = meterFactory?.Create($"{name}.Sequencer.Detailed") ?? new Meter($"{name}.Sequencer.Detailed");

            _failoversTotal = _meter.CreateCounter<long>(
                "sequencer.ha.failovers",
                unit: "{failover}",
                description: "Total failovers performed");

            _takeoverAttempts = _meter.CreateCounter<long>(
                "sequencer.ha.takeover_attempts",
                unit: "{attempt}",
                description: "Total takeover attempts");

            _splitBrainDetected = _meter.CreateCounter<long>(
                "sequencer.ha.split_brain_detected",
                unit: "{event}",
                description: "Split-brain events detected — any increment requires investigation");

            _heartbeatLatency = _detailedMeter.CreateHistogram<double>(
                "sequencer.ha.heartbeat.duration",
                unit: "s",
                description: "Heartbeat response latency"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0, 2.5, 5.0]
                }
#endif
                );

            _failoverDuration = _detailedMeter.CreateHistogram<double>(
                "sequencer.ha.failover.duration",
                unit: "s",
                description: "Time from failover trigger to new sequencer active (RTO metric)"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0]
                }
#endif
                );

            _recoveryDuration = _detailedMeter.CreateHistogram<double>(
                "sequencer.ha.recovery.duration",
                unit: "s",
                description: "Time from failover completion to full operational state"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [1.0, 2.5, 5.0, 10.0, 30.0, 60.0, 120.0, 180.0, 300.0]
                }
#endif
                );

            _meter.CreateObservableGauge("sequencer.ha.replication_lag",
                () => new Measurement<long>(Interlocked.Read(ref _replicationLagBlocks),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Replication lag in blocks");

            _meter.CreateObservableGauge("sequencer.ha.last_anchor_block",
                () => new Measurement<long>(Interlocked.Read(ref _lastAnchorBlock),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Last anchored block number");

            _meter.CreateObservableGauge("sequencer.ha.primary_healthy",
                () => new Measurement<int>(Volatile.Read(ref _primaryHealthy),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                description: "Whether primary sequencer is healthy (1=healthy, 0=unhealthy)");

            _meter.CreateObservableGauge("sequencer.ha.data_loss_window",
                () => new Measurement<long>(Interlocked.Read(ref _dataLossWindow),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Blocks between local head and last anchor (RPO metric)");
        }

        public void RecordFailover()
        {
            _failoversTotal.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
            Interlocked.Increment(ref _failoversCount);
        }

        public void RecordFailoverDuration(double durationSeconds)
        {
            _failoverDuration.Record(durationSeconds, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
        }

        public void RecordRecoveryDuration(double durationSeconds)
        {
            _recoveryDuration.Record(durationSeconds, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
        }

        public void RecordHeartbeat(double latencySeconds, bool healthy)
        {
            _heartbeatLatency.Record(latencySeconds, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
            Volatile.Write(ref _primaryHealthy, healthy ? 1 : 0);
        }

        public void RecordTakeoverAttempt(bool success)
        {
            var result = success ? "success" : "failed";
            _takeoverAttempts.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "result", result } });
            _takeoversByResult.AddOrUpdate(result, 1, (_, old) => old + 1);
        }

        public void RecordSplitBrain()
        {
            _splitBrainDetected.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
            Interlocked.Increment(ref _splitBrainCount);
        }

        public void UpdateReplicationStatus(long lagBlocks, long lastAnchor)
        {
            Interlocked.Exchange(ref _replicationLagBlocks, lagBlocks);
            Interlocked.Exchange(ref _lastAnchorBlock, lastAnchor);
        }

        public void UpdateDataLossWindow(long localHead, long lastAnchoredBlock)
        {
            Interlocked.Exchange(ref _dataLossWindow, localHead - lastAnchoredBlock);
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
