using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryReceiptStore : IReceiptStore
    {
        private readonly ConcurrentDictionary<string, StoredReceipt> _receiptByTxHash = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _txHashesByBlockHash = new();
        private readonly ConcurrentDictionary<BigInteger, string> _blockHashByNumber = new();

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
        {
            var hashHex = ToHex(txHash);
            if (_receiptByTxHash.TryGetValue(hashHex, out var stored))
                return Task.FromResult(stored.Receipt);
            return Task.FromResult<Receipt>(null);
        }

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            var hashHex = ToHex(txHash);
            if (_receiptByTxHash.TryGetValue(hashHex, out var stored))
            {
                return Task.FromResult(new ReceiptInfo
                {
                    Receipt = stored.Receipt,
                    TxHash = txHash,
                    BlockHash = stored.BlockHash,
                    BlockNumber = stored.BlockNumber,
                    TransactionIndex = stored.TransactionIndex,
                    GasUsed = stored.GasUsed,
                    ContractAddress = stored.ContractAddress,
                    EffectiveGasPrice = stored.EffectiveGasPrice
                });
            }
            return Task.FromResult<ReceiptInfo>(null);
        }

        public Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            var hashHex = ToHex(blockHash);
            if (!_txHashesByBlockHash.TryGetValue(hashHex, out var txHashes))
                return Task.FromResult(new List<Receipt>());

            var result = txHashes.Keys
                .Select(h => _receiptByTxHash.TryGetValue(h, out var stored) ? stored : null)
                .Where(s => s != null)
                .OrderBy(s => s.TransactionIndex)
                .Select(s => s.Receipt)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            if (!_blockHashByNumber.TryGetValue(blockNumber, out var blockHashHex))
                return Task.FromResult(new List<Receipt>());

            if (!_txHashesByBlockHash.TryGetValue(blockHashHex, out var txHashes))
                return Task.FromResult(new List<Receipt>());

            var result = txHashes.Keys
                .Select(h => _receiptByTxHash.TryGetValue(h, out var stored) ? stored : null)
                .Where(s => s != null)
                .OrderBy(s => s.TransactionIndex)
                .Select(s => s.Receipt)
                .ToList();

            return Task.FromResult(result);
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            var txHashHex = ToHex(txHash);
            var blockHashHex = ToHex(blockHash);

            _receiptByTxHash[txHashHex] = new StoredReceipt
            {
                Receipt = receipt,
                BlockHash = blockHash,
                BlockNumber = blockNumber,
                TransactionIndex = txIndex,
                GasUsed = gasUsed,
                ContractAddress = contractAddress,
                EffectiveGasPrice = effectiveGasPrice
            };

            var set = _txHashesByBlockHash.GetOrAdd(blockHashHex, _ => new ConcurrentDictionary<string, byte>());
            set.TryAdd(txHashHex, 0);

            _blockHashByNumber[blockNumber] = blockHashHex;

            return Task.CompletedTask;
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (!_blockHashByNumber.TryRemove(blockNumber, out var blockHashHex))
                return Task.CompletedTask;

            if (_txHashesByBlockHash.TryRemove(blockHashHex, out var txHashes))
            {
                foreach (var txHashHex in txHashes.Keys)
                {
                    _receiptByTxHash.TryRemove(txHashHex, out _);
                }
            }

            return Task.CompletedTask;
        }

        public void Clear()
        {
            _receiptByTxHash.Clear();
            _txHashesByBlockHash.Clear();
            _blockHashByNumber.Clear();
        }

        private static string ToHex(byte[] bytes) => bytes?.ToHex();

        private class StoredReceipt
        {
            public Receipt Receipt { get; set; }
            public byte[] BlockHash { get; set; }
            public BigInteger BlockNumber { get; set; }
            public int TransactionIndex { get; set; }
            public BigInteger GasUsed { get; set; }
            public string ContractAddress { get; set; }
            public BigInteger EffectiveGasPrice { get; set; }
        }
    }
}
