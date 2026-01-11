using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryStateStore : IStateStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>();
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _storage = new Dictionary<string, Dictionary<BigInteger, byte[]>>();
        private readonly Dictionary<string, byte[]> _code = new Dictionary<string, byte[]>();
        private int _nextSnapshotId = 0;

        public Task<Account> GetAccountAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                _accounts.TryGetValue(normalizedAddress, out var account);
                return Task.FromResult(account);
            }
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                _accounts[normalizedAddress] = account;
            }
            return Task.FromResult(0);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                return Task.FromResult(_accounts.ContainsKey(normalizedAddress));
            }
        }

        public Task DeleteAccountAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                _accounts.Remove(normalizedAddress);
                _storage.Remove(normalizedAddress);
            }
            return Task.FromResult(0);
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(new Dictionary<string, Account>(_accounts));
            }
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                    return Task.FromResult<byte[]>(null);

                accountStorage.TryGetValue(slot, out var value);
                return Task.FromResult(value);
            }
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                {
                    accountStorage = new Dictionary<BigInteger, byte[]>();
                    _storage[normalizedAddress] = accountStorage;
                }

                if (value == null || value.All(b => b == 0))
                    accountStorage.Remove(slot);
                else
                    accountStorage[slot] = value;
            }
            return Task.FromResult(0);
        }

        public Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                    return Task.FromResult(new Dictionary<BigInteger, byte[]>());

                return Task.FromResult(new Dictionary<BigInteger, byte[]>(accountStorage));
            }
        }

        public Task ClearStorageAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = NormalizeAddress(address);
                _storage.Remove(normalizedAddress);
            }
            return Task.FromResult(0);
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(codeHash);
                _code.TryGetValue(hashHex, out var code);
                return Task.FromResult(code);
            }
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            lock (_lock)
            {
                var hashHex = ToHex(codeHash);
                _code[hashHex] = code;
            }
            return Task.FromResult(0);
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            lock (_lock)
            {
                var snapshot = new InMemoryStateSnapshot(
                    _nextSnapshotId++,
                    CloneAccounts(),
                    CloneStorage(),
                    CloneCode()
                );
                return Task.FromResult<IStateSnapshot>(snapshot);
            }
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            return Task.FromResult(0);
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is InMemoryStateSnapshot memSnapshot)
            {
                lock (_lock)
                {
                    _accounts.Clear();
                    foreach (var kvp in memSnapshot.Accounts)
                        _accounts[kvp.Key] = kvp.Value;

                    _storage.Clear();
                    foreach (var kvp in memSnapshot.Storage)
                        _storage[kvp.Key] = kvp.Value;

                    _code.Clear();
                    foreach (var kvp in memSnapshot.Code)
                        _code[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult(0);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _accounts.Clear();
                _storage.Clear();
                _code.Clear();
            }
        }

        private Dictionary<string, Account> CloneAccounts()
        {
            var clone = new Dictionary<string, Account>();
            foreach (var kvp in _accounts)
            {
                clone[kvp.Key] = new Account
                {
                    Nonce = kvp.Value.Nonce,
                    Balance = kvp.Value.Balance,
                    StateRoot = kvp.Value.StateRoot?.ToArray(),
                    CodeHash = kvp.Value.CodeHash?.ToArray()
                };
            }
            return clone;
        }

        private Dictionary<string, Dictionary<BigInteger, byte[]>> CloneStorage()
        {
            var clone = new Dictionary<string, Dictionary<BigInteger, byte[]>>();
            foreach (var kvp in _storage)
            {
                clone[kvp.Key] = new Dictionary<BigInteger, byte[]>();
                foreach (var storageKvp in kvp.Value)
                {
                    clone[kvp.Key][storageKvp.Key] = storageKvp.Value?.ToArray();
                }
            }
            return clone;
        }

        private Dictionary<string, byte[]> CloneCode()
        {
            var clone = new Dictionary<string, byte[]>();
            foreach (var kvp in _code)
            {
                clone[kvp.Key] = kvp.Value?.ToArray();
            }
            return clone;
        }

        private static string NormalizeAddress(string address)
        {
            return address?.ToLowerInvariant().Replace("0x", "") ?? "";
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    internal class InMemoryStateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public Dictionary<string, Account> Accounts { get; }
        public Dictionary<string, Dictionary<BigInteger, byte[]>> Storage { get; }
        public Dictionary<string, byte[]> Code { get; }

        private readonly Dictionary<string, Account> _pendingAccounts = new Dictionary<string, Account>();
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _pendingStorage = new Dictionary<string, Dictionary<BigInteger, byte[]>>();
        private readonly Dictionary<string, byte[]> _pendingCode = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _deletedAccounts = new HashSet<string>();
        private readonly HashSet<string> _clearedStorage = new HashSet<string>();

        public InMemoryStateSnapshot(
            int snapshotId,
            Dictionary<string, Account> accounts,
            Dictionary<string, Dictionary<BigInteger, byte[]>> storage,
            Dictionary<string, byte[]> code)
        {
            SnapshotId = snapshotId;
            Accounts = accounts;
            Storage = storage;
            Code = code;
        }

        public void SetAccount(string address, Account account)
        {
            var normalizedAddress = NormalizeAddress(address);
            _pendingAccounts[normalizedAddress] = account;
            _deletedAccounts.Remove(normalizedAddress);
        }

        public void SetStorage(string address, BigInteger slot, byte[] value)
        {
            var normalizedAddress = NormalizeAddress(address);
            if (!_pendingStorage.TryGetValue(normalizedAddress, out var accountStorage))
            {
                accountStorage = new Dictionary<BigInteger, byte[]>();
                _pendingStorage[normalizedAddress] = accountStorage;
            }
            accountStorage[slot] = value;
        }

        public void SetCode(byte[] codeHash, byte[] code)
        {
            var hashHex = ToHex(codeHash);
            _pendingCode[hashHex] = code;
        }

        public void DeleteAccount(string address)
        {
            var normalizedAddress = NormalizeAddress(address);
            _deletedAccounts.Add(normalizedAddress);
            _pendingAccounts.Remove(normalizedAddress);
            _pendingStorage.Remove(normalizedAddress);
        }

        public void ClearStorage(string address)
        {
            var normalizedAddress = NormalizeAddress(address);
            _clearedStorage.Add(normalizedAddress);
            _pendingStorage.Remove(normalizedAddress);
        }

        public void Dispose()
        {
        }

        private static string NormalizeAddress(string address)
        {
            return address?.ToLowerInvariant().Replace("0x", "") ?? "";
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
