using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.CoreChain.Metrics
{
    public class BlockProductionMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _blocksProduced;
        private readonly Counter<long> _productionErrors;
        private readonly Histogram<double> _productionDuration;

        private long _blocksProducedCount;
        private long _currentBlockNumber;
        private int _transactionsPerBlock;
        private long _blockGasUsed;

        public long BlocksProducedCount => Interlocked.Read(ref _blocksProducedCount);
        public long CurrentBlockNumber => Interlocked.Read(ref _currentBlockNumber);
        public int TransactionsPerBlock => Volatile.Read(ref _transactionsPerBlock);
        public long BlockGasUsed => Interlocked.Read(ref _blockGasUsed);

        public BlockProductionMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.CoreChain") ?? new Meter($"{name}.CoreChain");
            _detailedMeter = meterFactory?.Create($"{name}.CoreChain.Detailed") ?? new Meter($"{name}.CoreChain.Detailed");

            _blocksProduced = _meter.CreateCounter<long>(
                "corechain.block.produced",
                unit: "{block}",
                description: "Total number of blocks produced");

            _productionErrors = _meter.CreateCounter<long>(
                "corechain.block.production.errors",
                unit: "{error}",
                description: "Total block production errors");

            _productionDuration = _detailedMeter.CreateHistogram<double>(
                "corechain.block.production.duration",
                unit: "s",
                description: "Time to produce a block"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0]
                }
#endif
                );

            _meter.CreateObservableGauge(
                "corechain.block.number",
                () => new Measurement<long>(Interlocked.Read(ref _currentBlockNumber),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{block}",
                description: "Current block number");

            _meter.CreateObservableGauge(
                "corechain.block.transactions",
                () => new Measurement<int>(Volatile.Read(ref _transactionsPerBlock),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{transaction}",
                description: "Transactions in latest block");

            _meter.CreateObservableGauge(
                "corechain.block.gas_used",
                () => new Measurement<long>(Interlocked.Read(ref _blockGasUsed),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                description: "Gas used in latest block");
        }

        public void RecordBlockProduced(int txCount, long gasUsed, long blockNumber, double durationSeconds)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _blocksProduced.Add(1, tags);
            _productionDuration.Record(durationSeconds, tags);
            Interlocked.Increment(ref _blocksProducedCount);
            Volatile.Write(ref _transactionsPerBlock, txCount);
            Interlocked.Exchange(ref _blockGasUsed, gasUsed);
            Interlocked.Exchange(ref _currentBlockNumber, blockNumber);
        }

        public void SetCurrentBlockNumber(long blockNumber)
        {
            Interlocked.Exchange(ref _currentBlockNumber, blockNumber);
        }

        public void RecordError(string reason)
        {
            _productionErrors.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "reason", reason } });
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
