using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryFilterStore : IFilterStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, FilterState> _filters = new Dictionary<string, FilterState>();
        private long _filterIdCounter = 0;
        private DateTime _lastCleanup = DateTime.UtcNow;

        public TimeSpan FilterTtl { get; set; } = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

        public string CreateLogFilter(LogFilter filter, BigInteger currentBlock)
        {
            lock (_lock)
            {
                EvictExpiredFiltersLocked();
                var filterId = GenerateFilterId();
                var state = new FilterState
                {
                    Id = filterId,
                    Type = FilterType.Log,
                    LogFilter = filter,
                    LastCheckedBlock = filter.FromBlock ?? currentBlock
                };
                _filters[filterId] = state;
                return filterId;
            }
        }

        public string CreateBlockFilter(BigInteger currentBlock)
        {
            lock (_lock)
            {
                EvictExpiredFiltersLocked();
                var filterId = GenerateFilterId();
                var state = new FilterState
                {
                    Id = filterId,
                    Type = FilterType.Block,
                    LastCheckedBlock = currentBlock
                };
                _filters[filterId] = state;
                return filterId;
            }
        }

        public string CreatePendingTransactionFilter()
        {
            lock (_lock)
            {
                EvictExpiredFiltersLocked();
                var filterId = GenerateFilterId();
                var state = new FilterState
                {
                    Id = filterId,
                    Type = FilterType.PendingTransaction,
                    LastCheckedBlock = BigInteger.Zero
                };
                _filters[filterId] = state;
                return filterId;
            }
        }

        public FilterState GetFilter(string filterId)
        {
            lock (_lock)
            {
                EvictExpiredFiltersLocked();

                if (_filters.TryGetValue(filterId, out var state))
                {
                    state.LastAccessedAt = DateTime.UtcNow;
                    return state;
                }
                return null;
            }
        }

        public bool RemoveFilter(string filterId)
        {
            lock (_lock)
            {
                return _filters.Remove(filterId);
            }
        }

        public void UpdateFilterLastBlock(string filterId, BigInteger blockNumber)
        {
            lock (_lock)
            {
                if (_filters.TryGetValue(filterId, out var state))
                {
                    state.LastCheckedBlock = blockNumber;
                    state.LastAccessedAt = DateTime.UtcNow;
                }
            }
        }

        private void EvictExpiredFiltersLocked()
        {
            var now = DateTime.UtcNow;
            if (now - _lastCleanup < CleanupInterval)
                return;

            _lastCleanup = now;
            var expired = _filters
                .Where(kvp => now - kvp.Value.LastAccessedAt > FilterTtl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _filters.Remove(key);
            }
        }

        private string GenerateFilterId()
        {
            _filterIdCounter++;
            return "0x" + _filterIdCounter.ToString("x");
        }
    }
}
