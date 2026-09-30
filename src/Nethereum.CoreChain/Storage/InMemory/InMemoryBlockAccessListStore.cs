using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryBlockAccessListStore : IBlockAccessListStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _byBlockHash = new();
        private readonly IBlockStore _blockStore;

        public InMemoryBlockAccessListStore(IBlockStore blockStore = null)
        {
            _blockStore = blockStore;
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp)
        {
            if (blockHash == null) return Task.CompletedTask;
            if (blockAccessListRlp == null) return Task.CompletedTask;
            _byBlockHash[ToHex(blockHash)] = Snapshot(blockAccessListRlp);
            return Task.CompletedTask;
        }

        public Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<byte[]>(null);
            return Task.FromResult(_byBlockHash.TryGetValue(ToHex(blockHash), out var rlp) ? Snapshot(rlp) : null);
        }

        public async Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockHash = await ResolveBlockHashAsync(blockNumber).ConfigureAwait(false);
            if (blockHash == null) return null;
            return await GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.CompletedTask;
            _byBlockHash.TryRemove(ToHex(blockHash), out _);
            return Task.CompletedTask;
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockHash = await ResolveBlockHashAsync(blockNumber).ConfigureAwait(false);
            if (blockHash != null) await DeleteByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        private async Task<byte[]> ResolveBlockHashAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return null;
            return await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
        }

        private static byte[] Snapshot(byte[] rlp)
        {
            var copy = new byte[rlp.Length];
            Array.Copy(rlp, copy, rlp.Length);
            return copy;
        }

        private static string ToHex(byte[] bytes) => bytes.ToHex();
    }
}
