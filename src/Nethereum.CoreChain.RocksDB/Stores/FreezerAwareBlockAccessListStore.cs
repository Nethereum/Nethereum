using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareBlockAccessListStore : IBlockAccessListStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly IBlockAccessListStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareBlockAccessListStore(FreezerHistoryStore frozen, IBlockAccessListStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? ((IBlockAccessListStore)_frozen).GetByBlockNumberAsync(blockNumber)
                : _recent.GetByBlockNumberAsync(blockNumber);

        public Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            if (!_router.TryResolveNumber(blockHash, out var n)) return Task.FromResult<byte[]>(null);
            return GetByBlockNumberAsync(n);
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp) => _recent.SaveAsync(blockHash, blockAccessListRlp);

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
