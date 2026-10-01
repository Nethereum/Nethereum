using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbLogStore : ILogStore
    {
        private readonly RocksDbManager _manager;

        public RocksDbLogStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
        {
            if (logs == null || logs.Count == 0) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            var logsCf = _manager.GetColumnFamily(RocksDbManager.CF_LOGS);
            var logByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_BLOCK);
            var logByAddressCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_ADDRESS);
            var logByTxCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_TX);

            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                var filteredLog = FilteredLog.FromLog(log, blockHash, blockNumber, txHash, txIndex, i);

                var logKey = CreateLogKey(blockNumber, txIndex, i);
                var logData = RocksDbSerializer.SerializeFilteredLog(filteredLog);
                batch.Put(logKey, logData, logsCf);

                var blockLogKey = CreateBlockLogKey(blockHash, txIndex, i);
                batch.Put(blockLogKey, logKey, logByBlockCf);

                if (txHash != null)
                {
                    var txLogKey = CreateTxLogKey(txHash, i);
                    batch.Put(txLogKey, logKey, logByTxCf);
                }

                if (!string.IsNullOrEmpty(log.Address))
                {
                    var addressLogKey = CreateAddressLogKey(log.Address, blockNumber, txIndex, i);
                    batch.Put(addressLogKey, logKey, logByAddressCf);
                }
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task SaveManyLogsAsync(
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs,
            byte[] blockHash, BigInteger blockNumber)
        {
            if (txLogs == null || txLogs.Count == 0) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            StageManyLogsInto(batch, txLogs, blockHash, blockNumber);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public void StageManyLogsInto(
            RocksDbSharp.WriteBatch batch,
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs,
            byte[] blockHash, BigInteger blockNumber)
        {
            if (txLogs == null || txLogs.Count == 0) return;

            var logsCf = _manager.GetColumnFamily(RocksDbManager.CF_LOGS);
            var logByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_BLOCK);
            var logByAddressCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_ADDRESS);
            var logByTxCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_TX);

            foreach (var (logs, txHash, txIndex) in txLogs)
            {
                if (logs == null || logs.Count == 0) continue;
                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    var filteredLog = FilteredLog.FromLog(log, blockHash, blockNumber, txHash, txIndex, i);

                    var logKey = CreateLogKey(blockNumber, txIndex, i);
                    var logData = RocksDbSerializer.SerializeFilteredLog(filteredLog);
                    batch.Put(logKey, logData, logsCf);

                    var blockLogKey = CreateBlockLogKey(blockHash, txIndex, i);
                    batch.Put(blockLogKey, logKey, logByBlockCf);

                    if (txHash != null)
                    {
                        var txLogKey = CreateTxLogKey(txHash, i);
                        batch.Put(txLogKey, logKey, logByTxCf);
                    }

                    if (!string.IsNullOrEmpty(log.Address))
                    {
                        var addressLogKey = CreateAddressLogKey(log.Address, blockNumber, txIndex, i);
                        batch.Put(addressLogKey, logKey, logByAddressCf);
                    }
                }
            }
        }

        public static void CollectBulkWrites(
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs, byte[] blockHash, BigInteger blockNumber,
            List<(string Cf, byte[] Key, byte[] Value)> sequential,
            List<(string Cf, byte[] Key, byte[] Value)> indexes)
        {
            if (txLogs == null) return;
            foreach (var (logs, txHash, txIndex) in txLogs)
            {
                if (logs == null || logs.Count == 0) continue;
                for (int i = 0; i < logs.Count; i++)
                {
                    var filteredLog = FilteredLog.FromLog(logs[i], blockHash, blockNumber, txHash, txIndex, i);
                    var logKey = CreateLogKey(blockNumber, txIndex, i);
                    sequential.Add((RocksDbManager.CF_LOGS, logKey, RocksDbSerializer.SerializeFilteredLog(filteredLog)));
                    indexes.Add((RocksDbManager.CF_LOG_BY_BLOCK, CreateBlockLogKey(blockHash, txIndex, i), logKey));
                    if (txHash != null)
                        indexes.Add((RocksDbManager.CF_LOG_BY_TX, CreateTxLogKey(txHash, i), logKey));
                    if (!string.IsNullOrEmpty(logs[i].Address))
                        indexes.Add((RocksDbManager.CF_LOG_BY_ADDRESS, CreateAddressLogKey(logs[i].Address, blockNumber, txIndex, i), logKey));
                }
            }
        }

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256)
                return Task.CompletedTask;

            var key = CreateBlockNumberKey(blockNumber);
            _manager.Put(RocksDbManager.CF_BLOCK_BLOOMS, key, bloom);
            return Task.CompletedTask;
        }

        public void StageBlockBloomInto(RocksDbSharp.WriteBatch batch, BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256) return;
            var key = CreateBlockNumberKey(blockNumber);
            batch.Put(key, bloom, _manager.GetColumnFamily(RocksDbManager.CF_BLOCK_BLOOMS));
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var result = new List<FilteredLog>();
            var queryTerms = BuildQueryBloomTerms(filter);

            if (queryTerms != null)
            {
                foreach (var blockNumber in GetMatchingBlocks(filter, queryTerms))
                    FilterBlockInto(GetLogsByBlockNumberInternal(blockNumber), filter, result);
                return Task.FromResult(result);
            }

            StreamRangeByBlockInto(filter, result);
            return Task.FromResult(result);
        }

        private static void FilterBlockInto(List<FilteredLog> blockLogs, LogFilter filter, List<FilteredLog> into)
        {
            foreach (var log in blockLogs)
                if (filter.MatchesAddress(log.Address) && filter.MatchesTopics(log.Topics))
                    into.Add(log);
        }

        private void StreamRangeByBlockInto(LogFilter filter, List<FilteredLog> into)
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOGS);
            if (filter.FromBlock.HasValue) iterator.Seek(CreateLogKey(filter.FromBlock.Value, 0, 0));
            else iterator.SeekToFirst();

            var currentBlock = new List<FilteredLog>();
            var haveBlock = false;
            BigInteger blockNumber = BigInteger.Zero;

            while (iterator.Valid())
            {
                var log = RocksDbSerializer.DeserializeFilteredLog(iterator.Value());
                iterator.Next();
                if (log == null) continue;
                if (filter.ToBlock.HasValue && log.BlockNumber > filter.ToBlock.Value) break;

                if (haveBlock && log.BlockNumber != blockNumber)
                {
                    NumberAndFilterBlockInto(currentBlock, filter, into);
                    currentBlock = new List<FilteredLog>();
                }

                blockNumber = log.BlockNumber;
                haveBlock = true;
                currentBlock.Add(log);
            }

            NumberAndFilterBlockInto(currentBlock, filter, into);
        }

        private static void NumberAndFilterBlockInto(List<FilteredLog> blockLogs, LogFilter filter, List<FilteredLog> into)
        {
            if (blockLogs.Count == 0) return;
            BlockWideLogIndex.Assign(blockLogs);
            FilterBlockInto(blockLogs, filter, into);
        }

        private List<BigInteger> GetMatchingBlocks(LogFilter filter, QueryBloomTerms queryTerms)
        {
            var matchingBlocks = new List<BigInteger>();
            var fromBlock = filter.FromBlock ?? BigInteger.Zero;
            var toBlock = filter.ToBlock ?? BigInteger.Zero;

            if (toBlock == BigInteger.Zero)
            {
                var metaKey = System.Text.Encoding.UTF8.GetBytes("height");
                var metaData = _manager.Get(RocksDbManager.CF_METADATA, metaKey);
                if (metaData != null)
                {
                    toBlock = new BigInteger(metaData, isUnsigned: true, isBigEndian: true);
                }
            }

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_BLOCK_BLOOMS);
            var startKey = CreateBlockNumberKey(fromBlock);
            iterator.Seek(startKey);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                var blockNumber = new BigInteger(key, isUnsigned: true, isBigEndian: true);

                if (blockNumber > toBlock)
                    break;

                var blockBloom = iterator.Value();
                if (MatchesQueryBloomTerms(queryTerms, blockBloom))
                {
                    matchingBlocks.Add(blockNumber);
                }

                iterator.Next();
            }

            return matchingBlocks;
        }

        private List<FilteredLog> GetLogsByBlockNumberInternal(BigInteger blockNumber)
        {
            var result = new List<FilteredLog>();
            var startKey = CreateLogKey(blockNumber, 0, 0);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOGS);
            iterator.Seek(startKey);

            while (iterator.Valid())
            {
                var data = iterator.Value();
                var log = RocksDbSerializer.DeserializeFilteredLog(data);

                if (log != null)
                {
                    if (log.BlockNumber > blockNumber)
                        break;

                    if (log.BlockNumber == blockNumber)
                    {
                        result.Add(log);
                    }
                }

                iterator.Next();
            }

            BlockWideLogIndex.Assign(result);
            return result;
        }

        public sealed class QueryBloomTerms
        {
            public List<LogBloomFilter> AddressBlooms { get; } = new List<LogBloomFilter>();
            public List<List<LogBloomFilter>> TopicPositionBlooms { get; } = new List<List<LogBloomFilter>>();
        }

        public static QueryBloomTerms BuildQueryBloomTerms(LogFilter filter)
        {
            if (filter == null)
                return null;

            var hasAddresses = filter.Addresses != null && filter.Addresses.Count > 0;
            var hasTopics = filter.Topics != null && filter.Topics.Count > 0 &&
                            filter.Topics.Exists(t => t != null && t.Count > 0);

            if (!hasAddresses && !hasTopics)
                return null;

            var terms = new QueryBloomTerms();

            if (hasAddresses)
            {
                foreach (var address in filter.Addresses)
                {
                    var addressBloom = new LogBloomFilter();
                    addressBloom.AddAddress(address);
                    terms.AddressBlooms.Add(addressBloom);
                }
            }

            if (hasTopics)
            {
                for (int i = 0; i < filter.Topics.Count; i++)
                {
                    var topicFilter = filter.Topics[i];
                    var alternatives = new List<LogBloomFilter>();
                    if (topicFilter != null && topicFilter.Count > 0)
                    {
                        foreach (var topic in topicFilter)
                        {
                            var topicBloom = new LogBloomFilter();
                            topicBloom.AddTopic(topic);
                            alternatives.Add(topicBloom);
                        }
                    }
                    terms.TopicPositionBlooms.Add(alternatives);
                }
            }

            return terms;
        }

        public static bool MatchesQueryBloomTerms(QueryBloomTerms terms, byte[] blockBloom)
        {
            if (terms == null)
                return true;

            if (terms.AddressBlooms.Count > 0)
            {
                var anyAddressMatches = false;
                foreach (var addressBloom in terms.AddressBlooms)
                {
                    if (addressBloom.Matches(blockBloom))
                    {
                        anyAddressMatches = true;
                        break;
                    }
                }
                if (!anyAddressMatches)
                    return false;
            }

            foreach (var alternatives in terms.TopicPositionBlooms)
            {
                if (alternatives == null || alternatives.Count == 0)
                    continue;

                var anyTopicMatches = false;
                foreach (var topicBloom in alternatives)
                {
                    if (topicBloom.Matches(blockBloom))
                    {
                        anyTopicMatches = true;
                        break;
                    }
                }
                if (!anyTopicMatches)
                    return false;
            }

            return true;
        }

        internal static byte[] CreateBlockNumberKey(BigInteger blockNumber)
        {
            var blockBytes = blockNumber.ToByteArray(isUnsigned: true, isBigEndian: true);
            var paddedBlock = new byte[32];
            if (blockBytes.Length <= 32)
            {
                Buffer.BlockCopy(blockBytes, 0, paddedBlock, 32 - blockBytes.Length, blockBytes.Length);
            }
            return paddedBlock;
        }

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            var result = new List<FilteredLog>();
            if (txHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOG_BY_TX);
            iterator.Seek(txHash);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, txHash))
                    break;

                var logKey = iterator.Value();
                var logData = _manager.Get(RocksDbManager.CF_LOGS, logKey);
                if (logData != null)
                {
                    var log = RocksDbSerializer.DeserializeFilteredLog(logData);
                    if (log != null)
                        result.Add(log);
                }

                iterator.Next();
            }

            result.Sort((a, b) => a.LogIndex.CompareTo(b.LogIndex));
            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<FilteredLog>();
            if (blockHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOG_BY_BLOCK);
            iterator.Seek(blockHash);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, blockHash))
                    break;

                var logKey = iterator.Value();
                var logData = _manager.Get(RocksDbManager.CF_LOGS, logKey);
                if (logData != null)
                {
                    var log = RocksDbSerializer.DeserializeFilteredLog(logData);
                    if (log != null)
                    {
                        result.Add(log);
                    }
                }

                iterator.Next();
            }

            BlockWideLogIndex.Assign(result);
            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
            => Task.FromResult(GetLogsByBlockNumberInternal(blockNumber));

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            using var batch = _manager.CreateWriteBatch();
            var logsCf = _manager.GetColumnFamily(RocksDbManager.CF_LOGS);
            var bloomsCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCK_BLOOMS);
            var logByBlockCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_BLOCK);
            var logByAddressCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_ADDRESS);
            var logByTxCf = _manager.GetColumnFamily(RocksDbManager.CF_LOG_BY_TX);

            var startKey = CreateLogKey(blockNumber, 0, 0);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOGS);
            iterator.Seek(startKey);

            while (iterator.Valid())
            {
                var data = iterator.Value();
                var log = RocksDbSerializer.DeserializeFilteredLog(data);

                if (log == null || log.BlockNumber > blockNumber)
                    break;

                if (log.BlockNumber == blockNumber)
                {
                    batch.Delete(iterator.Key(), logsCf);

                    if (log.BlockHash != null)
                    {
                        var blockLogKey = CreateBlockLogKey(log.BlockHash, log.TransactionIndex, log.LogIndex);
                        batch.Delete(blockLogKey, logByBlockCf);
                    }

                    if (log.TransactionHash != null)
                    {
                        var txLogKey = CreateTxLogKey(log.TransactionHash, log.LogIndex);
                        batch.Delete(txLogKey, logByTxCf);
                    }

                    if (!string.IsNullOrEmpty(log.Address))
                    {
                        var addressLogKey = CreateAddressLogKey(log.Address, blockNumber, log.TransactionIndex, log.LogIndex);
                        batch.Delete(addressLogKey, logByAddressCf);
                    }
                }

                iterator.Next();
            }

            var bloomKey = CreateBlockNumberKey(blockNumber);
            batch.Delete(bloomKey, bloomsCf);

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private static byte[] CreateLogKey(BigInteger blockNumber, int txIndex, int logIndex)
        {
            var blockBytes = blockNumber.ToByteArray(isUnsigned: true, isBigEndian: true);
            var paddedBlock = new byte[32];
            if (blockBytes.Length <= 32)
            {
                Buffer.BlockCopy(blockBytes, 0, paddedBlock, 32 - blockBytes.Length, blockBytes.Length);
            }

            var txBytes = BitConverter.GetBytes(txIndex);
            var logBytes = BitConverter.GetBytes(logIndex);

            var key = new byte[paddedBlock.Length + txBytes.Length + logBytes.Length];
            Buffer.BlockCopy(paddedBlock, 0, key, 0, paddedBlock.Length);
            Buffer.BlockCopy(txBytes, 0, key, paddedBlock.Length, txBytes.Length);
            Buffer.BlockCopy(logBytes, 0, key, paddedBlock.Length + txBytes.Length, logBytes.Length);

            return key;
        }

        private static byte[] CreateBlockLogKey(byte[] blockHash, int txIndex, int logIndex)
        {
            var txBytes = BitConverter.GetBytes(txIndex);
            var logBytes = BitConverter.GetBytes(logIndex);

            var key = new byte[blockHash.Length + txBytes.Length + logBytes.Length];
            Buffer.BlockCopy(blockHash, 0, key, 0, blockHash.Length);
            Buffer.BlockCopy(txBytes, 0, key, blockHash.Length, txBytes.Length);
            Buffer.BlockCopy(logBytes, 0, key, blockHash.Length + txBytes.Length, logBytes.Length);

            return key;
        }

        private static byte[] CreateAddressLogKey(string address, BigInteger blockNumber, int txIndex, int logIndex)
        {
            var addressBytes = address.HexToByteArray();
            var logKey = CreateLogKey(blockNumber, txIndex, logIndex);

            var key = new byte[addressBytes.Length + logKey.Length];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(logKey, 0, key, addressBytes.Length, logKey.Length);

            return key;
        }

        private static byte[] CreateTxLogKey(byte[] txHash, int logIndex)
        {
            var logBytes = BitConverter.GetBytes(logIndex);
            var key = new byte[txHash.Length + logBytes.Length];
            Buffer.BlockCopy(txHash, 0, key, 0, txHash.Length);
            Buffer.BlockCopy(logBytes, 0, key, txHash.Length, logBytes.Length);
            return key;
        }

    }
}
