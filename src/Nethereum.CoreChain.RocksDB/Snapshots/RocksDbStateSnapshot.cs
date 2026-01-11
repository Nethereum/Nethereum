using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Snapshots
{
    public class RocksDbStateSnapshot : IStateSnapshot
    {
        private readonly RocksDbManager _manager;
        private readonly Snapshot _rocksSnapshot;
        private readonly ReadOptions _readOptions;

        private readonly Dictionary<string, Account> _pendingAccounts = new Dictionary<string, Account>();
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _pendingStorage = new Dictionary<string, Dictionary<BigInteger, byte[]>>();
        private readonly Dictionary<string, byte[]> _pendingCode = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _deletedAccounts = new HashSet<string>();
        private readonly HashSet<string> _clearedStorage = new HashSet<string>();
        private readonly HashSet<string> _modifiedAddresses = new HashSet<string>();
        private readonly HashSet<byte[]> _modifiedStorageKeys = new HashSet<byte[]>(ByteArrayComparer.Current);

        private bool _disposed;

        public int SnapshotId { get; }

        public RocksDbStateSnapshot(RocksDbManager manager, int snapshotId)
        {
            _manager = manager;
            SnapshotId = snapshotId;
            _rocksSnapshot = manager.CreateSnapshot();
            _readOptions = new ReadOptions().SetSnapshot(_rocksSnapshot);
        }

        public void SetAccount(string address, Account account)
        {
            var normalized = NormalizeAddress(address);
            _pendingAccounts[normalized] = account;
            _deletedAccounts.Remove(normalized);
        }

        public void SetStorage(string address, BigInteger slot, byte[] value)
        {
            var normalized = NormalizeAddress(address);
            if (!_pendingStorage.TryGetValue(normalized, out var storage))
            {
                storage = new Dictionary<BigInteger, byte[]>();
                _pendingStorage[normalized] = storage;
            }
            storage[slot] = value;
        }

        public void SetCode(byte[] codeHash, byte[] code)
        {
            var hashHex = ToHex(codeHash);
            _pendingCode[hashHex] = code;
        }

        public void DeleteAccount(string address)
        {
            var normalized = NormalizeAddress(address);
            _deletedAccounts.Add(normalized);
            _pendingAccounts.Remove(normalized);
            _pendingStorage.Remove(normalized);
        }

        public void ClearStorage(string address)
        {
            var normalized = NormalizeAddress(address);
            _clearedStorage.Add(normalized);
            _pendingStorage.Remove(normalized);
        }

        public Dictionary<string, Account> PendingAccounts => _pendingAccounts;
        public Dictionary<string, Dictionary<BigInteger, byte[]>> PendingStorage => _pendingStorage;
        public Dictionary<string, byte[]> PendingCode => _pendingCode;
        public HashSet<string> DeletedAccounts => _deletedAccounts;
        public HashSet<string> ClearedStorage => _clearedStorage;
        public ReadOptions SnapshotReadOptions => _readOptions;
        public HashSet<string> ModifiedAddresses => _modifiedAddresses;
        public HashSet<byte[]> ModifiedStorageKeys => _modifiedStorageKeys;

        public void TrackAccountModification(string address)
        {
            _modifiedAddresses.Add(NormalizeAddress(address));
        }

        public void TrackStorageModification(byte[] storageKey)
        {
            _modifiedStorageKeys.Add(storageKey);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _rocksSnapshot?.Dispose();
                }
                _disposed = true;
            }
        }

        private static string NormalizeAddress(string address)
        {
            return address?.ToLowerInvariant().Replace("0x", "") ?? "";
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
