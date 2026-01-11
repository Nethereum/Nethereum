using System;
using System.Numerics;
using System.Text;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbFilterStore : IFilterStore
    {
        private readonly RocksDbManager _manager;
        private readonly object _lock = new object();
        private int _nextFilterId = 0;

        public RocksDbFilterStore(RocksDbManager manager)
        {
            _manager = manager;
        }

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
                    LastCheckedBlock = currentBlock,
                    CreatedAt = DateTime.UtcNow
                };

                SaveFilterState(filterId, state);
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
                    LastCheckedBlock = currentBlock,
                    CreatedAt = DateTime.UtcNow
                };

                SaveFilterState(filterId, state);
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
                    LastCheckedBlock = 0,
                    CreatedAt = DateTime.UtcNow
                };

                SaveFilterState(filterId, state);
                return filterId;
            }
        }

        public FilterState GetFilter(string filterId)
        {
            if (string.IsNullOrEmpty(filterId)) return null;

            var key = Encoding.UTF8.GetBytes(filterId);
            var data = _manager.Get(RocksDbManager.CF_FILTERS, key);
            return RocksDbSerializer.DeserializeFilterState(data);
        }

        public bool RemoveFilter(string filterId)
        {
            if (string.IsNullOrEmpty(filterId)) return false;

            var key = Encoding.UTF8.GetBytes(filterId);
            if (!_manager.KeyExists(RocksDbManager.CF_FILTERS, key))
                return false;

            _manager.Delete(RocksDbManager.CF_FILTERS, key);
            return true;
        }

        public void UpdateFilterLastBlock(string filterId, BigInteger blockNumber)
        {
            if (string.IsNullOrEmpty(filterId)) return;

            var state = GetFilter(filterId);
            if (state == null) return;

            state.LastCheckedBlock = blockNumber;
            SaveFilterState(filterId, state);
        }

        private void SaveFilterState(string filterId, FilterState state)
        {
            var key = Encoding.UTF8.GetBytes(filterId);
            var data = RocksDbSerializer.SerializeFilterState(state);
            _manager.Put(RocksDbManager.CF_FILTERS, key, data);
        }

        private string GenerateFilterId()
        {
            var id = _nextFilterId++;
            return $"0x{id:x}";
        }
    }
}
