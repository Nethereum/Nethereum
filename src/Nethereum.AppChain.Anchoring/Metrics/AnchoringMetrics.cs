using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Anchoring.Metrics
{
    public class AnchoringMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _submissionsTotal;
        private readonly Counter<long> _anchorErrors;
        private readonly Histogram<double> _anchorLatency;
        private readonly Histogram<double> _l1ConfirmationTime;

        private long _submissionsCount;
        private long _lastAnchoredBlock;
        private long _l1GasUsed;
        private long _batchAgeBlocks;

        public long SubmissionsCount => Interlocked.Read(ref _submissionsCount);
        public long LastAnchoredBlock => Interlocked.Read(ref _lastAnchoredBlock);
        public long L1GasUsed => Interlocked.Read(ref _l1GasUsed);
        public long BatchAgeBlocks => Interlocked.Read(ref _batchAgeBlocks);

        public AnchoringMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.Anchoring") ?? new Meter($"{name}.Anchoring");
            _detailedMeter = meterFactory?.Create($"{name}.Anchoring.Detailed") ?? new Meter($"{name}.Anchoring.Detailed");

            _submissionsTotal = _meter.CreateCounter<long>(
                "anchoring.submissions",
                unit: "{submission}",
                description: "Total anchor submissions");

            _anchorErrors = _meter.CreateCounter<long>(
                "anchoring.errors",
                unit: "{error}",
                description: "Total anchor errors");

            _anchorLatency = _detailedMeter.CreateHistogram<double>(
                "anchoring.confirmation.duration",
                unit: "s",
                description: "Time to confirm anchor on L1"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [5, 15, 30, 60, 120, 180, 300, 600]
                }
#endif
                );

            _l1ConfirmationTime = _detailedMeter.CreateHistogram<double>(
                "anchoring.l1_confirmation.duration",
                unit: "s",
                description: "L1 transaction confirmation time"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [5, 15, 30, 60, 120, 180, 300, 600]
                }
#endif
                );

            _meter.CreateObservableGauge("anchoring.last_block",
                () => new Measurement<long>(Interlocked.Read(ref _lastAnchoredBlock),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Last anchored block number");

            _meter.CreateObservableGauge("anchoring.l1_gas_used",
                () => new Measurement<long>(Interlocked.Read(ref _l1GasUsed),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                description: "L1 gas used in last anchor");

            _meter.CreateObservableGauge("anchoring.batch_age",
                () => new Measurement<long>(Interlocked.Read(ref _batchAgeBlocks),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}", description: "Age of oldest unanchored batch in blocks");
        }

        public void RecordAnchor(long blockNumber, long gasUsed, double latencySeconds)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _submissionsTotal.Add(1, tags);
            _anchorLatency.Record(latencySeconds, tags);
            Interlocked.Increment(ref _submissionsCount);
            Interlocked.Exchange(ref _lastAnchoredBlock, blockNumber);
            Interlocked.Exchange(ref _l1GasUsed, gasUsed);
        }

        public void RecordL1Confirmation(double durationSeconds)
        {
            _l1ConfirmationTime.Record(durationSeconds, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
        }

        public void RecordError(string reason)
        {
            _anchorErrors.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "reason", reason } });
        }

        public void UpdateBatchAge(long ageBlocks)
        {
            Interlocked.Exchange(ref _batchAgeBlocks, ageBlocks);
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
