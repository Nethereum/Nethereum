using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareUncleStore : IUncleStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly IUncleStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareUncleStore(FreezerHistoryStore frozen, IUncleStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? ((IUncleStore)_frozen).GetByBlockNumberAsync(blockNumber)
                : _recent.GetByBlockNumberAsync(blockNumber);

        public async Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return new List<BlockHeader>();
            return await GetByBlockNumberAsync(n).ConfigureAwait(false);
        }

        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles) => _recent.SaveAsync(blockHash, uncles);

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
