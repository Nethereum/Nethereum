using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbBlockAccessListStore : IBlockAccessListStore
    {
        private readonly RocksDbManager _manager;
        private readonly ColumnFamilyHandle _blockAccessList, _blockHashIndex;

        public RocksDbBlockAccessListStore(RocksDbManager manager,
            string blockAccessListCf = null, string blockHashIndexCf = null)
        {
            _manager = manager;
            _blockAccessList = manager.GetColumnFamily(blockAccessListCf ?? HistoryColumnFamilies.BlockAccessList);
            _blockHashIndex = manager.GetColumnFamily(blockHashIndexCf ?? HistoryColumnFamilies.BlockHashIndex);
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp)
        {
            if (blockAccessListRlp == null) return Task.CompletedTask;
            var number = Number(blockHash);
            if (!number.HasValue)
                throw new System.InvalidOperationException(
                    "Cannot retain a block access list for a block that is not in this store's block-hash " +
                    "index: persist the block header before its access list.");
            using (var lease = _manager.Lease())
                lease.Database.Put(HistoryKeys.BlockKey(number.Value), blockAccessListRlp, _blockAccessList);
            return Task.CompletedTask;
        }

        public Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = Number(blockHash);
            return number.HasValue ? GetByBlockNumberAsync(number.Value) : Task.FromResult<byte[]>(null);
        }

        public Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < 0) return Task.FromResult<byte[]>(null);
            using var lease = _manager.Lease();
            return Task.FromResult(lease.Database.Get(HistoryKeys.BlockKey((ulong)blockNumber), _blockAccessList));
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            var number = Number(blockHash);
            return number.HasValue ? DeleteByBlockNumberAsync(number.Value) : Task.CompletedTask;
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < 0) return Task.CompletedTask;
            using var batch = _manager.CreateWriteBatch();
            batch.Delete(HistoryKeys.BlockKey((ulong)blockNumber), _blockAccessList);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public void StageInto(WriteBatch batch, ulong blockNumber, byte[] blockAccessListRlp)
        {
            if (blockAccessListRlp == null) return;
            batch.Put(HistoryKeys.BlockKey(blockNumber), blockAccessListRlp, _blockAccessList);
        }

        private ulong? Number(byte[] blockHash)
        {
            if (blockHash == null) return null;
            using var lease = _manager.Lease();
            var num = lease.Database.Get(blockHash, _blockHashIndex);
            return num == null || num.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(num);
        }
    }
}
