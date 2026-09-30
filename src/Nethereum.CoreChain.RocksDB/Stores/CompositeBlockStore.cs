using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeBlockStore : IBlockStore
    {
        private readonly IBlockStore _history;
        private readonly RocksDbHotBlockWindowStore _hot;
        private readonly bool _writeThroughHistory;

        public CompositeBlockStore(IBlockStore history, RocksDbHotBlockWindowStore hot, bool writeThroughHistory = true)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _writeThroughHistory = writeThroughHistory;
        }

        public async Task<BlockHeader> GetByNumberAsync(BigInteger number)
            => _hot.TryGetHeader((ulong)number) ?? await _history.GetByNumberAsync(number).ConfigureAwait(false);

        public async Task<BlockHeader> GetByHashAsync(byte[] hash)
            => _hot.TryGetHeaderByHash(hash) ?? await _history.GetByHashAsync(hash).ConfigureAwait(false);

        public async Task<BlockHeader> GetLatestAsync()
        {
            var latest = _hot.TryGetLatestNumber();
            if (latest.HasValue) return _hot.TryGetHeader(latest.Value);
            return await _history.GetLatestAsync().ConfigureAwait(false);
        }

        public async Task<BigInteger> GetHeightAsync()
        {
            var latest = _hot.TryGetLatestNumber();
            if (latest.HasValue) return (BigInteger)latest.Value;
            return await _history.GetHeightAsync().ConfigureAwait(false);
        }

        public async Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            if (_writeThroughHistory)
                await _history.SaveAsync(header, blockHash).ConfigureAwait(false);
            if (ShouldWriteHot((ulong)header.BlockNumber.ToBigInteger()))
                _hot.WriteHeader(header, blockHash);
        }

        private bool ShouldWriteHot(ulong number)
        {
            if (!_writeThroughHistory) return true;
            var tip = _hot.TryGetLatestNumber();
            if (tip == null) return true;
            if (number > tip.Value) return true;
            return number + (ulong)_hot.WindowSize > tip.Value;
        }

        public async Task<bool> ExistsAsync(byte[] hash)
            => _hot.ContainsBlockHash(hash) || await _history.ExistsAsync(hash).ConfigureAwait(false);

        public async Task<byte[]> GetHashByNumberAsync(BigInteger number)
            => _hot.TryGetHash((ulong)number) ?? await _history.GetHashByNumberAsync(number).ConfigureAwait(false);

        public async Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
        {
            if (_writeThroughHistory)
                await _history.UpdateBlockHashAsync(blockNumber, newHash).ConfigureAwait(false);
            _hot.UpdateBlockHash((ulong)blockNumber, newHash);
        }

        public async Task DeleteByNumberAsync(BigInteger blockNumber)
        {
            if (_writeThroughHistory)
                await _history.DeleteByNumberAsync(blockNumber).ConfigureAwait(false);
            _hot.DeleteBlockHeader((ulong)blockNumber);
        }
    }
}
