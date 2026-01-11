using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryReceiptStore : IReceiptStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, StoredReceipt> _receiptByTxHash = new Dictionary<string, StoredReceipt>();
        private readonly Dictionary<string, List<string>> _txHashesByBlockHash = new Dictionary<string, List<string>>();
        private readonly Dictionary<BigInteger, string> _blockHashByNumber = new Dictionary<BigInteger, string>();

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(txHash);
                if (_receiptByTxHash.TryGetValue(hashHex, out var stored))
                    return Task.FromResult(stored.Receipt);
                return Task.FromResult<Receipt>(null);
            }
        }

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            lock (_lock)
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
        }

        public Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(blockHash);
                if (!_txHashesByBlockHash.TryGetValue(hashHex, out var txHashes))
                    return Task.FromResult(new List<Receipt>());

                var result = txHashes
                    .Select(h => _receiptByTxHash.TryGetValue(h, out var stored) ? stored : null)
                    .Where(s => s != null)
                    .OrderBy(s => s.TransactionIndex)
                    .Select(s => s.Receipt)
                    .ToList();

                return Task.FromResult(result);
            }
        }

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                if (!_blockHashByNumber.TryGetValue(blockNumber, out var blockHashHex))
                    return Task.FromResult(new List<Receipt>());

                return GetByBlockHashAsync(FromHex(blockHashHex));
            }
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            lock (_lock)
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

                if (!_txHashesByBlockHash.TryGetValue(blockHashHex, out var list))
                {
                    list = new List<string>();
                    _txHashesByBlockHash[blockHashHex] = list;
                }

                if (!list.Contains(txHashHex))
                    list.Add(txHashHex);

                _blockHashByNumber[blockNumber] = blockHashHex;
            }
            return Task.FromResult(0);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _receiptByTxHash.Clear();
                _txHashesByBlockHash.Clear();
                _blockHashByNumber.Clear();
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

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
