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

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_RECEIPTS);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var data = iterator.Value();
                var info = RocksDbSerializer.DeserializeReceiptInfo(data);
                if (info != null && ByteArrayEquals(info.BlockHash, blockHash))
                {
                    result.Add(info.Receipt);
                }
                iterator.Next();
            }

            result.Sort((a, b) => 0);
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

            var data = RocksDbSerializer.SerializeReceiptInfo(info);
            _manager.Put(RocksDbManager.CF_RECEIPTS, txHash, data);

            return Task.CompletedTask;
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
