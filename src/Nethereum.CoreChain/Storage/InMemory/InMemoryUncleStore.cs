using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryUncleStore : IUncleStore
    {
        private readonly ConcurrentDictionary<string, IList<BlockHeader>> _unclesByBlockHash = new();
        private readonly IBlockStore _blockStore;

        public InMemoryUncleStore(IBlockStore blockStore = null)
        {
            _blockStore = blockStore;
        }

        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles)
        {
            if (blockHash == null || uncles == null) return Task.CompletedTask;
            _unclesByBlockHash[ToHex(blockHash)] = uncles;
            return Task.CompletedTask;
        }

        public Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
            if (_unclesByBlockHash.TryGetValue(ToHex(blockHash), out var uncles))
                return Task.FromResult<IList<BlockHeader>>(uncles);
            return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
        }

        public async Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return new List<BlockHeader>();
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
            if (blockHash == null) return new List<BlockHeader>();
            return await GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.CompletedTask;
            _unclesByBlockHash.TryRemove(ToHex(blockHash), out _);
            return Task.CompletedTask;
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return;
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
            if (blockHash != null) await DeleteByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        private static string ToHex(byte[] bytes) => bytes == null ? null : bytes.ToHex();
    }
}
