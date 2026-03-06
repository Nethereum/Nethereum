#if NET8_0_OR_GREATER
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Numerics;
using System.Threading;

namespace Nethereum.BlockchainProcessing.Metrics
{
    public class LogProcessingMetrics : ILogProcessingObserver, IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly string _processorType;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _blocksProcessed;
        private readonly Counter<long> _logsProcessed;
        private readonly Counter<long> _errors;
        private readonly Counter<long> _reorgs;
        private readonly Counter<long> _getLogsRetries;
        private readonly Histogram<double> _batchDuration;

        private long _blocksProcessedCount;
        private long _logsProcessedCount;
        private long _lastProcessedBlock;
        private long _chainHead;

        public long BlocksProcessedCount => Interlocked.Read(ref _blocksProcessedCount);
        public long LogsProcessedCount => Interlocked.Read(ref _logsProcessedCount);
        public long LastProcessedBlock => Interlocked.Read(ref _lastProcessedBlock);
        public long ChainHead => Interlocked.Read(ref _chainHead);

        public LogProcessingMetrics(string chainId, string processorType, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _processorType = processorType;
            _meter = meterFactory?.Create($"{name}.LogProcessing") ?? new Meter($"{name}.LogProcessing");
            _detailedMeter = meterFactory?.Create($"{name}.LogProcessing.Detailed") ?? new Meter($"{name}.LogProcessing.Detailed");

            _blocksProcessed = _meter.CreateCounter<long>(
                "logprocessing.blocks.processed",
                unit: "{block}",
                description: "Total block ranges processed");

            _logsProcessed = _meter.CreateCounter<long>(
                "logprocessing.logs.processed",
                unit: "{log}",
                description: "Total individual logs processed");

            _errors = _meter.CreateCounter<long>(
                "logprocessing.errors",
                unit: "{error}",
                description: "Total processing errors");

            _reorgs = _meter.CreateCounter<long>(
                "logprocessing.reorgs",
                unit: "{reorg}",
                description: "Total reorg detections");

            _getLogsRetries = _meter.CreateCounter<long>(
                "logprocessing.getlogs.retries",
                unit: "{retry}",
                description: "Total getLogs RPC retry attempts");

            _batchDuration = _detailedMeter.CreateHistogram<double>(
                "logprocessing.batch.duration",
                unit: "s",
                description: "Batch processing duration"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0]
                }
#endif
                );

            _meter.CreateObservableGauge(
                "logprocessing.last_block",
                () => new Measurement<long>(Interlocked.Read(ref _lastProcessedBlock),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name),
                    new KeyValuePair<string, object?>("processor_type", _processorType)),
                unit: "{block}",
                description: "Last processed block number");

            _meter.CreateObservableGauge(
                "logprocessing.lag",
                () =>
                {
                    var head = Interlocked.Read(ref _chainHead);
                    var last = Interlocked.Read(ref _lastProcessedBlock);
                    var lag = head > last ? head - last : 0;
                    return new Measurement<long>(lag,
                        new KeyValuePair<string, object?>("chain_id", _chainId),
                        new KeyValuePair<string, object?>("chain_name", _name),
                        new KeyValuePair<string, object?>("processor_type", _processorType));
                },
                unit: "{block}",
                description: "Blocks behind chain head");
        }

        public void OnBatchProcessed(BigInteger fromBlock, BigInteger toBlock, int logCount, double durationSeconds)
        {
            var tags = new TagList
            {
                { "chain_id", _chainId },
                { "chain_name", _name },
                { "processor_type", _processorType }
            };
            var blockCount = (long)(toBlock - fromBlock + 1);
            _blocksProcessed.Add(blockCount, tags);
            _batchDuration.Record(durationSeconds, tags);
            Interlocked.Add(ref _blocksProcessedCount, blockCount);
            if (logCount > 0)
            {
                _logsProcessed.Add(logCount, tags);
                Interlocked.Add(ref _logsProcessedCount, logCount);
            }
        }

        public void OnError(string reason)
        {
            _errors.Add(1, new TagList
            {
                { "chain_id", _chainId },
                { "chain_name", _name },
                { "processor_type", _processorType },
                { "reason", reason }
            });
        }

        public void OnReorgDetected(BigInteger rewindToBlock, BigInteger lastCanonicalBlock)
        {
            _reorgs.Add(1, new TagList
            {
                { "chain_id", _chainId },
                { "chain_name", _name },
                { "processor_type", _processorType }
            });
        }

        public void OnBlockProgressUpdated(BigInteger lastBlock)
        {
            Interlocked.Exchange(ref _lastProcessedBlock, (long)lastBlock);
        }

        public void OnGetLogsRetry(int retryNumber)
        {
            _getLogsRetries.Add(1, new TagList
            {
                { "chain_id", _chainId },
                { "chain_name", _name },
                { "processor_type", _processorType },
                { "retry_number", retryNumber.ToString() }
            });
        }

        public void SetChainHead(BigInteger blockNumber)
        {
            Interlocked.Exchange(ref _chainHead, (long)blockNumber);
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
#endif
