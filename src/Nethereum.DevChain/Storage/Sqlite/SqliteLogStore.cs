using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteLogStore : ILogStore
    {
        private readonly SqliteStorageManager _manager;
        private long _savepointCounter;

        public SqliteLogStore(SqliteStorageManager manager)
        {
            _manager = manager;
        }

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
        {
            if (logs == null || logs.Count == 0) return Task.CompletedTask;

            var txHashHex = txHash.ToHex();
            var blockHashHex = blockHash.ToHex();
            var blockNum = (long)blockNumber;

            var sp = NextSavepoint();
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    var filteredLog = FilteredLog.FromLog(log, blockHash, blockNumber, txHash, txIndex, i);
                    var logData = SerializeFilteredLog(filteredLog);

                    using var cmd = _manager.Connection.CreateCommand();
                    cmd.CommandText = @"INSERT INTO logs (log_data, address, block_hash, block_number, tx_hash, tx_index, log_index)
                                        VALUES (@logData, @address, @blockHash, @blockNumber, @txHash, @txIndex, @logIndex)";
                    cmd.Parameters.AddWithValue("@logData", logData);
                    cmd.Parameters.AddWithValue("@address", (object)log.Address?.ToLower() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@blockHash", blockHashHex);
                    cmd.Parameters.AddWithValue("@blockNumber", blockNum);
                    cmd.Parameters.AddWithValue("@txHash", txHashHex);
                    cmd.Parameters.AddWithValue("@txIndex", txIndex);
                    cmd.Parameters.AddWithValue("@logIndex", i);
                    cmd.ExecuteNonQuery();
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }

            return Task.CompletedTask;
        }

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
        {
            if (bloom == null || bloom.Length != 256) return Task.CompletedTask;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO block_blooms (block_number, bloom) VALUES (@num, @bloom)";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);
            cmd.Parameters.AddWithValue("@bloom", bloom);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var result = new List<FilteredLog>();

            var sb = new StringBuilder("SELECT log_data FROM logs WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            if (filter.FromBlock.HasValue)
            {
                sb.Append(" AND block_number >= @fromBlock");
                parameters.Add(new SqliteParameter("@fromBlock", (long)filter.FromBlock.Value));
            }

            if (filter.ToBlock.HasValue)
            {
                sb.Append(" AND block_number <= @toBlock");
                parameters.Add(new SqliteParameter("@toBlock", (long)filter.ToBlock.Value));
            }

            if (filter.Addresses != null && filter.Addresses.Count > 0)
            {
                if (filter.Addresses.Count == 1)
                {
                    sb.Append(" AND address = @addr0");
                    parameters.Add(new SqliteParameter("@addr0", filter.Addresses[0]?.ToLower()));
                }
                else
                {
                    var placeholders = new StringBuilder();
                    for (int i = 0; i < filter.Addresses.Count; i++)
                    {
                        if (i > 0) placeholders.Append(',');
                        var paramName = $"@addr{i}";
                        placeholders.Append(paramName);
                        parameters.Add(new SqliteParameter(paramName, filter.Addresses[i]?.ToLower()));
                    }
                    sb.Append($" AND address IN ({placeholders})");
                }
            }

            sb.Append(" ORDER BY block_number, tx_index, log_index");

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sb.ToString();
            foreach (var p in parameters) cmd.Parameters.Add(p);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["log_data"];
                var log = DeserializeFilteredLog(data);
                if (log != null && filter.MatchesTopics(log.Topics))
                {
                    result.Add(log);
                }
            }

            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            var result = new List<FilteredLog>();
            if (txHash == null) return Task.FromResult(result);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT log_data FROM logs WHERE tx_hash = @hash ORDER BY log_index";
            cmd.Parameters.AddWithValue("@hash", txHash.ToHex());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["log_data"];
                var log = DeserializeFilteredLog(data);
                if (log != null) result.Add(log);
            }

            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<FilteredLog>();
            if (blockHash == null) return Task.FromResult(result);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT log_data FROM logs WHERE block_hash = @hash ORDER BY tx_index, log_index";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["log_data"];
                var log = DeserializeFilteredLog(data);
                if (log != null) result.Add(log);
            }

            return Task.FromResult(result);
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
        {
            var result = new List<FilteredLog>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT log_data FROM logs WHERE block_number = @num ORDER BY tx_index, log_index";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["log_data"];
                var log = DeserializeFilteredLog(data);
                if (log != null) result.Add(log);
            }

            return Task.FromResult(result);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var sp = NextSavepoint();
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM logs WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@num", (long)blockNumber);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM block_blooms WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@num", (long)blockNumber);
                    cmd.ExecuteNonQuery();
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }

            return Task.CompletedTask;
        }

        private string NextSavepoint() => $"ls_{Interlocked.Increment(ref _savepointCounter)}";

        private void ExecuteSql(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static byte[] SerializeFilteredLog(FilteredLog log)
        {
            var encodedTopics = new List<byte[]>();
            foreach (var topic in log.Topics)
            {
                encodedTopics.Add(RLP.RLP.EncodeElement(topic));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(log.Address?.HexToByteArray() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(log.Data ?? Array.Empty<byte>()),
                RLP.RLP.EncodeList(encodedTopics.ToArray()),
                RLP.RLP.EncodeElement(log.BlockHash),
                RLP.RLP.EncodeElement(log.BlockNumber.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(log.TransactionHash),
                RLP.RLP.EncodeElement(log.TransactionIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(log.LogIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(new byte[] { log.Removed ? (byte)1 : (byte)0 })
            );
        }

        private static FilteredLog DeserializeFilteredLog(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLP.RLPCollection)decoded;

            var log = new FilteredLog
            {
                Address = elements[0].RLPData?.Length > 0 ? elements[0].RLPData.ToHex(true) : null,
                Data = elements[1].RLPData,
                BlockHash = elements[3].RLPData,
                BlockNumber = elements[4].RLPData.ToBigIntegerFromRLPDecoded(),
                TransactionHash = elements[5].RLPData,
                TransactionIndex = (int)elements[6].RLPData.ToLongFromRLPDecoded(),
                LogIndex = (int)elements[7].RLPData.ToLongFromRLPDecoded(),
                Removed = elements[8].RLPData?.Length > 0 && elements[8].RLPData[0] == 1
            };

            var topicsCollection = (RLP.RLPCollection)elements[2];
            log.Topics = new List<byte[]>();
            foreach (var topic in topicsCollection)
            {
                log.Topics.Add(topic.RLPData);
            }

            return log;
        }
    }
}
