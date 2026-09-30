using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareWithdrawalStore : IWithdrawalStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly IWithdrawalStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareWithdrawalStore(FreezerHistoryStore frozen, IWithdrawalStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<IList<Withdrawal>> GetByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? ((IWithdrawalStore)_frozen).GetByBlockNumberAsync(blockNumber)
                : _recent.GetByBlockNumberAsync(blockNumber);

        public Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return Task.FromResult<IList<Withdrawal>>(null);
            return GetByBlockNumberAsync(n);
        }

        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals) => _recent.SaveAsync(blockHash, withdrawals);

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return Task.CompletedTask;
            return n < _router.FrozenCount() ? _frozen.DeleteByBlockHashAsync(blockHash) : _recent.DeleteByBlockHashAsync(blockHash);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? _frozen.DeleteByBlockNumberAsync(blockNumber)
                : _recent.DeleteByBlockNumberAsync(blockNumber);
    }
}
