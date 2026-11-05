using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class PendingFlushFlatOverlay : IPendingFlushFlatOverlay
    {
        private readonly object _gate = new object();

        private readonly Dictionary<EvmAddress, Account> _accounts = new Dictionary<EvmAddress, Account>();
        private readonly HashSet<EvmAddress> _deletedAccounts = new HashSet<EvmAddress>();
        private readonly HashSet<EvmAddress> _clearedStorage = new HashSet<EvmAddress>();
        private readonly Dictionary<(EvmAddress Address, BigInteger Slot), byte[]> _nonZeroStorage =
            new Dictionary<(EvmAddress, BigInteger), byte[]>();
        private readonly HashSet<(EvmAddress Address, BigInteger Slot)> _deletedSlots =
            new HashSet<(EvmAddress, BigInteger)>();
        private readonly Dictionary<byte[], byte[]> _code = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);

        private ulong? _forBlock;

        public void Populate(ulong block, FlatStateBatch flat)
        {
            if (flat == null || flat.IsEmpty) return;

            lock (_gate)
            {
                foreach (var address in flat.DeletedAccountEvmAddresses)
                    _deletedAccounts.Add(address);

                foreach (var address in flat.ClearedStorageEvmAddresses)
                    _clearedStorage.Add(address);

                foreach (var (address, slot, value) in flat.NonZeroStorageEvm)
                    _nonZeroStorage[(address, slot)] = value;

                foreach (var (address, slot) in flat.DeletedSlotsEvm)
                    _deletedSlots.Add((address, slot));

                foreach (var (address, account) in flat.AccountsEvm)
                    _accounts[address] = account;

                foreach (var (codeHash, code) in flat.Code)
                {
                    if (codeHash == null || codeHash.Length != 32) continue;
                    _code[codeHash] = code;
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
            => TryGetAccount(EvmAddress.FromHex(address), out account, out isDeleted);

        public bool TryGetAccount(EvmAddress key, out Account account, out bool isDeleted)
        {
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
            => TryGetStorage(EvmAddress.FromHex(address), slot, out value, out isCleared);

        public bool TryGetStorage(EvmAddress address, BigInteger slot, out byte[] value, out bool isCleared)
        {
            var key = (address, slot);
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
                return _code.TryGetValue(codeHash, out code);
            }
        }

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
