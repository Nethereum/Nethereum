using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;

namespace Nethereum.CoreChain.Metrics
{
    public class StorageMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Histogram<double> _readLatency;
        private readonly Histogram<double> _writeLatency;

        private long _blocksTotal;
        private long _transactionsTotal;
        private readonly ConcurrentDictionary<string, long> _storeSizes = new();

        public long BlocksTotal => Interlocked.Read(ref _blocksTotal);
        public long TransactionsTotal => Interlocked.Read(ref _transactionsTotal);

        public StorageMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.CoreChain") ?? new Meter($"{name}.CoreChain");
            _detailedMeter = meterFactory?.Create($"{name}.CoreChain.Detailed") ?? new Meter($"{name}.CoreChain.Detailed");

            _readLatency = _detailedMeter.CreateHistogram<double>(
                "corechain.storage.read.duration",
                unit: "s",
                description: "Storage read latency"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.0001, 0.0005, 0.001, 0.005, 0.01, 0.025, 0.05, 0.1]
                }
#endif
                );

            _writeLatency = _detailedMeter.CreateHistogram<double>(
                "corechain.storage.write.duration",
                unit: "s",
                description: "Storage write latency"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.0001, 0.0005, 0.001, 0.005, 0.01, 0.025, 0.05, 0.1]
                }
#endif
                );

            _meter.CreateObservableGauge("corechain.storage.blocks",
                () => new Measurement<long>(Interlocked.Read(ref _blocksTotal),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Total blocks in storage");

            _meter.CreateObservableGauge("corechain.storage.transactions",
                () => new Measurement<long>(Interlocked.Read(ref _transactionsTotal),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{transaction}", description: "Total transactions in storage");

            _meter.CreateObservableGauge<long>("corechain.storage.size", ObserveStoreSizes,
                unit: "By", description: "Storage size in bytes");
        }

        private IEnumerable<Measurement<long>> ObserveStoreSizes()
        {
            return _storeSizes.Select(kvp => new Measurement<long>(kvp.Value,
                new KeyValuePair<string, object?>("chain_id", _chainId),
                new KeyValuePair<string, object?>("chain_name", _name),
                new KeyValuePair<string, object?>("store", kvp.Key)));
        }

        public void UpdateStorageStats(long blockCount, long txCount)
        {
            Interlocked.Exchange(ref _blocksTotal, blockCount);
            Interlocked.Exchange(ref _transactionsTotal, txCount);
        }

        public void SetStorageSize(string store, long sizeBytes) => _storeSizes[store] = sizeBytes;

        public DurationTimer MeasureRead(string store)
        {
            return new DurationTimer(_readLatency, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "store", store } });
        }

        public DurationTimer MeasureWrite(string store)
        {
            return new DurationTimer(_writeLatency, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "store", store } });
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
