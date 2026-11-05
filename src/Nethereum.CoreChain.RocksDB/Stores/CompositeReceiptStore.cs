using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeReceiptStore : IReceiptStore
    {
        private readonly IReceiptStore _history;
        private readonly RocksDbReceiptStore _hot;
        private readonly RocksDbHotBlockWindowStore _hotWindow;

        public CompositeReceiptStore(IReceiptStore history, RocksDbReceiptStore hot, RocksDbHotBlockWindowStore hotWindow)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _hotWindow = hotWindow ?? throw new ArgumentNullException(nameof(hotWindow));
        }

        public async Task<Receipt> GetByTxHashAsync(byte[] txHash)
            => await _hot.GetByTxHashAsync(txHash).ConfigureAwait(false)
               ?? await _history.GetByTxHashAsync(txHash).ConfigureAwait(false);

        public async Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
            => await _hot.GetInfoByTxHashAsync(txHash).ConfigureAwait(false)
               ?? await _history.GetInfoByTxHashAsync(txHash).ConfigureAwait(false);

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
            => HotThenHistory.ReadAsync(
                _hotWindow.ContainsBlock((ulong)blockNumber),
                () => _hot.GetByBlockNumberAsync(blockNumber),
                HotThenHistory.NotEmpty,
                () => _history.GetByBlockNumberAsync(blockNumber));

        public async Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = _hotWindow.TryGetBlockNumberByHash(blockHash);
            return await HotThenHistory.ReadAsync(
                number.HasValue,
                () => _hot.GetByBlockNumberAsync((BigInteger)number.Value),
                HotThenHistory.NotEmpty,
                () => _history.GetByBlockHashAsync(blockHash)).ConfigureAwait(false);
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex,
            BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
            => _hot.SaveAsync(receipt, txHash, blockHash, blockNumber, txIndex, gasUsed, contractAddress, effectiveGasPrice);

        public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ReceiptSaveItem> items)
            => _hot.SaveManyAsync(blockHash, blockNumber, items);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => _hot.DeleteByBlockNumberAsync(blockNumber);
    }
}
