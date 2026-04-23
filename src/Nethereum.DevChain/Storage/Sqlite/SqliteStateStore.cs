using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteStateStore : IStateStore
    {
        private readonly SqliteStorageManager _manager;
        private readonly IAccountLayoutStrategy _accountLayout;
        private readonly object _lock = new object();
        private int _nextSnapshotId = 0;
        private readonly Dictionary<int, SqliteStateSnapshot> _activeSnapshots = new Dictionary<int, SqliteStateSnapshot>();
        private readonly HashSet<string> _dirtyAccounts = new HashSet<string>();
        private readonly Dictionary<string, HashSet<BigInteger>> _dirtyStorageSlots = new Dictionary<string, HashSet<BigInteger>>();

        public SqliteStateStore(SqliteStorageManager manager, IAccountLayoutStrategy accountLayout = null)
        {
            _manager = manager;
            _accountLayout = accountLayout ?? RlpAccountLayout.Instance;
        }

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        public Task<Account> GetAccountAsync(string address)
        {
            var normalized = NormalizeAddress(address);
            return Task.FromResult(GetAccountInternal(normalized));
        }

        private Account GetAccountInternal(string normalizedAddress)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = _accountLayout.HasExternalCodeHash
                ? "SELECT account_data, code_hash FROM accounts WHERE address = @addr"
                : "SELECT account_data FROM accounts WHERE address = @addr";
            cmd.Parameters.AddWithValue("@addr", normalizedAddress);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var data = reader["account_data"] as byte[];
            if (data == null) return null;

            var account = _accountLayout.DecodeAccount(data);
            if (account != null && _accountLayout.HasExternalCodeHash)
                account.CodeHash = reader["code_hash"] as byte[];

            return account;
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            var normalized = NormalizeAddress(address);
            var data = _accountLayout.EncodeAccount(account);

            using var cmd = _manager.Connection.CreateCommand();
            if (_accountLayout.HasExternalCodeHash)
            {
                cmd.CommandText = "INSERT OR REPLACE INTO accounts (address, account_data, code_hash) VALUES (@addr, @data, @ch)";
                cmd.Parameters.AddWithValue("@ch", (object)account.CodeHash ?? System.DBNull.Value);
            }
            else
            {
                cmd.CommandText = "INSERT OR REPLACE INTO accounts (address, account_data) VALUES (@addr, @data)";
            }
            cmd.Parameters.AddWithValue("@addr", normalized);
            cmd.Parameters.AddWithValue("@data", data);
            cmd.ExecuteNonQuery();

            lock (_lock)
            {
                _dirtyAccounts.Add(normalized);
            }

            return Task.CompletedTask;
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            var normalized = NormalizeAddress(address);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM accounts WHERE address = @addr LIMIT 1";
            cmd.Parameters.AddWithValue("@addr", normalized);

            return Task.FromResult(cmd.ExecuteScalar() != null);
        }

        public Task DeleteAccountAsync(string address)
        {
            var normalized = NormalizeAddress(address);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE address = @addr";
            cmd.Parameters.AddWithValue("@addr", normalized);
            cmd.ExecuteNonQuery();

            using var cmd2 = _manager.Connection.CreateCommand();
            cmd2.CommandText = "DELETE FROM account_storage WHERE address = @addr";
            cmd2.Parameters.AddWithValue("@addr", normalized);
            cmd2.ExecuteNonQuery();

            lock (_lock)
            {
                _dirtyAccounts.Add(normalized);
            }

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var result = new Dictionary<string, Account>();
            var hasExtCodeHash = _accountLayout.HasExternalCodeHash;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = hasExtCodeHash
                ? "SELECT address, account_data, code_hash FROM accounts"
                : "SELECT address, account_data FROM accounts";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var addr = reader.GetString(0);
                var data = (byte[])reader[1];
                var account = _accountLayout.DecodeAccount(data);
                if (account != null && hasExtCodeHash)
                    account.CodeHash = reader[2] as byte[];
                if (account != null)
                    result[addr] = account;
            }

            return Task.FromResult(result);
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var normalized = NormalizeAddress(address);
            var slotBytes = SlotToBytes(slot);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM account_storage WHERE address = @addr AND slot = @slot";
            cmd.Parameters.AddWithValue("@addr", normalized);
            cmd.Parameters.AddWithValue("@slot", slotBytes);

            var value = cmd.ExecuteScalar() as byte[];
            return Task.FromResult(value);
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            var normalized = NormalizeAddress(address);
            var slotBytes = SlotToBytes(slot);

            if (value == null || IsAllZero(value))
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "DELETE FROM account_storage WHERE address = @addr AND slot = @slot";
                cmd.Parameters.AddWithValue("@addr", normalized);
                cmd.Parameters.AddWithValue("@slot", slotBytes);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO account_storage (address, slot, value) VALUES (@addr, @slot, @val)";
                cmd.Parameters.AddWithValue("@addr", normalized);
                cmd.Parameters.AddWithValue("@slot", slotBytes);
                cmd.Parameters.AddWithValue("@val", value);
                cmd.ExecuteNonQuery();
            }

            lock (_lock)
            {
                _dirtyAccounts.Add(normalized);
                if (!_dirtyStorageSlots.TryGetValue(normalized, out var dirtySlots))
                {
                    dirtySlots = new HashSet<BigInteger>();
                    _dirtyStorageSlots[normalized] = dirtySlots;
                }
                dirtySlots.Add(slot);
            }

            return Task.CompletedTask;
        }

        public Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address)
        {
            var normalized = NormalizeAddress(address);
            var result = new Dictionary<BigInteger, byte[]>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT slot, value FROM account_storage WHERE address = @addr";
            cmd.Parameters.AddWithValue("@addr", normalized);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var slotBytesRaw = (byte[])reader[0];
                var value = (byte[])reader[1];
                var slot = new BigInteger(slotBytesRaw, isUnsigned: true, isBigEndian: true);
                result[slot] = value;
            }

            return Task.FromResult(result);
        }

        public Task ClearStorageAsync(string address)
        {
            var normalized = NormalizeAddress(address);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM account_storage WHERE address = @addr";
            cmd.Parameters.AddWithValue("@addr", normalized);
            cmd.ExecuteNonQuery();

            lock (_lock)
            {
                _dirtyAccounts.Add(normalized);
            }

            return Task.CompletedTask;
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            if (codeHash == null) return Task.FromResult<byte[]>(null);

            var hashHex = codeHash.ToHex();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT code FROM code WHERE code_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", hashHex);

            var code = cmd.ExecuteScalar() as byte[];
            return Task.FromResult(code);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            if (codeHash == null) return Task.CompletedTask;

            var hashHex = codeHash.ToHex();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO code (code_hash, code) VALUES (@hash, @code)";
            cmd.Parameters.AddWithValue("@hash", hashHex);
            cmd.Parameters.AddWithValue("@code", (object)code ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            lock (_lock)
            {
                var snapshotId = Interlocked.Increment(ref _nextSnapshotId);

                var dirtyAccountsCopy = new HashSet<string>(_dirtyAccounts);
                var dirtyStorageCopy = new Dictionary<string, HashSet<BigInteger>>();
                foreach (var kvp in _dirtyStorageSlots)
                {
                    dirtyStorageCopy[kvp.Key] = new HashSet<BigInteger>(kvp.Value);
                }

                var snapshot = new SqliteStateSnapshot(snapshotId, dirtyAccountsCopy, dirtyStorageCopy);
                _activeSnapshots[snapshotId] = snapshot;

                ExecuteSql($"SAVEPOINT {snapshot.SavepointName}");

                return Task.FromResult<IStateSnapshot>(snapshot);
            }
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is SqliteStateSnapshot sqliteSnapshot)
            {
                lock (_lock)
                {
                    ExecuteSql($"RELEASE SAVEPOINT {sqliteSnapshot.SavepointName}");
                    _activeSnapshots.Remove(sqliteSnapshot.SnapshotId);
                }
            }

            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is SqliteStateSnapshot sqliteSnapshot)
            {
                lock (_lock)
                {
                    ExecuteSql($"ROLLBACK TO SAVEPOINT {sqliteSnapshot.SavepointName}");
                    ExecuteSql($"RELEASE SAVEPOINT {sqliteSnapshot.SavepointName}");

                    _dirtyAccounts.Clear();
                    foreach (var addr in sqliteSnapshot.DirtyAccountsCopy)
                        _dirtyAccounts.Add(addr);

                    _dirtyStorageSlots.Clear();
                    foreach (var kvp in sqliteSnapshot.DirtyStorageSlotsCopy)
                        _dirtyStorageSlots[kvp.Key] = new HashSet<BigInteger>(kvp.Value);

                    _activeSnapshots.Remove(sqliteSnapshot.SnapshotId);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<string>>(_dirtyAccounts.ToList());
            }
        }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            lock (_lock)
            {
                var normalized = NormalizeAddress(address);
                if (!_dirtyStorageSlots.TryGetValue(normalized, out var dirtySlots))
                    return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(dirtySlots.ToList());
            }
        }

        public Task ClearDirtyTrackingAsync()
        {
            lock (_lock)
            {
                _dirtyAccounts.Clear();
                _dirtyStorageSlots.Clear();
            }
            return Task.CompletedTask;
        }

        private void ExecuteSql(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static byte[] SlotToBytes(BigInteger slot)
        {
            return slot.ToByteArray(isUnsigned: true, isBigEndian: true);
        }

        private static bool IsAllZero(byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) return false;
            }
            return true;
        }
    }
}
