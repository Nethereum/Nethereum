using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryTransactionStore : ITransactionStore
    {
        private readonly ConcurrentDictionary<string, StoredTransaction> _txByHash = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _txHashesByBlockHash = new();
        private readonly IBlockStore _blockStore;

        public InMemoryTransactionStore(IBlockStore blockStore = null)
        {
            _blockStore = blockStore;
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            var hashHex = ToHex(txHash);
            if (_txByHash.TryGetValue(hashHex, out var stored))
                return Task.FromResult(stored.Transaction);
            return Task.FromResult<ISignedTransaction>(null);
        }

        public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            var hashHex = ToHex(blockHash);
            if (!_txHashesByBlockHash.TryGetValue(hashHex, out var txHashes))
                return Task.FromResult(new List<ISignedTransaction>());

            var result = txHashes.Keys
                .Select(h => _txByHash.TryGetValue(h, out var stored) ? stored : null)
                .Where(s => s != null)
                .OrderBy(s => s.TransactionIndex)
                .Select(s => s.Transaction)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            var hashHex = ToHex(blockHash);
            if (!_txHashesByBlockHash.TryGetValue(hashHex, out var txHashes))
                return Task.FromResult(new List<byte[]>());

            var result = txHashes.Keys
                .Select(h => _txByHash.TryGetValue(h, out var stored) ? stored : null)
                .Where(s => s != null)
                .OrderBy(s => s.TransactionIndex)
                .Select(s => s.Transaction.Hash)
                .ToList();

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
            var txHashHex = ToHex(tx.Hash);
            var blockHashHex = ToHex(blockHash);

            _txByHash[txHashHex] = new StoredTransaction
            {
                Transaction = tx,
                BlockHash = blockHash,
                TransactionIndex = txIndex,
                BlockNumber = blockNumber
            };

            var set = _txHashesByBlockHash.GetOrAdd(blockHashHex, _ => new ConcurrentDictionary<string, byte>());
            set.TryAdd(txHashHex, 0);

            return Task.CompletedTask;
        }

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            var hashHex = ToHex(txHash);
            if (!_txByHash.TryGetValue(hashHex, out var stored))
                return Task.FromResult<TransactionLocation>(null);

            return Task.FromResult(new TransactionLocation
            {
                BlockHash = stored.BlockHash,
                TransactionIndex = stored.TransactionIndex,
                BlockNumber = stored.BlockNumber
            });
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return;

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            if (blockHash == null) return;

            var blockHashHex = ToHex(blockHash);
            if (_txHashesByBlockHash.TryRemove(blockHashHex, out var txHashes))
            {
                foreach (var txHashHex in txHashes.Keys)
                {
                    _txByHash.TryRemove(txHashHex, out _);
                }
            }
        }

        public void Clear()
        {
            _txByHash.Clear();
            _txHashesByBlockHash.Clear();
        }

        private static string ToHex(byte[] bytes) => bytes?.ToHex();

        private class StoredTransaction
        {
            public ISignedTransaction Transaction { get; set; }
            public byte[] BlockHash { get; set; }
            public int TransactionIndex { get; set; }
            public BigInteger BlockNumber { get; set; }
        }
    }
}
