using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeTransactionStore : ITransactionStore
    {
        private readonly ITransactionStore _history;
        private readonly RocksDbHotBlockWindowStore _hot;
        private readonly bool _writeThroughHistory;

        public CompositeTransactionStore(ITransactionStore history, RocksDbHotBlockWindowStore hot, bool writeThroughHistory = true)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _writeThroughHistory = writeThroughHistory;
        }

        public async Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
            => _hot.TryGetTransactionByHash(txHash) ?? await _history.GetByHashAsync(txHash).ConfigureAwait(false);

        public async Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var n = (ulong)blockNumber;
            if (_hot.ContainsBlock(n)) return _hot.GetTransactionsForBlock(n);
            return await _history.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false);
        }

        public async Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            var n = _hot.TryGetBlockNumberByHash(blockHash);
            if (n.HasValue) return _hot.GetTransactionsForBlock(n.Value);
            return await _history.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public async Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            var n = _hot.TryGetBlockNumberByHash(blockHash);
            if (n.HasValue) return _hot.GetTransactionHashesForBlock(n.Value);
            return await _history.GetHashesByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public async Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber)
        {
            if (_writeThroughHistory)
                await _history.SaveAsync(tx, blockHash, txIndex, blockNumber).ConfigureAwait(false);
            if (ShouldWriteHot((ulong)blockNumber))
                _hot.WriteTransaction((ulong)blockNumber, txIndex, tx);
        }

        public async Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs)
        {
            if (_writeThroughHistory)
                await _history.SaveManyAsync(blockHash, blockNumber, txs).ConfigureAwait(false);
            if (ShouldWriteHot((ulong)blockNumber))
                _hot.WriteTransactions((ulong)blockNumber, txs);
        }

        private bool ShouldWriteHot(ulong number)
        {
            if (!_writeThroughHistory) return true;
            var tip = _hot.TryGetLatestNumber();
            if (tip == null) return true;
            if (number > tip.Value) return true;
            return number + (ulong)_hot.WindowSize > tip.Value;
        }

        public async Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            var loc = _hot.TryGetTransactionLocation(txHash);
            if (loc.HasValue)
            {
                return new TransactionLocation
                {
                    BlockHash = _hot.TryGetHash(loc.Value.Block),
                    BlockNumber = loc.Value.Block,
                    TransactionIndex = (int)loc.Value.Index,
                };
            }
            return await _history.GetLocationAsync(txHash).ConfigureAwait(false);
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_writeThroughHistory)
                await _history.DeleteByBlockNumberAsync(blockNumber).ConfigureAwait(false);
            _hot.DeleteBlockTransactions((ulong)blockNumber);
        }
    }
}
