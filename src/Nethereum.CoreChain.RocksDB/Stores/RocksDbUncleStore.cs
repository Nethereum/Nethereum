using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.RLP;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbUncleStore : IUncleStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockEncodingProvider _provider;
        private readonly ColumnFamilyHandle _blockMeta, _blockHashIndex;

        public RocksDbUncleStore(RocksDbManager manager, IBlockStore blockStore = null, IBlockEncodingProvider provider = null,
            string blockMetaCf = null, string blockHashIndexCf = null)
        {
            _manager = manager;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
            _blockMeta = manager.GetColumnFamily(blockMetaCf ?? HistoryColumnFamilies.BlockMeta);
            _blockHashIndex = manager.GetColumnFamily(blockHashIndexCf ?? HistoryColumnFamilies.BlockHashIndex);
        }

        private IList<BlockHeader> Decode(byte[] unclesRlp)
        {
            var result = new List<BlockHeader>();
            if (unclesRlp == null || unclesRlp.Length == 0) return result;
            foreach (var item in (RLPCollection)RLP.RLP.Decode(unclesRlp))
            {
                var h = _provider.DecodeBlockHeader(item.RLPData);
                if (h != null) result.Add(h);
            }
            return result;
        }

        public Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            byte[] metaBytes;
            using (var lease = _manager.Lease()) metaBytes = lease.Database.Get(HistoryKeys.BlockKey((ulong)blockNumber), _blockMeta);
            return Task.FromResult(Decode(BlockMetaCodec.Decode(metaBytes)?.Uncles));
        }

        public Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash)
        {
            var n = Number(blockHash);
            return n.HasValue ? GetByBlockNumberAsync(n.Value) : Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
        }

        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles)
        {
            var n = Number(blockHash);
            if (!n.HasValue) return Task.CompletedTask;
            var key = HistoryKeys.BlockKey(n.Value);
            using var lease = _manager.Lease();
            var meta = BlockMetaCodec.Decode(lease.Database.Get(key, _blockMeta)) ?? new BlockMeta { BlockHash = blockHash };
            var encoded = new byte[uncles?.Count ?? 0][];
            for (int i = 0; i < (uncles?.Count ?? 0); i++) encoded[i] = _provider.EncodeBlockHeader(uncles[i]);
            meta.Uncles = RLP.RLP.EncodeList(encoded);
            lease.Database.Put(key, BlockMetaCodec.Encode(meta), _blockMeta);
            return Task.CompletedTask;
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash) => Task.CompletedTask;
        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => Task.CompletedTask;

        private ulong? Number(byte[] blockHash)
        {
            if (blockHash == null) return null;
            using var lease = _manager.Lease();
            var num = lease.Database.Get(blockHash, _blockHashIndex);
            return num == null || num.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(num);
        }
    }
}
