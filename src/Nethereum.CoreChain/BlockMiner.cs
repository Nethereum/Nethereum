using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class BlockMiner
    {
        protected readonly ITxPool _txPool;
        protected readonly IBlockProducer _blockProducer;
        protected readonly BlockMinerConfig _config;

        public BlockMiner(ITxPool txPool, IBlockProducer blockProducer, BlockMinerConfig config)
        {
            _txPool = txPool ?? throw new ArgumentNullException(nameof(txPool));
            _blockProducer = blockProducer ?? throw new ArgumentNullException(nameof(blockProducer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool AutoMine { get; set; }

        public virtual async Task<BlockProductionResult> MineBlockAsync()
        {
            var transactions = await _txPool.GetPendingAsync(_config.MaxTransactionsPerBlock);

            if (transactions.Count == 0 && !_config.AllowEmptyBlocks)
            {
                return null;
            }

            var options = CreateBlockProductionOptions();
            var result = await _blockProducer.ProduceBlockAsync(transactions, options);

            foreach (var txResult in result.TransactionResults)
            {
                await _txPool.RemoveAsync(txResult.TxHash);
            }

            return result;
        }

        public virtual async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            var txHash = await _txPool.AddAsync(tx);

            var result = new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = txHash,
                Success = true
            };

            if (AutoMine)
            {
                await MineBlockAsync();
            }

            return result;
        }

        protected virtual BlockProductionOptions CreateBlockProductionOptions()
        {
            return new BlockProductionOptions
            {
                Timestamp = GetTimestamp(),
                Coinbase = _config.Coinbase,
                BaseFee = _config.BaseFee,
                BlockGasLimit = _config.BlockGasLimit,
                Difficulty = _config.Difficulty
            };
        }

        protected virtual long GetTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
