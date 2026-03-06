using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.AppChain.Sync;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer
{
    public class SequencerBatchProducer : IBatchProducer
    {
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly IBatchStore _batchStore;
        private readonly BatchFileWriter _batchWriter;
        private readonly BatchProductionConfig _config;
        private readonly BigInteger _chainId;

        private BigInteger _lastBatchedBlock = -1;
        private DateTimeOffset _lastBatchTime = DateTimeOffset.MinValue;

        public BigInteger LastBatchedBlock => _lastBatchedBlock;
        public BigInteger NextBatchBlock => _lastBatchedBlock < 0
            ? _config.BatchCadence - 1
            : _lastBatchedBlock + _config.BatchCadence;
        public DateTimeOffset LastBatchTime => _lastBatchTime;

        public event EventHandler<BatchProductionResult>? BatchProduced;

        public SequencerBatchProducer(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            IBatchStore batchStore,
            BatchProductionConfig config,
            BigInteger chainId)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _batchStore = batchStore ?? throw new ArgumentNullException(nameof(batchStore));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _chainId = chainId;
            _batchWriter = new BatchFileWriter();
        }

        public async Task InitializeAsync()
        {
            _lastBatchedBlock = await _batchStore.GetLatestImportedBlockAsync();
            _lastBatchTime = DateTimeOffset.UtcNow;
        }

        public bool IsBatchDue(BigInteger blockNumber)
        {
            if (!_config.Enabled)
                return false;

            if (blockNumber <= _lastBatchedBlock)
                return false;

            if ((blockNumber + 1) % _config.BatchCadence == 0)
                return true;

            if (_config.TimeThresholdSeconds > 0 && IsTimeThresholdExceeded(blockNumber))
                return true;

            return false;
        }

        public bool IsTimeThresholdExceeded(BigInteger currentBlockNumber)
        {
            if (_config.TimeThresholdSeconds <= 0)
                return false;

            if (currentBlockNumber <= _lastBatchedBlock)
                return false;

            if (_lastBatchTime == DateTimeOffset.MinValue)
                return false;

            var elapsed = DateTimeOffset.UtcNow - _lastBatchTime;
            return elapsed.TotalSeconds >= _config.TimeThresholdSeconds;
        }

        public async Task<BatchProductionResult> ProduceBatchIfDueAsync(BigInteger currentBlockNumber, CancellationToken cancellationToken = default)
        {
            if (!IsBatchDue(currentBlockNumber))
            {
                return new BatchProductionResult
                {
                    Success = false,
                    ErrorMessage = "Batch not due"
                };
            }

            var fromBlock = _lastBatchedBlock + 1;
            if (fromBlock < 0) fromBlock = 0;

            return await ProduceBatchAsync(fromBlock, currentBlockNumber, cancellationToken);
        }

        public async Task<BatchProductionResult> ProduceBatchAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default)
        {
            var result = new BatchProductionResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var blocks = await CollectBlocksAsync(fromBlock, toBlock, cancellationToken);

                if (blocks.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No blocks found in range";
                    return result;
                }

                EnsureOutputDirectory();

                var fileName = BatchFileFormat.GetBatchFileName(
                    (long)_chainId,
                    (long)fromBlock,
                    (long)toBlock,
                    _config.CompressBatches);
                var filePath = Path.Combine(_config.BatchOutputDirectory, fileName);

                var batchInfo = await _batchWriter.WriteBatchToFileAsync(
                    filePath,
                    _chainId,
                    blocks,
                    _config.CompressBatches,
                    cancellationToken);

                batchInfo.Status = BatchStatus.Written;
                await _batchStore.SaveBatchAsync(batchInfo);

                _lastBatchedBlock = toBlock;
                _lastBatchTime = DateTimeOffset.UtcNow;

                result.Success = true;
                result.BatchInfo = batchInfo;
                result.FilePath = filePath;

                BatchProduced?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        private async Task<List<BatchBlockData>> CollectBlocksAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken)
        {
            var blocks = new List<BatchBlockData>();

            for (var i = fromBlock; i <= toBlock; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var header = await _blockStore.GetByNumberAsync((long)i);
                if (header == null)
                    break;

                var blockHash = await _blockStore.GetHashByNumberAsync((long)i);
                var transactions = await _transactionStore.GetByBlockHashAsync(blockHash);
                var receipts = await _receiptStore.GetByBlockNumberAsync((long)i);

                blocks.Add(new BatchBlockData
                {
                    Header = header,
                    Transactions = transactions ?? new List<ISignedTransaction>(),
                    Receipts = receipts ?? new List<Receipt>()
                });
            }

            return blocks;
        }

        private void EnsureOutputDirectory()
        {
            if (!Directory.Exists(_config.BatchOutputDirectory))
            {
                Directory.CreateDirectory(_config.BatchOutputDirectory);
            }
        }
    }
}
