using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeUncleStore : IUncleStore
    {
        private readonly IUncleStore _history;
        private readonly IUncleStore _hot;
        private readonly RocksDbHotBlockWindowStore _hotWindow;

        public CompositeUncleStore(IUncleStore history, IUncleStore hot, RocksDbHotBlockWindowStore hotWindow)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _hotWindow = hotWindow ?? throw new ArgumentNullException(nameof(hotWindow));
        }

        public async Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber)
            => _hotWindow.ContainsBlock((ulong)blockNumber)
                ? await _hot.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false)
                : await _history.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false);

        public async Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = _hotWindow.TryGetBlockNumberByHash(blockHash);
            return number.HasValue
                ? await _hot.GetByBlockNumberAsync((BigInteger)number.Value).ConfigureAwait(false)
                : await _history.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles) => _hot.SaveAsync(blockHash, uncles);

        public Task DeleteByBlockHashAsync(byte[] blockHash) => _hot.DeleteByBlockHashAsync(blockHash);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => _hot.DeleteByBlockNumberAsync(blockNumber);
    }
}
