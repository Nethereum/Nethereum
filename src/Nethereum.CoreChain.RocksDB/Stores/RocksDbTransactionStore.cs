using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbTransactionStore : ITransactionStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;

        public RocksDbTransactionStore(RocksDbManager manager, IBlockStore blockStore = null)
        {
            _manager = manager;
            _blockStore = blockStore;
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<ISignedTransaction>(null);

            var data = _manager.Get(RocksDbManager.CF_TRANSACTIONS, txHash);
            if (data == null) return Task.FromResult<ISignedTransaction>(null);

            var tx = DeserializeStoredTransaction(data);
            return Task.FromResult(tx);
        }

        public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<ISignedTransaction>();
            if (blockHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_TX_BY_BLOCK);
            var prefix = blockHash;
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!StartsWith(key, prefix))
                    break;

                var txHash = iterator.Value();
                var txData = _manager.Get(RocksDbManager.CF_TRANSACTIONS, txHash);
                if (txData != null)
                {
                    var tx = DeserializeStoredTransaction(txData);
                    if (tx != null)
                    {
                        result.Add(tx);
                    }
                }

                iterator.Next();
            }

            return Task.FromResult(result);
        }

        public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<byte[]>();
            if (blockHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_TX_BY_BLOCK);
            var prefix = blockHash;
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!StartsWith(key, prefix))
                    break;

                var txHash = iterator.Value();
                if (txHash != null)
                    result.Add(txHash);

                iterator.Next();
            }

            return Task.FromResult(result);
        }

        public async Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null)
                return new List<ISignedTransaction>();

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            if (blockHash == null)
                return new List<ISignedTransaction>();

            return await GetByBlockHashAsync(blockHash);
        }

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber)
        {
            if (tx == null || blockHash == null) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            var txCf = _manager.GetColumnFamily(RocksDbManager.CF_TRANSACTIONS);
            var txByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_TX_BY_BLOCK);

            var txHash = tx.Hash;
            var txRlp = tx.GetRLPEncoded();
            var storedData = SerializeStoredTransaction(txRlp, blockHash, txIndex, blockNumber);
            batch.Put(txHash, storedData, txCf);

            var blockTxKey = CreateBlockTxKey(blockHash, txIndex);
            batch.Put(blockTxKey, txHash, txByBlockCf);

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<TransactionLocation>(null);

            var data = _manager.Get(RocksDbManager.CF_TRANSACTIONS, txHash);
            if (data == null) return Task.FromResult<TransactionLocation>(null);

            var location = DeserializeTransactionLocation(data);
            return Task.FromResult(location);
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return;

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            if (blockHash == null) return;

            using var batch = _manager.CreateWriteBatch();
            var txCf = _manager.GetColumnFamily(RocksDbManager.CF_TRANSACTIONS);
            var txByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_TX_BY_BLOCK);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_TX_BY_BLOCK);
            iterator.Seek(blockHash);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!StartsWith(key, blockHash))
                    break;

                var txHash = iterator.Value();
                batch.Delete(txHash, txCf);
                batch.Delete(key, txByBlockCf);

                iterator.Next();
            }

            _manager.Write(batch);
        }

        private static byte[] SerializeStoredTransaction(byte[] txRlp, byte[] blockHash, int txIndex, BigInteger blockNumber)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(txRlp),
                RLP.RLP.EncodeElement(blockHash),
                RLP.RLP.EncodeElement(txIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(blockNumber.ToBytesForRLPEncoding())
            );
        }

        private static ISignedTransaction DeserializeStoredTransaction(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            try
            {
                var decoded = RLP.RLP.Decode(data);
                var elements = (RLPCollection)decoded;

                var txRlp = elements[0].RLPData;
                return TransactionFactory.CreateTransaction(txRlp);
            }
            catch
            {
                return null;
            }
        }

        private static TransactionLocation DeserializeTransactionLocation(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            try
            {
                var decoded = RLP.RLP.Decode(data);
                var elements = (RLPCollection)decoded;

                var location = new TransactionLocation
                {
                    BlockHash = elements[1].RLPData,
                    TransactionIndex = (int)elements[2].RLPData.ToLongFromRLPDecoded()
                };

                if (elements.Count > 3 && elements[3].RLPData != null)
                {
                    location.BlockNumber = elements[3].RLPData.ToBigIntegerFromRLPDecoded();
                }

                return location;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] CreateBlockTxKey(byte[] blockHash, int txIndex)
        {
            var indexBytes = BitConverter.GetBytes(txIndex);
            var key = new byte[blockHash.Length + indexBytes.Length];
            Buffer.BlockCopy(blockHash, 0, key, 0, blockHash.Length);
            Buffer.BlockCopy(indexBytes, 0, key, blockHash.Length, indexBytes.Length);
            return key;
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || prefix == null) return false;
            if (data.Length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i]) return false;
            }
            return true;
        }
    }
}
