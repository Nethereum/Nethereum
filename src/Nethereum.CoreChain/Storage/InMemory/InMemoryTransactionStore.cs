using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryTransactionStore : ITransactionStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, StoredTransaction> _txByHash = new Dictionary<string, StoredTransaction>();
        private readonly Dictionary<string, List<string>> _txHashesByBlockHash = new Dictionary<string, List<string>>();
        private readonly IBlockStore _blockStore;

        public InMemoryTransactionStore(IBlockStore blockStore = null)
        {
            _blockStore = blockStore;
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(txHash);
                if (_txByHash.TryGetValue(hashHex, out var stored))
                    return Task.FromResult(stored.Transaction);
                return Task.FromResult<ISignedTransaction>(null);
            }
        }

        public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(blockHash);
                if (!_txHashesByBlockHash.TryGetValue(hashHex, out var txHashes))
                    return Task.FromResult(new List<ISignedTransaction>());

                var result = txHashes
                    .Select(h => _txByHash.TryGetValue(h, out var stored) ? stored : null)
                    .Where(s => s != null)
                    .OrderBy(s => s.TransactionIndex)
                    .Select(s => s.Transaction)
                    .ToList();

                return Task.FromResult(result);
            }
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

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex)
        {
            lock (_lock)
            {
                var txHashHex = ToHex(tx.Hash);
                var blockHashHex = ToHex(blockHash);

                _txByHash[txHashHex] = new StoredTransaction
                {
                    Transaction = tx,
                    BlockHash = blockHash,
                    TransactionIndex = txIndex
                };

                if (!_txHashesByBlockHash.TryGetValue(blockHashHex, out var list))
                {
                    list = new List<string>();
                    _txHashesByBlockHash[blockHashHex] = list;
                }

                if (!list.Contains(txHashHex))
                    list.Add(txHashHex);
            }
            return Task.FromResult(0);
        }

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(txHash);
                if (!_txByHash.TryGetValue(hashHex, out var stored))
                    return Task.FromResult<TransactionLocation>(null);

                return Task.FromResult(new TransactionLocation
                {
                    BlockHash = stored.BlockHash,
                    TransactionIndex = stored.TransactionIndex
                });
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _txByHash.Clear();
                _txHashesByBlockHash.Clear();
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private class StoredTransaction
        {
            public ISignedTransaction Transaction { get; set; }
            public byte[] BlockHash { get; set; }
            public int TransactionIndex { get; set; }
        }
    }
}
