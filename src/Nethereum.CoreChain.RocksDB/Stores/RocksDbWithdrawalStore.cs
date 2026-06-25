using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    /// <summary>
    /// RocksDB-backed <see cref="IWithdrawalStore"/>. Stores the withdrawal
    /// list as a single RLP-encoded blob keyed by block hash — same shape as
    /// <see cref="RocksDbUncleStore"/>. Withdrawals are persisted with the
    /// canonical <see cref="WithdrawalEncoder"/> so the read-back path can
    /// rebuild byte-identical bodies for header-hash recomputation.
    /// </summary>
    public class RocksDbWithdrawalStore : IWithdrawalStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;

        public RocksDbWithdrawalStore(
            RocksDbManager manager,
            IBlockStore blockStore = null)
        {
            _manager = manager;
            _blockStore = blockStore;
        }

        /// <summary>
        /// Persist the withdrawal list for a block. <c>null</c> withdrawals is
        /// a no-op (pre-Shanghai block has no row). An empty list IS
        /// persisted as a zero-length sentinel blob so the read path returns
        /// an empty list rather than <c>null</c> for Shanghai+ blocks with no
        /// withdrawals.
        /// </summary>
        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals)
        {
            if (blockHash == null) return Task.CompletedTask;
            if (withdrawals == null) return Task.CompletedTask;
            if (withdrawals.Count == 0)
            {
                _manager.Put(RocksDbManager.CF_WITHDRAWALS, blockHash, Array.Empty<byte>());
                return Task.CompletedTask;
            }
            var encoded = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++)
            {
                encoded[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);
            }
            var listRlp = RLP.RLP.EncodeList(encoded);
            _manager.Put(RocksDbManager.CF_WITHDRAWALS, blockHash, listRlp);
            return Task.CompletedTask;
        }

        public Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<IList<Withdrawal>>(null);
            var data = _manager.Get(RocksDbManager.CF_WITHDRAWALS, blockHash);
            if (data == null) return Task.FromResult<IList<Withdrawal>>(null);
            // Zero-length sentinel marks "Shanghai+ block, no withdrawals" — distinct from "no row".
            if (data.Length == 0)
                return Task.FromResult<IList<Withdrawal>>(new List<Withdrawal>());

            IList<Withdrawal> result = new List<Withdrawal>();
            try
            {
                var decoded = RLP.RLP.Decode(data);
                if (decoded is RLPCollection list)
                {
                    foreach (var item in list)
                    {
                        var w = WithdrawalEncoder.Current.Decode(item.RLPData);
                        if (w != null) result.Add(w);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new StoreCorruptionException(
                    nameof(RocksDbWithdrawalStore),
                    blockHash.ToHex(),
                    ex);
            }
            return Task.FromResult(result);
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
            _manager.Delete(RocksDbManager.CF_WITHDRAWALS, blockHash);
            return Task.CompletedTask;
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return;
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
            if (blockHash != null) await DeleteByBlockHashAsync(blockHash).ConfigureAwait(false);
        }
    }
}
