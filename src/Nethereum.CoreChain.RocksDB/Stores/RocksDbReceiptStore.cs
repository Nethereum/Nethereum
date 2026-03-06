using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbReceiptStore : IReceiptStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;

        public RocksDbReceiptStore(RocksDbManager manager, IBlockStore blockStore = null)
        {
            _manager = manager;
            _blockStore = blockStore;
        }

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<Receipt>(null);

            var data = _manager.Get(RocksDbManager.CF_RECEIPTS, txHash);
            if (data == null) return Task.FromResult<Receipt>(null);

            var info = RocksDbSerializer.DeserializeReceiptInfo(data);
            return Task.FromResult(info?.Receipt);
        }

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<ReceiptInfo>(null);

            var data = _manager.Get(RocksDbManager.CF_RECEIPTS, txHash);
            if (data == null) return Task.FromResult<ReceiptInfo>(null);

            var info = RocksDbSerializer.DeserializeReceiptInfo(data);
            return Task.FromResult(info);
        }

        public Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<Receipt>();
            if (blockHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_RECEIPT_BY_BLOCK);
            iterator.Seek(blockHash);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!StartsWith(key, blockHash))
                    break;

                var txHash = iterator.Value();
                var receiptData = _manager.Get(RocksDbManager.CF_RECEIPTS, txHash);
                if (receiptData != null)
                {
                    var info = RocksDbSerializer.DeserializeReceiptInfo(receiptData);
                    if (info != null)
                        result.Add(info.Receipt);
                }

                iterator.Next();
            }

            return Task.FromResult(result);
        }

        public async Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null)
                return new List<Receipt>();

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            if (blockHash == null)
                return new List<Receipt>();

            return await GetByBlockHashAsync(blockHash);
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            if (receipt == null || txHash == null) return Task.CompletedTask;

            var info = new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = txHash,
                BlockHash = blockHash,
                BlockNumber = blockNumber,
                TransactionIndex = txIndex,
                GasUsed = gasUsed,
                ContractAddress = contractAddress,
                EffectiveGasPrice = effectiveGasPrice
            };

            using var batch = _manager.CreateWriteBatch();
            var receiptsCf = _manager.GetColumnFamily(RocksDbManager.CF_RECEIPTS);
            var receiptByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_RECEIPT_BY_BLOCK);

            var data = RocksDbSerializer.SerializeReceiptInfo(info);
            batch.Put(txHash, data, receiptsCf);

            if (blockHash != null)
            {
                var blockReceiptKey = CreateBlockReceiptKey(blockHash, txIndex);
                batch.Put(blockReceiptKey, txHash, receiptByBlockCf);
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return;

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            if (blockHash == null) return;

            using var batch = _manager.CreateWriteBatch();
            var receiptsCf = _manager.GetColumnFamily(RocksDbManager.CF_RECEIPTS);
            var receiptByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_RECEIPT_BY_BLOCK);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_RECEIPT_BY_BLOCK);
            iterator.Seek(blockHash);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!StartsWith(key, blockHash))
                    break;

                var txHash = iterator.Value();
                batch.Delete(txHash, receiptsCf);
                batch.Delete(key, receiptByBlockCf);

                iterator.Next();
            }

            _manager.Write(batch);
        }

        private static byte[] CreateBlockReceiptKey(byte[] blockHash, int txIndex)
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
