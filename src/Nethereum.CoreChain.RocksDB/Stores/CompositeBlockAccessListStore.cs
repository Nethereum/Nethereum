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

        public Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
            => HotThenHistory.ReadAsync(
                _hotWindow.ContainsBlock((ulong)blockNumber),
                () => _hot.GetByBlockNumberAsync(blockNumber),
                HotThenHistory.NotNull,
                () => _history.GetByBlockNumberAsync(blockNumber));

        public async Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = _hotWindow.TryGetBlockNumberByHash(blockHash);
            return await HotThenHistory.ReadAsync(
                number.HasValue,
                () => _hot.GetByBlockNumberAsync((BigInteger)number.Value),
                HotThenHistory.NotNull,
                () => _history.GetByBlockHashAsync(blockHash)).ConfigureAwait(false);
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp)
            => _hot.SaveAsync(blockHash, blockAccessListRlp);

        public Task DeleteByBlockHashAsync(byte[] blockHash)
            => _hot.DeleteByBlockHashAsync(blockHash);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => _hot.DeleteByBlockNumberAsync(blockNumber);
    }
}
