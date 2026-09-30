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
    public class RocksDbWithdrawalStore : IWithdrawalStore
    {
        private readonly RocksDbManager _manager;
        private readonly ColumnFamilyHandle _blockMeta, _blockHashIndex;

        public RocksDbWithdrawalStore(RocksDbManager manager, IBlockStore blockStore = null,
            string blockMetaCf = null, string blockHashIndexCf = null)
        {
            _manager = manager;
            _blockMeta = manager.GetColumnFamily(blockMetaCf ?? HistoryColumnFamilies.BlockMeta);
            _blockHashIndex = manager.GetColumnFamily(blockHashIndexCf ?? HistoryColumnFamilies.BlockHashIndex);
        }

        private static IList<Withdrawal> Decode(byte[] withdrawalsRlp)
        {
            if (withdrawalsRlp == null) return null;
            var result = new List<Withdrawal>();
            if (withdrawalsRlp.Length == 0) return result;
            foreach (var item in (RLPCollection)RLP.RLP.Decode(withdrawalsRlp))
            {
                var w = WithdrawalEncoder.Current.Decode(item.RLPData);
                if (w != null) result.Add(w);
            }
            return result;
        }

        public Task<IList<Withdrawal>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            byte[] metaBytes;
            using (var lease = _manager.Lease()) metaBytes = lease.Database.Get(HistoryKeys.BlockKey((ulong)blockNumber), _blockMeta);
            return Task.FromResult(Decode(BlockMetaCodec.Decode(metaBytes)?.Withdrawals));
        }

        public Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash)
        {
            var n = Number(blockHash);
            return n.HasValue ? GetByBlockNumberAsync(n.Value) : Task.FromResult<IList<Withdrawal>>(null);
        }

        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals)
        {
            if (withdrawals == null) return Task.CompletedTask;
            var n = Number(blockHash);
            if (!n.HasValue) return Task.CompletedTask;
            var key = HistoryKeys.BlockKey(n.Value);
            using var lease = _manager.Lease();
            var meta = BlockMetaCodec.Decode(lease.Database.Get(key, _blockMeta)) ?? new BlockMeta { BlockHash = blockHash };
            var encoded = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++) encoded[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);
            meta.Withdrawals = RLP.RLP.EncodeList(encoded);
            lease.Database.Put(key, BlockMetaCodec.Encode(meta), _blockMeta);
            return Task.CompletedTask;
        }

        public void StageInto(WriteBatch batch, byte[] blockHash, IList<Withdrawal> withdrawals)
        {
            if (withdrawals == null) return;
            var n = Number(blockHash);
            if (!n.HasValue) return;
            var key = HistoryKeys.BlockKey(n.Value);
            byte[] metaBytes;
            using (var lease = _manager.Lease()) metaBytes = lease.Database.Get(key, _blockMeta);
            var meta = BlockMetaCodec.Decode(metaBytes) ?? new BlockMeta { BlockHash = blockHash };
            var encoded = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++) encoded[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);
            meta.Withdrawals = RLP.RLP.EncodeList(encoded);
            batch.Put(key, BlockMetaCodec.Encode(meta), _blockMeta);
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
