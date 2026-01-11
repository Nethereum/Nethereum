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

            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                var filteredLog = FilteredLog.FromLog(log, blockHash, blockNumber, txHash, txIndex, i);

                var logKey = CreateLogKey(blockNumber, txIndex, i);
                var logData = RocksDbSerializer.SerializeFilteredLog(filteredLog);
                batch.Put(logKey, logData, logsCf);

                var blockLogKey = CreateBlockLogKey(blockHash, txIndex, i);
                batch.Put(blockLogKey, logKey, logByBlockCf);

                if (!string.IsNullOrEmpty(log.Address))
                {
                    var addressLogKey = CreateAddressLogKey(log.Address, blockNumber, txIndex, i);
                    batch.Put(addressLogKey, logKey, logByAddressCf);
                }
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var result = new List<FilteredLog>();

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

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            var result = new List<FilteredLog>();
            if (txHash == null) return Task.FromResult(result);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_LOGS);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var data = iterator.Value();
                var log = RocksDbSerializer.DeserializeFilteredLog(data);

                if (log != null && ByteArrayEquals(log.TransactionHash, txHash))
                {
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
            var endBlockNumber = blockNumber + 1;

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

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
