using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryLogStore : ILogStore
    {
        private int _nextLogIndex;
        private readonly ConcurrentDictionary<int, FilteredLog> _logs = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _logIndexesByTxHash = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _logIndexesByBlockHash = new();
        private readonly ConcurrentDictionary<BigInteger, ConcurrentDictionary<int, byte>> _logIndexesByBlockNumber = new();
        private readonly ConcurrentDictionary<BigInteger, byte[]> _bloomByBlockNumber = new();

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
        {
            if (logs == null || logs.Count == 0)
                return Task.CompletedTask;

            var txHashHex = ToHex(txHash);
            var blockHashHex = ToHex(blockHash);

            var txIndexes = _logIndexesByTxHash.GetOrAdd(txHashHex, _ => new ConcurrentDictionary<int, byte>());
            var blockHashIndexes = _logIndexesByBlockHash.GetOrAdd(blockHashHex, _ => new ConcurrentDictionary<int, byte>());
            var blockNumIndexes = _logIndexesByBlockNumber.GetOrAdd(blockNumber, _ => new ConcurrentDictionary<int, byte>());

            for (int i = 0; i < logs.Count; i++)
            {
                var filteredLog = FilteredLog.FromLog(logs[i], blockHash, blockNumber, txHash, txIndex, i);
                var index = Interlocked.Increment(ref _nextLogIndex) - 1;
                _logs[index] = filteredLog;

                txIndexes.TryAdd(index, 0);
                blockHashIndexes.TryAdd(index, 0);
                blockNumIndexes.TryAdd(index, 0);
            }

            return Task.CompletedTask;
        }

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256)
                return Task.CompletedTask;

            _bloomByBlockNumber[blockNumber] = bloom;
            return Task.CompletedTask;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var queryBloom = BuildQueryBloom(filter);
            var hasBloomFilter = queryBloom != null && !queryBloom.IsEmpty() && !_bloomByBlockNumber.IsEmpty;

            if (!hasBloomFilter)
            {
                var count = Volatile.Read(ref _nextLogIndex);
                var result = new List<FilteredLog>();
                for (int i = 0; i < count; i++)
                {
                    if (_logs.TryGetValue(i, out var log) && MatchesFilter(log, filter))
                        result.Add(log);
                }
                return Task.FromResult(result);
            }

            var matchingBlocks = new HashSet<BigInteger>();
            var fromBlock = filter.FromBlock ?? BigInteger.Zero;
            var toBlock = filter.ToBlock ?? GetMaxBlockNumber();

            foreach (var kvp in _bloomByBlockNumber)
            {
                if (kvp.Key >= fromBlock && kvp.Key <= toBlock)
                {
                    if (queryBloom.Matches(kvp.Value))
                    {
                        matchingBlocks.Add(kvp.Key);
                    }
                }
            }

            var resultLogs = new List<FilteredLog>();
            foreach (var blockNumber in matchingBlocks)
            {
                if (_logIndexesByBlockNumber.TryGetValue(blockNumber, out var indexes))
                {
                    foreach (var index in indexes.Keys)
                    {
                        if (_logs.TryGetValue(index, out var log) && MatchesAddressAndTopics(log, filter))
                        {
                            resultLogs.Add(log);
                        }
                    }
                }
            }

            return Task.FromResult(resultLogs);
        }

        private BigInteger GetMaxBlockNumber()
        {
            BigInteger max = BigInteger.Zero;
            foreach (var key in _logIndexesByBlockNumber.Keys)
            {
                if (key > max) max = key;
            }
            return max;
        }

        private LogBloomFilter BuildQueryBloom(LogFilter filter)
        {
            if (filter == null)
                return null;

            var hasAddresses = filter.Addresses != null && filter.Addresses.Count > 0;
            var hasTopics = filter.Topics != null && filter.Topics.Count > 0 &&
                            filter.Topics.Any(t => t != null && t.Count > 0);

            if (!hasAddresses && !hasTopics)
                return null;

            var bloom = new LogBloomFilter();

            if (hasAddresses)
            {
                foreach (var address in filter.Addresses)
                {
                    bloom.AddAddress(address);
                }
            }

            if (hasTopics)
            {
                for (int i = 0; i < filter.Topics.Count; i++)
                {
                    var topicFilter = filter.Topics[i];
                    if (topicFilter != null && topicFilter.Count > 0)
                    {
                        foreach (var topic in topicFilter)
                        {
                            bloom.AddTopic(topic);
                        }
                    }
                }
            }

            return bloom;
        }

        private bool MatchesAddressAndTopics(FilteredLog log, LogFilter filter)
        {
            if (!filter.MatchesAddress(log.Address))
                return false;

            if (!filter.MatchesTopics(log.Topics))
                return false;

            return true;
        }

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            var hashHex = ToHex(txHash);
            if (!_logIndexesByTxHash.TryGetValue(hashHex, out var indexes))
                return Task.FromResult(new List<FilteredLog>());

            var result = new List<FilteredLog>();
            foreach (var index in indexes.Keys)
            {
                if (_logs.TryGetValue(index, out var log))
                    result.Add(log);
            }
            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            var hashHex = ToHex(blockHash);
            if (!_logIndexesByBlockHash.TryGetValue(hashHex, out var indexes))
                return Task.FromResult(new List<FilteredLog>());

            var result = new List<FilteredLog>();
            foreach (var index in indexes.Keys)
            {
                if (_logs.TryGetValue(index, out var log))
                    result.Add(log);
            }
            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
        {
            if (!_logIndexesByBlockNumber.TryGetValue(blockNumber, out var indexes))
                return Task.FromResult(new List<FilteredLog>());

            var result = new List<FilteredLog>();
            foreach (var index in indexes.Keys)
            {
                if (_logs.TryGetValue(index, out var log))
                    result.Add(log);
            }
            return Task.FromResult(result);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (_logIndexesByBlockNumber.TryRemove(blockNumber, out var logIndexes))
            {
                foreach (var logIndex in logIndexes.Keys)
                {
                    if (_logs.TryRemove(logIndex, out var log))
                    {
                        var txHashHex = ToHex(log.TransactionHash);
                        if (txHashHex != null && _logIndexesByTxHash.TryGetValue(txHashHex, out var txIndexes))
                        {
                            txIndexes.TryRemove(logIndex, out _);
                        }

                        var blockHashHex = ToHex(log.BlockHash);
                        if (blockHashHex != null && _logIndexesByBlockHash.TryGetValue(blockHashHex, out var blockIndexes))
                        {
                            blockIndexes.TryRemove(logIndex, out _);
                        }
                    }
                }
            }

            _bloomByBlockNumber.TryRemove(blockNumber, out _);

            return Task.CompletedTask;
        }

        public void Clear()
        {
            _logs.Clear();
            _logIndexesByTxHash.Clear();
            _logIndexesByBlockHash.Clear();
            _logIndexesByBlockNumber.Clear();
            _bloomByBlockNumber.Clear();
            Interlocked.Exchange(ref _nextLogIndex, 0);
        }

        private bool MatchesFilter(FilteredLog log, LogFilter filter)
        {
            if (filter == null)
                return true;

            if (!filter.MatchesBlockRange(log.BlockNumber))
                return false;

            if (!filter.MatchesAddress(log.Address))
                return false;

            if (!filter.MatchesTopics(log.Topics))
                return false;

            return true;
        }

        private static string ToHex(byte[] bytes) => bytes?.ToHex();
    }
}
