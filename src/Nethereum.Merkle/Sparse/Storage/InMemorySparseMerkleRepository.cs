using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Merkle.Sparse.Storage
{
    /// <summary>
    /// In-memory implementation of ISparseMerkleRepository
    /// Uses dictionaries for fast access, suitable for development and testing
    /// </summary>
    public class InMemorySparseMerkleRepository<T> : ISparseMerkleRepository<T>
    {
        private readonly Dictionary<string, T> _leaves = new Dictionary<string, T>();
        private readonly Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();
        private readonly object _lock = new object();

        public Task<T> GetLeafAsync(string key)
        {
            lock (_lock)
            {
                _leaves.TryGetValue(key, out var value);
                return Task.FromResult(value);
            }
        }

        public Task SetLeafAsync(string key, T value)
        {
            lock (_lock)
            {
                if (value == null)
                {
                    _leaves.Remove(key);
                }
                else
                {
                    _leaves[key] = value;
                }
            }
            return Task.FromResult(0);
        }

        public Task RemoveLeafAsync(string key)
        {
            lock (_lock)
            {
                _leaves.Remove(key);
            }
            return Task.FromResult(0);
        }

        public Task<bool> LeafExistsAsync(string key)
        {
            lock (_lock)
            {
                return Task.FromResult(_leaves.ContainsKey(key));
            }
        }

        public Task<long> GetLeafCountAsync()
        {
            lock (_lock)
            {
                return Task.FromResult((long)_leaves.Count);
            }
        }

        public Task<byte[]> GetCachedNodeAsync(string key)
        {
            lock (_lock)
            {
                _cache.TryGetValue(key, out var value);
                return Task.FromResult(value);
            }
        }

        public Task SetCachedNodeAsync(string key, byte[] hash)
        {
            lock (_lock)
            {
                if (hash == null)
                {
                    _cache.Remove(key);
                }
                else
                {
                    _cache[key] = hash;
                }
            }
            return Task.FromResult(0);
        }

        public Task RemoveCachedNodeAsync(string key)
        {
            lock (_lock)
            {
                _cache.Remove(key);
            }
            return Task.FromResult(0);
        }

        public Task SetLeavesBatchAsync(Dictionary<string, T> leaves)
        {
            if (leaves == null || leaves.Count == 0)
                return Task.FromResult(0);

            lock (_lock)
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
            return Task.FromResult(0);
        }

        public Task RemoveLeavesBatchAsync(IEnumerable<string> keys)
        {
            if (keys == null)
                return Task.FromResult(0);

            lock (_lock)
            {
                foreach (var key in keys)
                {
                    _leaves.Remove(key);
                }
            }
            return Task.FromResult(0);
        }

        public Task SetCachedNodesBatchAsync(Dictionary<string, byte[]> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return Task.FromResult(0);

            lock (_lock)
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
            return Task.FromResult(0);
        }

        public Task RemoveCachedNodesBatchAsync(IEnumerable<string> keys)
        {
            if (keys == null)
                return Task.FromResult(0);

            lock (_lock)
            {
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                }
            }
            return Task.FromResult(0);
        }

        public Task ClearAllLeavesAsync()
        {
            lock (_lock)
            {
                _leaves.Clear();
            }
            return Task.FromResult(0);
        }

        public Task ClearAllCacheAsync()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
            return Task.FromResult(0);
        }

        public Task<IEnumerable<string>> GetLeafKeysAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_leaves.Keys.ToList().AsEnumerable());
            }
        }

        public Task<IEnumerable<string>> GetLeafKeysWithPrefixAsync(string prefix)
        {
            lock (_lock)
            {
                var keysWithPrefix = _leaves.Keys.Where(key => key.StartsWith(prefix)).ToList();
                return Task.FromResult(keysWithPrefix.AsEnumerable());
            }
        }

        public Task<bool> HasAnyLeafWithKeyPrefixAsync(string keyPrefix)
        {
            lock (_lock)
            {
                var hasPrefix = _leaves.Keys.Any(key => key.StartsWith(keyPrefix));
                return Task.FromResult(hasPrefix);
            }
        }

        public Task<bool> IsOptimizedForLargeDatasets()
        {
            // In-memory storage is not optimized for large datasets
            return Task.FromResult(false);
        }
    }
}