using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeBlockAccessListStore : IBlockAccessListStore
    {
        private readonly IBlockAccessListStore _history;
        private readonly RocksDbBlockAccessListStore _hot;
        private readonly RocksDbHotBlockWindowStore _hotWindow;

        public CompositeBlockAccessListStore(IBlockAccessListStore history, RocksDbBlockAccessListStore hot,
            RocksDbHotBlockWindowStore hotWindow)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _hotWindow = hotWindow ?? throw new ArgumentNullException(nameof(hotWindow));
        }

        public async Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
            => _hotWindow.ContainsBlock((ulong)blockNumber)
                ? await _hot.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false)
                : await _history.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false);

        public async Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = _hotWindow.TryGetBlockNumberByHash(blockHash);
            return number.HasValue
                ? await _hot.GetByBlockNumberAsync((BigInteger)number.Value).ConfigureAwait(false)
                : await _history.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp)
            => _hot.SaveAsync(blockHash, blockAccessListRlp);

        public Task DeleteByBlockHashAsync(byte[] blockHash)
            => _hot.DeleteByBlockHashAsync(blockHash);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => _hot.DeleteByBlockNumberAsync(blockNumber);
    }
}
