using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Merkle.Sparse.Storage;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    /// <summary>
    /// Mock implementation of ISparseMerkleRepository for testing
    /// Tracks all operations for verification and provides in-memory storage
    /// </summary>
    public class MockSparseMerkleRepository<T> : ISparseMerkleRepository<T>
    {
        private readonly Dictionary<string, T> _leaves = new Dictionary<string, T>();
        private readonly Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();

        // Operation tracking for test verification
        public List<string> OperationLog { get; } = new List<string>();
        public int GetLeafCallCount { get; private set; }
        public int SetLeafCallCount { get; private set; }
        public int RemoveLeafCallCount { get; private set; }
        public int LeafExistsCallCount { get; private set; }
        public int GetLeafCountCallCount { get; private set; }
        public int GetCachedNodeCallCount { get; private set; }
        public int SetCachedNodeCallCount { get; private set; }
        public int RemoveCachedNodeCallCount { get; private set; }
        public int SetLeavesBatchCallCount { get; private set; }
        public int RemoveLeavesBatchCallCount { get; private set; }
        public int SetCachedNodesBatchCallCount { get; private set; }
        public int RemoveCachedNodesBatchCallCount { get; private set; }
        public int ClearAllLeavesCallCount { get; private set; }
        public int ClearAllCacheCallCount { get; private set; }
        public int GetLeafKeysCallCount { get; private set; }
        public int GetLeafKeysWithPrefixCallCount { get; private set; }
        public int HasAnyLeafWithKeyPrefixCallCount { get; private set; }
        public int IsOptimizedForLargeDatasetsCallCount { get; private set; }

        // Properties for verification
        public Dictionary<string, T> Leaves => new Dictionary<string, T>(_leaves);
        public Dictionary<string, byte[]> Cache => new Dictionary<string, byte[]>(_cache);

        public Task<T> GetLeafAsync(string key)
        {
            GetLeafCallCount++;
            OperationLog.Add($"GetLeaf({key})");
            _leaves.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetLeafAsync(string key, T value)
        {
            SetLeafCallCount++;
            OperationLog.Add($"SetLeaf({key}, {value})");
            if (value == null)
            {
                _leaves.Remove(key);
            }
            else
            {
                _leaves[key] = value;
            }
            return Task.CompletedTask;
        }

        public Task RemoveLeafAsync(string key)
        {
            RemoveLeafCallCount++;
            OperationLog.Add($"RemoveLeaf({key})");
            _leaves.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> LeafExistsAsync(string key)
        {
            LeafExistsCallCount++;
            OperationLog.Add($"LeafExists({key})");
            return Task.FromResult(_leaves.ContainsKey(key));
        }

        public Task<long> GetLeafCountAsync()
        {
            GetLeafCountCallCount++;
            OperationLog.Add("GetLeafCount()");
            return Task.FromResult((long)_leaves.Count);
        }

        public Task<byte[]> GetCachedNodeAsync(string key)
        {
            GetCachedNodeCallCount++;
            OperationLog.Add($"GetCachedNode({key})");
            _cache.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetCachedNodeAsync(string key, byte[] hash)
        {
            SetCachedNodeCallCount++;
            OperationLog.Add($"SetCachedNode({key}, {hash?.Length ?? 0} bytes)");
            if (hash == null)
            {
                _cache.Remove(key);
            }
            else
            {
                _cache[key] = hash;
            }
            return Task.CompletedTask;
        }

        public Task RemoveCachedNodeAsync(string key)
        {
            RemoveCachedNodeCallCount++;
            OperationLog.Add($"RemoveCachedNode({key})");
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetLeavesBatchAsync(Dictionary<string, T> leaves)
        {
            SetLeavesBatchCallCount++;
            OperationLog.Add($"SetLeavesBatch({leaves?.Count ?? 0} items)");
            if (leaves != null)
            {
                foreach (var kvp in leaves)
                {
                    if (kvp.Value == null)
                    {
                        _leaves.Remove(kvp.Key);
                    }
                    else
                    {
                        _leaves[kvp.Key] = kvp.Value;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task RemoveLeavesBatchAsync(IEnumerable<string> keys)
        {
            RemoveLeavesBatchCallCount++;
            var keyList = keys?.ToList() ?? new List<string>();
            OperationLog.Add($"RemoveLeavesBatch({keyList.Count} items)");
            foreach (var key in keyList)
            {
                _leaves.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task SetCachedNodesBatchAsync(Dictionary<string, byte[]> nodes)
        {
            SetCachedNodesBatchCallCount++;
            OperationLog.Add($"SetCachedNodesBatch({nodes?.Count ?? 0} items)");
            if (nodes != null)
            {
                foreach (var kvp in nodes)
                {
                    if (kvp.Value == null)
                    {
                        _cache.Remove(kvp.Key);
                    }
                    else
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task RemoveCachedNodesBatchAsync(IEnumerable<string> keys)
        {
            RemoveCachedNodesBatchCallCount++;
            var keyList = keys?.ToList() ?? new List<string>();
            OperationLog.Add($"RemoveCachedNodesBatch({keyList.Count} items)");
            foreach (var key in keyList)
            {
                _cache.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task ClearAllLeavesAsync()
        {
            ClearAllLeavesCallCount++;
            OperationLog.Add("ClearAllLeaves()");
            _leaves.Clear();
            return Task.CompletedTask;
        }

        public Task ClearAllCacheAsync()
        {
            ClearAllCacheCallCount++;
            OperationLog.Add("ClearAllCache()");
            _cache.Clear();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetLeafKeysAsync()
        {
            GetLeafKeysCallCount++;
            OperationLog.Add("GetLeafKeys()");
            return Task.FromResult(_leaves.Keys.ToList().AsEnumerable());
        }

        public Task<IEnumerable<string>> GetLeafKeysWithPrefixAsync(string prefix)
        {
            GetLeafKeysWithPrefixCallCount++;
            OperationLog.Add($"GetLeafKeysWithPrefix({prefix})");
            var keysWithPrefix = _leaves.Keys.Where(key => key.StartsWith(prefix)).ToList();
            return Task.FromResult(keysWithPrefix.AsEnumerable());
        }

        public Task<bool> HasAnyLeafWithKeyPrefixAsync(string keyPrefix)
        {
            HasAnyLeafWithKeyPrefixCallCount++;
            OperationLog.Add($"HasAnyLeafWithKeyPrefix({keyPrefix})");
            var hasPrefix = _leaves.Keys.Any(key => key.StartsWith(keyPrefix));
            return Task.FromResult(hasPrefix);
        }

        public Task<bool> IsOptimizedForLargeDatasets()
        {
            IsOptimizedForLargeDatasetsCallCount++;
            OperationLog.Add("IsOptimizedForLargeDatasets()");
            return Task.FromResult(false);
        }

        // Helper methods for testing
        public void ClearOperationLog()
        {
            OperationLog.Clear();
        }

        public void ResetCallCounts()
        {
            GetLeafCallCount = 0;
            SetLeafCallCount = 0;
            RemoveLeafCallCount = 0;
            LeafExistsCallCount = 0;
            GetLeafCountCallCount = 0;
            GetCachedNodeCallCount = 0;
            SetCachedNodeCallCount = 0;
            RemoveCachedNodeCallCount = 0;
            SetLeavesBatchCallCount = 0;
            RemoveLeavesBatchCallCount = 0;
            SetCachedNodesBatchCallCount = 0;
            RemoveCachedNodesBatchCallCount = 0;
            ClearAllLeavesCallCount = 0;
            ClearAllCacheCallCount = 0;
            GetLeafKeysCallCount = 0;
            GetLeafKeysWithPrefixCallCount = 0;
            HasAnyLeafWithKeyPrefixCallCount = 0;
            IsOptimizedForLargeDatasetsCallCount = 0;
        }

        public void Reset()
        {
            _leaves.Clear();
            _cache.Clear();
            OperationLog.Clear();
            ResetCallCounts();
        }
    }
}