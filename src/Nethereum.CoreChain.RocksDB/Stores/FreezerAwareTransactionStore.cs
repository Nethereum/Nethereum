using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareTransactionStore : ITransactionStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly ITransactionStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareTransactionStore(FreezerHistoryStore frozen, ITransactionStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount() ? _frozen.GetByBlockNumberAsync(blockNumber) : _recent.GetByBlockNumberAsync(blockNumber);

        public async Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return new List<ISignedTransaction>();
            return await GetByBlockNumberAsync(n).ConfigureAwait(false);
        }

        public async Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return new List<byte[]>();
            var txs = await GetByBlockNumberAsync(n).ConfigureAwait(false);
            return txs.Select(t => t.Hash).ToList();
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            if (!_router.TryResolveTxLocation(txHash, out var blockNumber, out _))
                return Task.FromResult<ISignedTransaction>(null);
            return blockNumber < _router.FrozenCount()
                ? ((ITransactionStore)_frozen).GetByHashAsync(txHash)
                : _recent.GetByHashAsync(txHash);
        }

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            if (!_router.TryResolveTxLocation(txHash, out var blockNumber, out _))
                return Task.FromResult<TransactionLocation>(null);
            return blockNumber < _router.FrozenCount() ? _frozen.GetLocationAsync(txHash) : _recent.GetLocationAsync(txHash);
        }

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber)
            => _recent.SaveAsync(tx, blockHash, txIndex, blockNumber);

        public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs)
            => _recent.SaveManyAsync(blockHash, blockNumber, txs);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? _frozen.DeleteByBlockNumberAsync(blockNumber)
                : _recent.DeleteByBlockNumberAsync(blockNumber);
    }
}
