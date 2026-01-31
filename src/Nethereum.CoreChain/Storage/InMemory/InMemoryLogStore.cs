using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryLogStore : ILogStore
    {
        private readonly object _lock = new object();
        private readonly List<FilteredLog> _logs = new List<FilteredLog>();
        private readonly Dictionary<string, List<int>> _logIndexesByTxHash = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, List<int>> _logIndexesByBlockHash = new Dictionary<string, List<int>>();
        private readonly Dictionary<BigInteger, List<int>> _logIndexesByBlockNumber = new Dictionary<BigInteger, List<int>>();
        private readonly Dictionary<BigInteger, byte[]> _bloomByBlockNumber = new Dictionary<BigInteger, byte[]>();

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
        {
            if (logs == null || logs.Count == 0)
                return Task.FromResult(0);

            lock (_lock)
            {
                var txHashHex = ToHex(txHash);
                var blockHashHex = ToHex(blockHash);

                if (!_logIndexesByTxHash.ContainsKey(txHashHex))
                    _logIndexesByTxHash[txHashHex] = new List<int>();

                if (!_logIndexesByBlockHash.ContainsKey(blockHashHex))
                    _logIndexesByBlockHash[blockHashHex] = new List<int>();

                if (!_logIndexesByBlockNumber.ContainsKey(blockNumber))
                    _logIndexesByBlockNumber[blockNumber] = new List<int>();

                for (int i = 0; i < logs.Count; i++)
                {
                    var filteredLog = FilteredLog.FromLog(logs[i], blockHash, blockNumber, txHash, txIndex, i);
                    var index = _logs.Count;
                    _logs.Add(filteredLog);

                    _logIndexesByTxHash[txHashHex].Add(index);
                    _logIndexesByBlockHash[blockHashHex].Add(index);
                    _logIndexesByBlockNumber[blockNumber].Add(index);
                }
            }
            return Task.FromResult(0);
        }

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256)
                return Task.CompletedTask;

            lock (_lock)
            {
                _bloomByBlockNumber[blockNumber] = bloom;
            }
            return Task.CompletedTask;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            lock (_lock)
            {
                var queryBloom = BuildQueryBloom(filter);
                var hasBloomFilter = queryBloom != null && !queryBloom.IsEmpty();

                if (!hasBloomFilter)
                {
                    var result = _logs
                        .Where(log => MatchesFilter(log, filter))
                        .ToList();
                    return Task.FromResult(result);
                }

                var matchingBlocks = new HashSet<BigInteger>();
                var fromBlock = filter.FromBlock ?? BigInteger.Zero;
                var toBlock = filter.ToBlock ?? _logIndexesByBlockNumber.Keys.DefaultIfEmpty(BigInteger.Zero).Max();

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
                        foreach (var index in indexes)
                        {
                            var log = _logs[index];
                            if (MatchesAddressAndTopics(log, filter))
                            {
                                resultLogs.Add(log);
                            }
                        }
                    }
                }

                return Task.FromResult(resultLogs);
            }
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
            lock (_lock)
            {
                var hashHex = ToHex(txHash);
                if (!_logIndexesByTxHash.TryGetValue(hashHex, out var indexes))
                    return Task.FromResult(new List<FilteredLog>());

                var result = indexes.Select(i => _logs[i]).ToList();
                return Task.FromResult(result);
            }
        }

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(blockHash);
                if (!_logIndexesByBlockHash.TryGetValue(hashHex, out var indexes))
                    return Task.FromResult(new List<FilteredLog>());

                var result = indexes.Select(i => _logs[i]).ToList();
                return Task.FromResult(result);
            }
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                if (!_logIndexesByBlockNumber.TryGetValue(blockNumber, out var indexes))
                    return Task.FromResult(new List<FilteredLog>());

                var result = indexes.Select(i => _logs[i]).ToList();
                return Task.FromResult(result);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _logs.Clear();
                _logIndexesByTxHash.Clear();
                _logIndexesByBlockHash.Clear();
                _logIndexesByBlockNumber.Clear();
                _bloomByBlockNumber.Clear();
            }
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

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
