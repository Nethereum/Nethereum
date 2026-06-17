using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    /// <summary>
    /// RocksDB-backed <see cref="IUncleStore"/>. Stores the uncle list as a
    /// single RLP-encoded blob keyed by block hash — mirrors how geth and
    /// erigon persist block bodies. The IBlockEncodingProvider hook keeps
    /// the encoding pluggable so AppChain forks (or future ssz-encoded
    /// PBS bodies) reuse the same store with a different codec.
    /// </summary>
    public class RocksDbUncleStore : IUncleStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;
        private readonly IBlockEncodingProvider _provider;

        public RocksDbUncleStore(
            RocksDbManager manager,
            IBlockStore blockStore = null,
            IBlockEncodingProvider provider = null)
        {
            _manager = manager;
            _blockStore = blockStore;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
        }

        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles)
        {
            if (blockHash == null) return Task.CompletedTask;
            // Always write — even an empty uncle list is meaningful (so a
            // GetByBlockHashAsync returns a concrete empty list, not null,
            // letting the re-execute loop distinguish "no uncles stored"
            // from "uncles weren't persisted at all" if needed).
            var encodedUncles = new byte[uncles?.Count ?? 0][];
            for (int i = 0; i < (uncles?.Count ?? 0); i++)
            {
                encodedUncles[i] = _provider.EncodeBlockHeader(uncles[i]);
            }
            var listRlp = RLP.RLP.EncodeList(encodedUncles);
            _manager.Put(RocksDbManager.CF_UNCLES, blockHash, listRlp);
            return Task.CompletedTask;
        }

        public Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
            var data = _manager.Get(RocksDbManager.CF_UNCLES, blockHash);
            if (data == null || data.Length == 0)
                return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());

            IList<BlockHeader> result = new List<BlockHeader>();
            try
            {
                var decoded = RLP.RLP.Decode(data);
                if (decoded is RLPCollection list)
                {
                    foreach (var item in list)
                    {
                        var header = _provider.DecodeBlockHeader(item.RLPData);
                        if (header != null) result.Add(header);
                    }
                }
            }
            catch
            {
                // corrupt entry — return empty list rather than throw; the
                // re-exec loop will detect the missing-rewards divergence.
            }
            return Task.FromResult(result);
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
            _manager.Delete(RocksDbManager.CF_UNCLES, blockHash);
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
