using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteStateDiffStore : IStateDiffStore
    {
        private const string META_OLDEST_BLOCK = "oldest_block";
        private const string META_NEWEST_BLOCK = "newest_block";

        private readonly SqliteStorageManager _manager;
        private readonly object _lock = new();
        private bool _initialized;
        private long _savepointCounter;

        public SqliteStateDiffStore(SqliteStorageManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            EnsureTablesCreated();
        }

        private void EnsureTablesCreated()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;

                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS state_diff_accounts (
                        address TEXT NOT NULL,
                        block_number INTEGER NOT NULL,
                        account_data BLOB,
                        PRIMARY KEY (address, block_number)
                    );
                    CREATE INDEX IF NOT EXISTS idx_sda_block ON state_diff_accounts(block_number);

                    CREATE TABLE IF NOT EXISTS state_diff_storage (
                        address TEXT NOT NULL,
                        slot BLOB NOT NULL,
                        block_number INTEGER NOT NULL,
                        value BLOB,
                        PRIMARY KEY (address, slot, block_number)
                    );
                    CREATE INDEX IF NOT EXISTS idx_sds_block ON state_diff_storage(block_number);

                    CREATE TABLE IF NOT EXISTS state_diff_block_index (
                        block_number INTEGER PRIMARY KEY
                    );

                    CREATE TABLE IF NOT EXISTS state_diff_meta (
                        key TEXT PRIMARY KEY,
                        value INTEGER
                    );
                ";
                cmd.ExecuteNonQuery();
                _initialized = true;
            }
        }

        private string NextSavepoint()
        {
            var id = Interlocked.Increment(ref _savepointCounter);
            return $"sd_{id}";
        }

        private void ExecuteSql(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public Task SaveBlockDiffAsync(BlockStateDiff diff)
        {
            lock (_lock)
            {
                var sp = NextSavepoint();
                ExecuteSql($"SAVEPOINT {sp}");

                try
                {
                    foreach (var entry in diff.AccountDiffs)
                    {
                        var normalizedAddress = NormalizeAddress(entry.Address);
                        byte[] accountData = entry.PreValue != null
                            ? AccountEncoder.Current.Encode(entry.PreValue)
                            : null;

                        using var cmd = _manager.Connection.CreateCommand();
                        cmd.CommandText = "INSERT OR REPLACE INTO state_diff_accounts (address, block_number, account_data) VALUES (@addr, @block, @data)";
                        cmd.Parameters.AddWithValue("@addr", normalizedAddress);
                        cmd.Parameters.AddWithValue("@block", (long)diff.BlockNumber);
                        cmd.Parameters.AddWithValue("@data", (object)accountData ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var entry in diff.StorageDiffs)
                    {
                        var normalizedAddress = NormalizeAddress(entry.Address);
                        var slotBytes = SlotToBytes(entry.Slot);

                        using var cmd = _manager.Connection.CreateCommand();
                        cmd.CommandText = "INSERT OR REPLACE INTO state_diff_storage (address, slot, block_number, value) VALUES (@addr, @slot, @block, @val)";
                        cmd.Parameters.AddWithValue("@addr", normalizedAddress);
                        cmd.Parameters.AddWithValue("@slot", slotBytes);
                        cmd.Parameters.AddWithValue("@block", (long)diff.BlockNumber);
                        cmd.Parameters.AddWithValue("@val", (object)entry.PreValue ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    {
                        using var cmd = _manager.Connection.CreateCommand();
                        cmd.CommandText = "INSERT OR IGNORE INTO state_diff_block_index (block_number) VALUES (@block)";
                        cmd.Parameters.AddWithValue("@block", (long)diff.BlockNumber);
                        cmd.ExecuteNonQuery();
                    }

                    UpdateMetaBounds();
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                }
                catch
                {
                    ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);
            var searchFrom = blockNumber + 1;

            lock (_lock)
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "SELECT account_data FROM state_diff_accounts WHERE address = @addr AND block_number >= @block ORDER BY block_number ASC LIMIT 1";
                cmd.Parameters.AddWithValue("@addr", normalizedAddress);
                cmd.Parameters.AddWithValue("@block", (long)searchFrom);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    if (reader.IsDBNull(0))
                        return Task.FromResult((true, (Account)null));

                    var data = (byte[])reader[0];
                    var account = AccountEncoder.Current.Decode(data);
                    return Task.FromResult((true, account));
                }
            }

            return Task.FromResult((false, (Account)null));
        }

        public Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);
            var slotBytes = SlotToBytes(slot);
            var searchFrom = blockNumber + 1;

            lock (_lock)
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "SELECT value FROM state_diff_storage WHERE address = @addr AND slot = @slot AND block_number >= @block ORDER BY block_number ASC LIMIT 1";
                cmd.Parameters.AddWithValue("@addr", normalizedAddress);
                cmd.Parameters.AddWithValue("@slot", slotBytes);
                cmd.Parameters.AddWithValue("@block", (long)searchFrom);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    if (reader.IsDBNull(0))
                        return Task.FromResult((true, (byte[])null));

                    var value = (byte[])reader[0];
                    return Task.FromResult((true, value));
                }
            }

            return Task.FromResult((false, (byte[])null));
        }

        public Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                var sp = NextSavepoint();
                ExecuteSql($"SAVEPOINT {sp}");

                try
                {
                    ExecuteNonQuerySimple("DELETE FROM state_diff_accounts WHERE block_number > @block",
                        ("@block", (long)blockNumber));
                    ExecuteNonQuerySimple("DELETE FROM state_diff_storage WHERE block_number > @block",
                        ("@block", (long)blockNumber));
                    ExecuteNonQuerySimple("DELETE FROM state_diff_block_index WHERE block_number > @block",
                        ("@block", (long)blockNumber));

                    UpdateMetaBounds();
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                }
                catch
                {
                    ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                var sp = NextSavepoint();
                ExecuteSql($"SAVEPOINT {sp}");

                try
                {
                    ExecuteNonQuerySimple("DELETE FROM state_diff_accounts WHERE block_number < @block",
                        ("@block", (long)blockNumber));
                    ExecuteNonQuerySimple("DELETE FROM state_diff_storage WHERE block_number < @block",
                        ("@block", (long)blockNumber));
                    ExecuteNonQuerySimple("DELETE FROM state_diff_block_index WHERE block_number < @block",
                        ("@block", (long)blockNumber));

                    UpdateMetaBounds();
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                }
                catch
                {
                    ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public Task<BigInteger?> GetOldestDiffBlockAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(GetMetaValue(META_OLDEST_BLOCK));
            }
        }

        public Task<BigInteger?> GetNewestDiffBlockAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(GetMetaValue(META_NEWEST_BLOCK));
            }
        }

        private void UpdateMetaBounds()
        {
            var oldest = ExecuteScalarLong("SELECT MIN(block_number) FROM state_diff_block_index");
            var newest = ExecuteScalarLong("SELECT MAX(block_number) FROM state_diff_block_index");

            SetMetaValue(META_OLDEST_BLOCK, oldest);
            SetMetaValue(META_NEWEST_BLOCK, newest);
        }

        private BigInteger? GetMetaValue(string key)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM state_diff_meta WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);

            var result = cmd.ExecuteScalar();
            if (result == null || result is DBNull)
                return null;

            return (BigInteger)(long)result;
        }

        private void SetMetaValue(string key, long? value)
        {
            if (value.HasValue)
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO state_diff_meta (key, value) VALUES (@key, @val)";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@val", value.Value);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "DELETE FROM state_diff_meta WHERE key = @key";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
            }
        }

        private long? ExecuteScalarLong(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;

            var result = cmd.ExecuteScalar();
            if (result == null || result is DBNull)
                return null;

            return (long)result;
        }

        private void ExecuteNonQuerySimple(string sql, params (string Name, object Value)[] parameters)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);
            cmd.ExecuteNonQuery();
        }

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        private static byte[] SlotToBytes(BigInteger slot)
        {
            return slot.ToByteArray(isUnsigned: true, isBigEndian: true);
        }
    }
}
