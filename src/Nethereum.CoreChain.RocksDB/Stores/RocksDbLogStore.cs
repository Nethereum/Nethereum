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

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256)
                return Task.CompletedTask;

            var key = CreateBlockNumberKey(blockNumber);
            _manager.Put(RocksDbManager.CF_BLOCK_BLOOMS, key, bloom);
            return Task.CompletedTask;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var result = new List<FilteredLog>();

            var hasSingleAddress = filter.Addresses != null && filter.Addresses.Count == 1;
            var hasTopics = filter.Topics != null && filter.Topics.Count > 0 &&
                            filter.Topics.Exists(t => t != null && t.Count > 0);

            if (hasSingleAddress)
            {
                var logs = GetLogsByAddressInternal(filter.Addresses[0], filter.FromBlock, filter.ToBlock);
                foreach (var log in logs)
                {
                    if (filter.MatchesTopics(log.Topics))
                    {
                        result.Add(log);
                    }
                }
                return Task.FromResult(result);
            }

            var queryBloom = BuildQueryBloom(filter);
            var hasBloomFilter = queryBloom != null && !queryBloom.IsEmpty();

            if (hasBloomFilter)
            {
                var matchingBlocks = GetMatchingBlocks(filter, queryBloom);
                foreach (var blockNumber in matchingBlocks)
                {
                    var blockLogs = GetLogsByBlockNumberInternal(blockNumber);
                    foreach (var log in blockLogs)
                    {
                        if (filter.MatchesAddress(log.Address) &&
                            filter.MatchesTopics(log.Topics))
                        {
                            result.Add(log);
                        }
                    }
                }
                return Task.FromResult(result);
            }

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOGS);

            if (filter.FromBlock.HasValue)
            {
                var startKey = CreateLogKey(filter.FromBlock.Value, 0, 0);
                iterator.Seek(startKey);
            }
            else
            {
                iterator.SeekToFirst();
            }

            while (iterator.Valid())
            {
                var data = iterator.Value();
                var log = RocksDbSerializer.DeserializeFilteredLog(data);

                if (log != null)
                {
                    if (filter.ToBlock.HasValue && log.BlockNumber > filter.ToBlock.Value)
                        break;

                    if (filter.MatchesBlockRange(log.BlockNumber) &&
                        filter.MatchesAddress(log.Address) &&
                        filter.MatchesTopics(log.Topics))
                    {
                        result.Add(log);
                    }
                }

                iterator.Next();
            }

            return Task.FromResult(result);
        }

        private List<FilteredLog> GetLogsByAddressInternal(string address, BigInteger? fromBlock, BigInteger? toBlock)
        {
            var result = new List<FilteredLog>();
            var addressBytes = address.HexToByteArray();

            var startBlockNumber = fromBlock ?? BigInteger.Zero;
            var startKey = CreateAddressLogKey(address, startBlockNumber, 0, 0);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOG_BY_ADDRESS);
            iterator.Seek(startKey);

            while (iterator.Valid())
            {
                var key = iterator.Key();

                if (!StartsWith(key, addressBytes))
                    break;

                var logKey = iterator.Value();
                var logData = _manager.Get(RocksDbManager.CF_LOGS, logKey);

                if (logData != null)
                {
                    var log = RocksDbSerializer.DeserializeFilteredLog(logData);
                    if (log != null)
                    {
                        if (toBlock.HasValue && log.BlockNumber > toBlock.Value)
                            break;

                        if (!fromBlock.HasValue || log.BlockNumber >= fromBlock.Value)
                        {
                            result.Add(log);
                        }
                    }
                }

                iterator.Next();
            }

            return result;
        }

        private List<BigInteger> GetMatchingBlocks(LogFilter filter, LogBloomFilter queryBloom)
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
                if (queryBloom.Matches(blockBloom))
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

            return result;
        }

        private static LogBloomFilter BuildQueryBloom(LogFilter filter)
        {
            if (filter == null)
                return null;

            var hasAddresses = filter.Addresses != null && filter.Addresses.Count > 0;
            var hasTopics = filter.Topics != null && filter.Topics.Count > 0 &&
                            filter.Topics.Exists(t => t != null && t.Count > 0);

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

        private static byte[] CreateBlockNumberKey(BigInteger blockNumber)
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
                if (!StartsWith(key, txHash))
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
                if (!StartsWith(key, blockHash))
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

            result.Sort((a, b) =>
            {
                var txCmp = a.TransactionIndex.CompareTo(b.TransactionIndex);
                return txCmp != 0 ? txCmp : a.LogIndex.CompareTo(b.LogIndex);
            });

            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
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

            result.Sort((a, b) =>
            {
                var txCmp = a.TransactionIndex.CompareTo(b.TransactionIndex);
                return txCmp != 0 ? txCmp : a.LogIndex.CompareTo(b.LogIndex);
            });

            return Task.FromResult(result);
        }

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

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || prefix == null) return false;
            if (data.Length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i]) return false;
            }
            return true;
        }

    }
}
