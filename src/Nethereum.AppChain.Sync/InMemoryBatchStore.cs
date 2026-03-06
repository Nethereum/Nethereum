using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AppChain.Sync
{
    public class InMemoryBatchStore : IBatchStore
    {
        private readonly ConcurrentDictionary<string, BatchInfo> _batchesByRange = new();
        private readonly ConcurrentDictionary<string, BatchInfo> _batchesByHash = new();
        private BigInteger _latestImportedBlock = BigInteger.MinusOne;
        private readonly object _lock = new();

        public Task SaveBatchAsync(BatchInfo batch)
        {
            if (batch == null) return Task.CompletedTask;

            var rangeKey = GetRangeKey(batch.FromBlock, batch.ToBlock);
            var hashKey = GetHashKey(batch.BatchHash);

            _batchesByRange[rangeKey] = batch;
            _batchesByHash[hashKey] = batch;

            lock (_lock)
            {
                if (batch.Status == BatchStatus.Imported && batch.ToBlock > _latestImportedBlock)
                {
                    _latestImportedBlock = batch.ToBlock;
                }
            }

            return Task.CompletedTask;
        }

        public Task<BatchInfo?> GetBatchAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            var key = GetRangeKey(fromBlock, toBlock);
            _batchesByRange.TryGetValue(key, out var batch);
            return Task.FromResult<BatchInfo?>(batch);
        }

        public Task<BatchInfo?> GetBatchByHashAsync(byte[] batchHash)
        {
            if (batchHash == null) return Task.FromResult<BatchInfo?>(null);

            var key = GetHashKey(batchHash);
            _batchesByHash.TryGetValue(key, out var batch);
            return Task.FromResult<BatchInfo?>(batch);
        }

        public Task<BatchInfo?> GetBatchContainingBlockAsync(BigInteger blockNumber)
        {
            var batch = _batchesByRange.Values
                .FirstOrDefault(b => b.FromBlock <= blockNumber && b.ToBlock >= blockNumber);
            return Task.FromResult<BatchInfo?>(batch);
        }

        public Task<BatchInfo?> GetLatestBatchAsync()
        {
            var batch = _batchesByRange.Values
                .OrderByDescending(b => b.ToBlock)
                .FirstOrDefault();
            return Task.FromResult<BatchInfo?>(batch);
        }

        public Task<BigInteger> GetLatestImportedBlockAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_latestImportedBlock);
            }
        }

        public Task<IReadOnlyList<BatchInfo>> GetBatchesAfterAsync(BigInteger fromBlock, int limit = 100)
        {
            var batches = _batchesByRange.Values
                .Where(b => b.FromBlock >= fromBlock)
                .OrderBy(b => b.FromBlock)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<BatchInfo>>(batches);
        }

        public Task<IReadOnlyList<BatchInfo>> GetPendingBatchesAsync()
        {
            var batches = _batchesByRange.Values
                .Where(b => b.Status == BatchStatus.Pending)
                .OrderBy(b => b.FromBlock)
                .ToList();
            return Task.FromResult<IReadOnlyList<BatchInfo>>(batches);
        }

        public Task UpdateBatchStatusAsync(BigInteger fromBlock, BigInteger toBlock, BatchStatus status)
        {
            var key = GetRangeKey(fromBlock, toBlock);
            if (_batchesByRange.TryGetValue(key, out var batch))
            {
                batch.Status = status;

                if (status == BatchStatus.Imported)
                {
                    lock (_lock)
                    {
                        if (batch.ToBlock > _latestImportedBlock)
                        {
                            _latestImportedBlock = batch.ToBlock;
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task<bool> IsBatchImportedAsync(byte[] batchHash)
        {
            if (batchHash == null) return Task.FromResult(false);

            var key = GetHashKey(batchHash);
            if (_batchesByHash.TryGetValue(key, out var batch))
            {
                return Task.FromResult(batch.Status == BatchStatus.Imported);
            }
            return Task.FromResult(false);
        }

        private static string GetRangeKey(BigInteger fromBlock, BigInteger toBlock)
        {
            return $"{fromBlock}-{toBlock}";
        }

        private static string GetHashKey(byte[] hash) => hash?.ToHex() ?? string.Empty;
    }
}
