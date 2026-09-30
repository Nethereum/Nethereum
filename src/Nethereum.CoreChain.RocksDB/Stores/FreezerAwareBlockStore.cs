using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareBlockStore : IBlockStore
    {
        private readonly FreezerHistoryStore _frozen;
        private readonly IBlockStore _recent;
        private readonly FreezerReadRouter _router;

        public FreezerAwareBlockStore(FreezerHistoryStore frozen, IBlockStore recent, FreezerReadRouter router)
        {
            _frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
            => (long)number < _router.FrozenCount() ? _frozen.GetByNumberAsync(number) : _recent.GetByNumberAsync(number);

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
            => _router.TryResolveNumber(hash, out var n) ? GetByNumberAsync(n) : Task.FromResult<BlockHeader>(null);

        public Task<BlockHeader> GetLatestAsync() => _recent.GetLatestAsync();

        public Task<BigInteger> GetHeightAsync() => _recent.GetHeightAsync();

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
            => (long)number < _router.FrozenCount() ? _frozen.GetHashByNumberAsync(number) : _recent.GetHashByNumberAsync(number);

        public Task<bool> ExistsAsync(byte[] hash)
        {
            if (!_router.TryResolveNumber(hash, out var n)) return Task.FromResult(false);
            return n < _router.FrozenCount() ? Task.FromResult(true) : _recent.ExistsAsync(hash);
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash) => _recent.SaveAsync(header, blockHash);

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
            => (long)blockNumber < _router.FrozenCount()
                ? _frozen.UpdateBlockHashAsync(blockNumber, newHash)
                : _recent.UpdateBlockHashAsync(blockNumber, newHash);

        public Task DeleteByNumberAsync(BigInteger blockNumber)
            => (long)blockNumber < _router.FrozenCount()
                ? _frozen.DeleteByNumberAsync(blockNumber)
                : _recent.DeleteByNumberAsync(blockNumber);
    }
}
