using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class PendingFlushFlatOverlay : IPendingFlushFlatOverlay
    {
        private readonly object _gate = new object();

        private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>();
        private readonly HashSet<string> _deletedAccounts = new HashSet<string>();
        private readonly HashSet<string> _clearedStorage = new HashSet<string>();
        private readonly Dictionary<(string Address, BigInteger Slot), byte[]> _nonZeroStorage =
            new Dictionary<(string, BigInteger), byte[]>();
        private readonly HashSet<(string Address, BigInteger Slot)> _deletedSlots =
            new HashSet<(string, BigInteger)>();
        private readonly Dictionary<string, byte[]> _code = new Dictionary<string, byte[]>();

        private ulong? _forBlock;

        public void Populate(ulong block, FlatStateBatch flat)
        {
            if (flat == null || flat.IsEmpty) return;

            lock (_gate)
            {
                foreach (var address in flat.DeletedAccountAddresses)
                    _deletedAccounts.Add(NormalizeAddress(address));

                foreach (var address in flat.ClearedStorageAddresses)
                    _clearedStorage.Add(NormalizeAddress(address));

                foreach (var (address, slot, value) in flat.NonZeroStorage)
                    _nonZeroStorage[(NormalizeAddress(address), slot)] = value;

                foreach (var (address, slot) in flat.DeletedSlots)
                    _deletedSlots.Add((NormalizeAddress(address), slot));

                foreach (var (address, account) in flat.Accounts)
                    _accounts[NormalizeAddress(address)] = account;

                foreach (var (codeHash, code) in flat.Code)
                {
                    if (codeHash == null || codeHash.Length != 32) continue;
                    _code[Convert.ToHexString(codeHash)] = code;
                }

                _forBlock = block;
            }
        }

        public void Clear(ulong block)
        {
            lock (_gate)
            {
                if (_forBlock != block) return;
                _accounts.Clear();
                _deletedAccounts.Clear();
                _clearedStorage.Clear();
                _nonZeroStorage.Clear();
                _deletedSlots.Clear();
                _code.Clear();
                _forBlock = null;
            }
        }

        public bool TryGetAccount(string address, out Account account, out bool isDeleted)
        {
            var key = NormalizeAddress(address);
            lock (_gate)
            {
                if (_accounts.TryGetValue(key, out var stored))
                {
                    account = CloneAccount(stored);
                    isDeleted = false;
                    return true;
                }
                if (_deletedAccounts.Contains(key))
                {
                    account = null;
                    isDeleted = true;
                    return true;
                }
                account = null;
                isDeleted = false;
                return false;
            }
        }

        public bool TryGetStorage(string address, BigInteger slot, out byte[] value, out bool isCleared)
        {
            var key = (NormalizeAddress(address), slot);
            lock (_gate)
            {
                if (_nonZeroStorage.TryGetValue(key, out var stored))
                {
                    value = stored;
                    isCleared = false;
                    return true;
                }
                if (_deletedSlots.Contains(key))
                {
                    value = null;
                    isCleared = true;
                    return true;
                }
                if (_clearedStorage.Contains(key.Item1))
                {
                    value = null;
                    isCleared = true;
                    return true;
                }
                value = null;
                isCleared = false;
                return false;
            }
        }

        public bool TryGetCode(byte[] codeHash, out byte[] code)
        {
            if (codeHash == null || codeHash.Length != 32)
            {
                code = null;
                return false;
            }
            lock (_gate)
            {
                return _code.TryGetValue(Convert.ToHexString(codeHash), out code);
            }
        }

        private static string NormalizeAddress(string address) => StateKeys.AccountKeyHex(address);

        private static Account CloneAccount(Account account)
        {
            if (account == null) return null;
            return new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                StateRoot = (byte[])account.StateRoot?.Clone(),
                CodeHash = (byte[])account.CodeHash?.Clone()
            };
        }
    }
}
