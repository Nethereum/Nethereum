using System;
using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class BlockAccessListBuilder
    {
        private readonly Dictionary<string, AccountRecord> _accounts =
            new Dictionary<string, AccountRecord>(StringComparer.Ordinal);

        public ulong BlockAccessIndex { get; set; }

        private class AccountRecord
        {
            public string Address;
            public readonly Dictionary<EvmUInt256, List<StorageChange>> StorageChanges =
                new Dictionary<EvmUInt256, List<StorageChange>>();
            public readonly HashSet<EvmUInt256> StorageReads = new HashSet<EvmUInt256>();
            public readonly List<BalanceChange> BalanceChanges = new List<BalanceChange>();
            public readonly List<NonceChange> NonceChanges = new List<NonceChange>();
            public readonly List<CodeChange> CodeChanges = new List<CodeChange>();
        }

        public void EnsureAccount(string address) => GetOrAdd(address);

        public void AddStorageWrite(string address, EvmUInt256 slot, EvmUInt256 postValue)
        {
            var account = GetOrAdd(address);
            if (!account.StorageChanges.TryGetValue(slot, out var changes))
            {
                changes = new List<StorageChange>();
                account.StorageChanges[slot] = changes;
            }
            for (var i = 0; i < changes.Count; i++)
            {
                if (changes[i].BlockAccessIndex == BlockAccessIndex)
                {
                    changes[i] = new StorageChange(BlockAccessIndex, postValue);
                    return;
                }
            }
            changes.Add(new StorageChange(BlockAccessIndex, postValue));
        }

        public void AddStorageRead(string address, EvmUInt256 slot)
        {
            GetOrAdd(address).StorageReads.Add(slot);
        }

        public void AddBalanceChange(string address, EvmUInt256 postBalance)
        {
            var list = GetOrAdd(address).BalanceChanges;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].BlockAccessIndex == BlockAccessIndex)
                {
                    list[i] = new BalanceChange(BlockAccessIndex, postBalance);
                    return;
                }
            }
            list.Add(new BalanceChange(BlockAccessIndex, postBalance));
        }

        public void AddNonceChange(string address, ulong newNonce)
        {
            var list = GetOrAdd(address).NonceChanges;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].BlockAccessIndex == BlockAccessIndex)
                {
                    if (newNonce > list[i].NewNonce)
                        list[i] = new NonceChange(BlockAccessIndex, newNonce);
                    return;
                }
            }
            list.Add(new NonceChange(BlockAccessIndex, newNonce));
        }

        public void AddCodeChange(string address, byte[] newCode)
        {
            var list = GetOrAdd(address).CodeChanges;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].BlockAccessIndex == BlockAccessIndex)
                {
                    list[i] = new CodeChange(BlockAccessIndex, newCode);
                    return;
                }
            }
            list.Add(new CodeChange(BlockAccessIndex, newCode));
        }

        public List<AccountChanges> Build()
        {
            var result = new List<AccountChanges>(_accounts.Count);

            foreach (var record in _accounts.Values)
            {
                var changes = new AccountChanges(record.Address);

                foreach (var pair in record.StorageChanges)
                {
                    var slotChanges = new SlotChanges(pair.Key);
                    slotChanges.Changes.AddRange(pair.Value);
                    slotChanges.Changes.Sort((a, b) => a.BlockAccessIndex.CompareTo(b.BlockAccessIndex));
                    changes.StorageChanges.Add(slotChanges);
                }
                changes.StorageChanges.Sort((a, b) => a.Slot.CompareTo(b.Slot));

                foreach (var slot in record.StorageReads)
                {
                    if (!record.StorageChanges.ContainsKey(slot))
                        changes.StorageReads.Add(slot);
                }
                changes.StorageReads.Sort((a, b) => a.CompareTo(b));

                changes.BalanceChanges.AddRange(record.BalanceChanges);
                changes.BalanceChanges.Sort((a, b) => a.BlockAccessIndex.CompareTo(b.BlockAccessIndex));

                changes.NonceChanges.AddRange(record.NonceChanges);
                changes.NonceChanges.Sort((a, b) => a.BlockAccessIndex.CompareTo(b.BlockAccessIndex));

                changes.CodeChanges.AddRange(record.CodeChanges);
                changes.CodeChanges.Sort((a, b) => a.BlockAccessIndex.CompareTo(b.BlockAccessIndex));

                result.Add(changes);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Address, b.Address));
            return result;
        }

        private AccountRecord GetOrAdd(string address)
        {
            var key = BlockAccessListValueConventions.NormaliseAddress(address);
            if (!_accounts.TryGetValue(key, out var record))
            {
                record = new AccountRecord { Address = key };
                _accounts[key] = record;
            }
            return record;
        }

    }
}
