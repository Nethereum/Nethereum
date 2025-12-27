using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.Caching
{
    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly LinkedList<string> _accessOrder = new();
        private readonly Dictionary<string, LinkedListNode<string>> _nodeMap = new();
        private readonly object _lruLock = new object();
        private readonly int _maxSize;

        public MemoryCacheProvider(int maxSize = 10000)
        {
            _maxSize = maxSize;
        }

        public Task<T> GetAsync<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    TouchKey(key);
                    return Task.FromResult((T)entry.Value);
                }
                RemoveKey(key);
            }
            return Task.FromResult(default(T));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var entry = new CacheEntry
            {
                Value = value,
                ExpiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null
            };

            if (_cache.TryAdd(key, entry))
            {
                AddKey(key);
                EvictIfNeeded();
            }
            else
            {
                _cache[key] = entry;
                TouchKey(key);
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    TouchKey(key);
                    return Task.FromResult(true);
                }
                RemoveKey(key);
            }
            return Task.FromResult(false);
        }

        public Task RemoveAsync(string key)
        {
            RemoveKey(key);
            return Task.CompletedTask;
        }

        public void Clear()
        {
            lock (_lruLock)
            {
                _cache.Clear();
                _accessOrder.Clear();
                _nodeMap.Clear();
            }
        }

        public int Count => _cache.Count;

        private void TouchKey(string key)
        {
            lock (_lruLock)
            {
                if (_nodeMap.TryGetValue(key, out var node))
                {
                    _accessOrder.Remove(node);
                    _accessOrder.AddLast(node);
                }
            }
        }

        private void AddKey(string key)
        {
            lock (_lruLock)
            {
                if (!_nodeMap.ContainsKey(key))
                {
                    var node = _accessOrder.AddLast(key);
                    _nodeMap[key] = node;
                }
            }
        }

        private void RemoveKey(string key)
        {
            _cache.TryRemove(key, out _);
            lock (_lruLock)
            {
                if (_nodeMap.TryGetValue(key, out var node))
                {
                    _accessOrder.Remove(node);
                    _nodeMap.Remove(key);
                }
            }
        }

        private void EvictIfNeeded()
        {
            while (_cache.Count > _maxSize)
            {
                string keyToRemove = null;
                lock (_lruLock)
                {
                    if (_accessOrder.Count > 0)
                    {
                        keyToRemove = _accessOrder.First.Value;
                    }
                }

                if (keyToRemove != null)
                {
                    RemoveKey(keyToRemove);
                }
                else
                {
                    break;
                }
            }
        }

        private class CacheEntry
        {
            public object Value { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
        }
    }
}
