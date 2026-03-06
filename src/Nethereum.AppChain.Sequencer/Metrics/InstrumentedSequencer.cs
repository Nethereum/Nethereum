using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer.Metrics
{
    public class InstrumentedSequencer : ISequencer
    {
        private readonly ISequencer _inner;
        private readonly BlockProductionMetrics _blockProduction;
        private readonly TxPoolMetrics _txPool;
        private readonly SequencerMetrics _sequencer;
        private volatile BlockProductionResult? _lastBlockResult;

        public InstrumentedSequencer(
            ISequencer inner,
            BlockProductionMetrics blockProduction,
            TxPoolMetrics txPool,
            SequencerMetrics sequencer)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(blockProduction);
            ArgumentNullException.ThrowIfNull(txPool);
            ArgumentNullException.ThrowIfNull(sequencer);

            _inner = inner;
            _blockProduction = blockProduction;
            _txPool = txPool;
            _sequencer = sequencer;
            _inner.BlockProduced += (s, e) =>
            {
                _lastBlockResult = e;
                BlockProduced?.Invoke(this, e);
            };
        }

        public SequencerConfig Config => _inner.Config;
        public IAppChain AppChain => _inner.AppChain;
        public ITxPool TxPool => _inner.TxPool;
        public IPolicyEnforcer PolicyEnforcer => _inner.PolicyEnforcer;

        public event EventHandler<BlockProductionResult>? BlockProduced;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _inner.StartAsync(cancellationToken);
            _sequencer.SetActive(true, 1);
        }

        public async Task StopAsync()
        {
            await _inner.StopAsync();
            _sequencer.SetActive(false, 0);
        }

        public async Task<byte[]> SubmitTransactionAsync(ISignedTransaction transaction)
        {
            _txPool.RecordTxReceived();

            try
            {
                var result = await _inner.SubmitTransactionAsync(transaction);
                _txPool.SetPendingCount(TxPool.PendingCount);
                return result;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("rejected"))
            {
                var reason = ExtractRejectionReason(ex.Message);
                _txPool.RecordTxRejected(reason);
                _sequencer.RecordPolicyRejection(reason);
                throw;
            }
        }

        public async Task<byte[]> ProduceBlockAsync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _lastBlockResult = null;
                var result = await _inner.ProduceBlockAsync();
                sw.Stop();

                var blockResult = _lastBlockResult;
                var txCount = (blockResult?.SuccessfulTransactions ?? 0) + (blockResult?.FailedTransactions ?? 0);
                var gasUsed = blockResult?.Header?.GasUsed ?? 0;
                var blockNumber = blockResult?.Header?.BlockNumber ?? await _inner.GetBlockNumberAsync();

                _blockProduction.RecordBlockProduced(
                    txCount: txCount,
                    gasUsed: gasUsed,
                    blockNumber: (long)blockNumber,
                    durationSeconds: sw.Elapsed.TotalSeconds);

                _txPool.SetPendingCount(TxPool.PendingCount);

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _blockProduction.RecordError(ex.GetType().Name);
                throw;
            }
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await _inner.GetBlockNumberAsync();
        }

        public async Task<BlockHeader?> GetLatestBlockAsync()
        {
            return await _inner.GetLatestBlockAsync();
        }

        private static string ExtractRejectionReason(string message)
        {
            if (message.Contains("allowlist"))
                return "not_in_allowlist";
            if (message.Contains("calldata"))
                return "calldata_too_large";
            if (message.Contains("gas"))
                return "gas_limit";
            if (message.Contains("nonce"))
                return "invalid_nonce";
            return "policy_violation";
        }
    }
}
