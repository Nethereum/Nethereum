using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Anchoring.Postgres.Metrics
{
    public class AnchorIndexingMetrics : IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _blocksProcessed;
        private readonly Counter<long> _eventsIndexed;
        private readonly Counter<long> _errors;
        private readonly Counter<long> _resets;
        private readonly Histogram<double> _batchDuration;

        private long _lastProcessedBlock;
        private long _chainHead;
        private long _denormalizedCount;

        public AnchorIndexingMetrics(string name = "Nethereum", IMeterFactory meterFactory = null)
        {
            _meter = meterFactory?.Create($"{name}.AnchorIndexing") ?? new Meter($"{name}.AnchorIndexing");

            _blocksProcessed = _meter.CreateCounter<long>(
                "anchoring.index.blocks_processed",
                unit: "{block}",
                description: "Total blocks processed by anchor indexer");

            _eventsIndexed = _meter.CreateCounter<long>(
                "anchoring.index.events_indexed",
                unit: "{event}",
                description: "Total anchor events indexed");

            _errors = _meter.CreateCounter<long>(
                "anchoring.index.errors",
                unit: "{error}",
                description: "Total anchor indexer errors");

            _resets = _meter.CreateCounter<long>(
                "anchoring.index.resets",
                unit: "{reset}",
                description: "Number of chain-restart resets");

            _batchDuration = _meter.CreateHistogram<double>(
                "anchoring.index.batch.duration",
                unit: "s",
                description: "Duration of batch indexing"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30]
                }
#endif
                );

            _meter.CreateObservableGauge("anchoring.index.lag",
                () => new Measurement<long>(
                    Interlocked.Read(ref _chainHead) - Interlocked.Read(ref _lastProcessedBlock)),
                unit: "{block}", description: "Blocks behind chain head");

            _meter.CreateObservableGauge("anchoring.index.last_block",
                () => new Measurement<long>(Interlocked.Read(ref _lastProcessedBlock)),
                unit: "{block}", description: "Last processed block number");

            _meter.CreateObservableGauge("anchoring.denormalizer.processed",
                () => new Measurement<long>(Interlocked.Read(ref _denormalizedCount)),
                unit: "{anchor}", description: "Total anchors denormalized");
        }

        public void RecordBatch(long fromBlock, long toBlock, int anchorCount, int chainCount, double durationSeconds)
        {
            _blocksProcessed.Add(toBlock - fromBlock + 1);
            _eventsIndexed.Add(anchorCount + chainCount, new KeyValuePair<string, object>("event_type", "mixed"));
            _batchDuration.Record(durationSeconds);
            Interlocked.Exchange(ref _lastProcessedBlock, toBlock);
        }

        public void SetChainHead(long blockNumber)
        {
            Interlocked.Exchange(ref _chainHead, blockNumber);
        }

        public void RecordError(string reason)
        {
            _errors.Add(1, new KeyValuePair<string, object>("reason", reason));
        }

        public void RecordReset()
        {
            _resets.Add(1);
        }

        public void RecordDenormalization(int count)
        {
            Interlocked.Add(ref _denormalizedCount, count);
        }

        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
