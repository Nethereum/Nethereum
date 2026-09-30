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
            if (raw == null || raw.Length < HistoryKeys.BlockKeyLength)
            {
                number = 0;
                return false;
            }

            number = (long)HistoryKeys.ReadBlockNumber(raw);
            return true;
        }

        public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex)
        {
            byte[] raw;
            using (var lease = _manager.Lease())
                raw = txHash == null ? null : lease.Database.Get(txHash, _txHashIndex);
            if (raw == null || raw.Length < HistoryKeys.TxKeyLength)
            {
                blockNumber = 0;
                txIndex = 0;
                return false;
            }

            blockNumber = (long)HistoryKeys.ReadBlockNumber(raw);
            txIndex = (int)HistoryKeys.ReadTxIndex(raw);
            return true;
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
