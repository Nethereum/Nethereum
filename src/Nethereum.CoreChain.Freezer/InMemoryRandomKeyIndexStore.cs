using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class InMemoryRandomKeyIndexStore : IRandomKeyIndexStore
    {
        private readonly ConcurrentDictionary<byte[], long> _blockNumberByHash = new(ByteArrayComparer.Current);
        private readonly ConcurrentDictionary<long, byte[]> _hashByBlockNumber = new();

        private readonly ConcurrentDictionary<byte[], TxLocation> _txLocationByHash = new(ByteArrayComparer.Current);
        private readonly ConcurrentDictionary<long, List<byte[]>> _txHashesByBlockNumber = new();

        public bool TryGetBlockNumberByHash(byte[] blockHash, out long number)
        {
            if (blockHash != null && _blockNumberByHash.TryGetValue(blockHash, out number))
                return true;

            number = 0;
            return false;
        }

        public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex)
        {
            if (txHash != null && _txLocationByHash.TryGetValue(txHash, out var location))
            {
                blockNumber = location.BlockNumber;
                txIndex = location.TxIndex;
                return true;
            }

            blockNumber = 0;
            txIndex = 0;
            return false;
        }

        public void PutBlockHash(byte[] hash, long number)
        {
            _blockNumberByHash[hash] = number;
            _hashByBlockNumber[number] = hash;
        }

        public void PutTxLocation(byte[] txHash, long blockNumber, int txIndex)
        {
            _txLocationByHash[txHash] = new TxLocation(blockNumber, txIndex);
            var perBlock = _txHashesByBlockNumber.GetOrAdd(blockNumber, _ => new List<byte[]>());
            lock (perBlock)
                perBlock.Add(txHash);
        }

        public void RemoveBlock(long number)
        {
            if (_hashByBlockNumber.TryRemove(number, out var hash))
                _blockNumberByHash.TryRemove(hash, out _);

            if (!_txHashesByBlockNumber.TryRemove(number, out var txHashes))
                return;

            List<byte[]> snapshot;
            lock (txHashes)
                snapshot = new List<byte[]>(txHashes);

            foreach (var txHash in snapshot)
                _txLocationByHash.TryRemove(txHash, out _);
        }

        private readonly struct TxLocation
        {
            public TxLocation(long blockNumber, int txIndex)
            {
                BlockNumber = blockNumber;
                TxIndex = txIndex;
            }

            public long BlockNumber { get; }
            public int TxIndex { get; }
        }
    }
}
