using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryWithdrawalStore : IWithdrawalStore
    {
        private readonly ConcurrentDictionary<string, IList<Withdrawal>> _withdrawalsByBlockHash = new();
        private readonly IBlockStore _blockStore;

        public InMemoryWithdrawalStore(IBlockStore blockStore = null)
        {
            _blockStore = blockStore;
        }

        /// <summary>
        /// Persist the withdrawal list for a block. A <c>null</c> list is a
        /// no-op (pre-Shanghai block — no withdrawals row). An empty list IS
        /// persisted as a Shanghai+ block that simply has no withdrawals for
        /// this slot; the read-back path distinguishes "no row" from
        /// "empty row" so callers can recompute the empty-trie withdrawals
        /// root without re-fetching from a peer.
        /// </summary>
        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals)
        {
            if (blockHash == null) return Task.CompletedTask;
            if (withdrawals == null) return Task.CompletedTask;
            _withdrawalsByBlockHash[ToHex(blockHash)] = withdrawals.Count == 0
                ? new List<Withdrawal>()
                : withdrawals;
            return Task.CompletedTask;
        }

        public Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<IList<Withdrawal>>(null);
            if (_withdrawalsByBlockHash.TryGetValue(ToHex(blockHash), out var withdrawals))
                return Task.FromResult<IList<Withdrawal>>(withdrawals);
            return Task.FromResult<IList<Withdrawal>>(null);
        }

        public async Task<IList<Withdrawal>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return null;
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
            if (blockHash == null) return null;
            return await GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.CompletedTask;
            _withdrawalsByBlockHash.TryRemove(ToHex(blockHash), out _);
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
