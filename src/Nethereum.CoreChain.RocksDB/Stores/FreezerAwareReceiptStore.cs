using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareReceiptStore : IReceiptStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly IReceiptStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareReceiptStore(FreezerHistoryStore frozen, IReceiptStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? ((IReceiptStore)_frozen).GetByBlockNumberAsync(blockNumber)
                : _recent.GetByBlockNumberAsync(blockNumber);

        public async Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return new List<Receipt>();
            return await GetByBlockNumberAsync(n).ConfigureAwait(false);
        }

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
        {
            if (!_router.TryResolveTxLocation(txHash, out var blockNumber, out _))
                return Task.FromResult<Receipt>(null);
            return blockNumber < _router.FrozenCount() ? _frozen.GetByTxHashAsync(txHash) : _recent.GetByTxHashAsync(txHash);
        }

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            if (!_router.TryResolveTxLocation(txHash, out var blockNumber, out _))
                return Task.FromResult<ReceiptInfo>(null);
            return blockNumber < _router.FrozenCount() ? _frozen.GetInfoByTxHashAsync(txHash) : _recent.GetInfoByTxHashAsync(txHash);
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex,
            BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
            => _recent.SaveAsync(receipt, txHash, blockHash, blockNumber, txIndex, gasUsed, contractAddress, effectiveGasPrice);

        public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ReceiptSaveItem> items)
            => _recent.SaveManyAsync(blockHash, blockNumber, items);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? _frozen.DeleteByBlockNumberAsync(blockNumber)
                : _recent.DeleteByBlockNumberAsync(blockNumber);
    }
}
