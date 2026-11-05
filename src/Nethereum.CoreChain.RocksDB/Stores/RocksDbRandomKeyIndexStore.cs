using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbRandomKeyIndexStore : IRandomKeyIndexStore
    {
        private readonly RocksDbManager _manager;
        private readonly ColumnFamilyHandle _blockHashIndex, _txHashIndex;

        public RocksDbRandomKeyIndexStore(RocksDbManager manager)
        {
            _manager = manager;
            _blockHashIndex = manager.GetColumnFamily(HistoryColumnFamilies.BlockHashIndex);
            _txHashIndex = manager.GetColumnFamily(HistoryColumnFamilies.TxHashIndex);
        }

        public bool TryGetBlockNumberByHash(byte[] blockHash, out long number)
        {
            byte[] raw;
            using (var lease = _manager.Lease())
                raw = blockHash == null ? null : lease.Database.Get(blockHash, _blockHashIndex);
            var read = HistoryKeys.TryReadBlockNumber(raw);
            number = read.HasValue ? (long)read.Value : 0;
            return read.HasValue;
        }

        public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex)
        {
            byte[] raw;
            using (var lease = _manager.Lease())
                raw = txHash == null ? null : lease.Database.Get(txHash, _txHashIndex);
            var read = HistoryKeys.TryReadTxLocation(raw);
            blockNumber = read.HasValue ? (long)read.Value.BlockNumber : 0;
            txIndex = read.HasValue ? (int)read.Value.TxIndex : 0;
            return read.HasValue;
        }

        public void PutBlockHash(byte[] hash, long number)
        {
            using var lease = _manager.Lease();
            lease.Database.Put(hash, HistoryKeys.BlockKey((ulong)number), _blockHashIndex);
        }

        public void PutTxLocation(byte[] txHash, long blockNumber, int txIndex)
        {
            using var lease = _manager.Lease();
            lease.Database.Put(txHash, HistoryKeys.TxKey((ulong)blockNumber, (uint)txIndex), _txHashIndex);
        }

        public void RemoveBlock(long number)
        {
        }
    }
}
