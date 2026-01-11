using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryFilterStore : IFilterStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, FilterState> _filters = new Dictionary<string, FilterState>();
        private long _filterIdCounter = 0;

        public string CreateLogFilter(LogFilter filter, BigInteger currentBlock)
        {
            lock (_lock)
            {
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
                return _filters.TryGetValue(filterId, out var state) ? state : null;
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
                }
            }
        }

        private string GenerateFilterId()
        {
            _filterIdCounter++;
            return "0x" + _filterIdCounter.ToString("x");
        }
    }
}
