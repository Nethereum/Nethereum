using System;
using Nethereum.CoreChain.Freezer;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbHotWindowRandomKeyIndexAdapter : IRandomKeyIndexStore
    {
        private readonly RocksDbHotBlockWindowStore _hot;

        public RocksDbHotWindowRandomKeyIndexAdapter(RocksDbHotBlockWindowStore hot)
        {
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
        }

        public bool TryGetBlockNumberByHash(byte[] blockHash, out long number)
        {
            var found = _hot.TryGetBlockNumberByHash(blockHash);
            number = found.HasValue ? (long)found.Value : 0;
            return found.HasValue;
        }

        public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex)
        {
            var location = _hot.TryGetTransactionLocation(txHash);
            if (!location.HasValue)
            {
                blockNumber = 0;
                txIndex = 0;
                return false;
            }

            blockNumber = (long)location.Value.Block;
            txIndex = (int)location.Value.Index;
            return true;
        }

        public void PutBlockHash(byte[] hash, long number) =>
            throw new NotSupportedException(
                "read-only: the hot window owns its own by-hash writes via WriteHeader/StageOne");

        public void PutTxLocation(byte[] txHash, long blockNumber, int txIndex) =>
            throw new NotSupportedException(
                "read-only: the hot window owns its own by-hash writes via WriteHeader/StageOne");

        public void RemoveBlock(long number)
        {
        }
    }
}
