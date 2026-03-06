using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Sync.Metrics
{
    public class SyncMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _batchImportsTotal;
        private readonly Counter<long> _liveBlocksReceived;
        private readonly Histogram<double> _batchImportDuration;
        private readonly Histogram<double> _liveBlockLatency;

        private string? _currentMode;
        private long _localHead;
        private long _remoteHead;
        private long _lagBlocks;
        private long _finalizedHead;
        private long _softHead;
        private long _batchSizeBytes;
        private long _batchImportsCount;

        public string? CurrentMode => _currentMode;
        public long LocalHeadValue => Interlocked.Read(ref _localHead);
        public long RemoteHeadValue => Interlocked.Read(ref _remoteHead);
        public long LagBlocksValue => Interlocked.Read(ref _lagBlocks);
        public long FinalizedHeadValue => Interlocked.Read(ref _finalizedHead);
        public long SoftHeadValue => Interlocked.Read(ref _softHead);
        public long BatchImportsCount => Interlocked.Read(ref _batchImportsCount);

        public SyncMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.Sync") ?? new Meter($"{name}.Sync");
            _detailedMeter = meterFactory?.Create($"{name}.Sync.Detailed") ?? new Meter($"{name}.Sync.Detailed");

            _batchImportsTotal = _meter.CreateCounter<long>(
                "sync.batch.imports",
                unit: "{import}",
                description: "Total batch imports");

            _liveBlocksReceived = _meter.CreateCounter<long>(
                "sync.live_blocks.received",
                unit: "{block}",
                description: "Total live blocks received");

            _batchImportDuration = _detailedMeter.CreateHistogram<double>(
                "sync.batch.import.duration",
                unit: "s",
                description: "Time to import a batch"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 5.0, 10.0, 30.0]
                }
#endif
                );

            _liveBlockLatency = _detailedMeter.CreateHistogram<double>(
                "sync.live_block.latency",
                unit: "s",
                description: "Latency between block production and reception"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0]
                }
#endif
                );

            _meter.CreateObservableGauge<int>("sync.mode", ObserveSyncMode,
                description: "Current sync mode (1=active for the labeled mode)");

            _meter.CreateObservableGauge("sync.local_head",
                () => new Measurement<long>(Interlocked.Read(ref _localHead),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Local chain head block number");

            _meter.CreateObservableGauge("sync.remote_head",
                () => new Measurement<long>(Interlocked.Read(ref _remoteHead),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Remote chain head block number");

            _meter.CreateObservableGauge("sync.lag",
                () => new Measurement<long>(Interlocked.Read(ref _lagBlocks),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Number of blocks behind remote");

            _meter.CreateObservableGauge("sync.finalized_head",
                () => new Measurement<long>(Interlocked.Read(ref _finalizedHead),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Finalized (anchored) block number");

            _meter.CreateObservableGauge("sync.soft_head",
                () => new Measurement<long>(Interlocked.Read(ref _softHead),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Soft (unanchored) block number");

            _meter.CreateObservableGauge("sync.batch.size",
                () => new Measurement<long>(Interlocked.Read(ref _batchSizeBytes),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "By", description: "Size of last imported batch");
        }

        private IEnumerable<Measurement<int>> ObserveSyncMode()
        {
            var mode = _currentMode;
            if (mode == null) return [];
            return [new Measurement<int>(1,
                new KeyValuePair<string, object?>("chain_id", _chainId),
                new KeyValuePair<string, object?>("chain_name", _name),
                new KeyValuePair<string, object?>("mode", mode))];
        }

        public void SetSyncMode(string mode) => _currentMode = mode;

        public void SetLocalHead(long blockNumber) => Interlocked.Exchange(ref _localHead, blockNumber);

        public void UpdateSyncStatus(long localHead, long remoteHead, long finalizedHead)
        {
            Interlocked.Exchange(ref _localHead, localHead);
            Interlocked.Exchange(ref _remoteHead, remoteHead);
            Interlocked.Exchange(ref _lagBlocks, remoteHead - localHead);
            Interlocked.Exchange(ref _finalizedHead, finalizedHead);
            Interlocked.Exchange(ref _softHead, localHead);
        }

        public void RecordBatchImport(long sizeBytes, double durationSeconds)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _batchImportsTotal.Add(1, tags);
            _batchImportDuration.Record(durationSeconds, tags);
            Interlocked.Exchange(ref _batchSizeBytes, sizeBytes);
            Interlocked.Increment(ref _batchImportsCount);
        }

        public void RecordLiveBlock(double latencySeconds)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _liveBlocksReceived.Add(1, tags);
            _liveBlockLatency.Record(latencySeconds, tags);
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
