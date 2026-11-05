using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    internal sealed class StateJournal
    {
        private enum Kind : byte
        {
            AccountAdded,
            WarmAddressAdded,
            SelfDestructAdded,
            TransientOuterAdded,
            TransientSet,
            Nonce,
            Code,
            ExecutionBalance,
            InitialBalance,
            IsNewContract,
            IsTouched,
            IsRemoved,
            StorageSet,
            StorageCleared,
            StorageReplaced,
            WarmStorageKeyAdded
        }

        private struct Entry
        {
            public Kind Kind;
            public bool Flag;
            public AccountExecutionState Account;
            public EvmAddress Address;
            public EvmUInt256 Key;
            public EvmUInt256 Number;
            public byte[] Bytes;
            public object Reference;
        }

        private struct Revision
        {
            public int Id;
            public int EntryCount;
        }

        private const int InitialCapacity = 32;
        private const int MaxRetainedCapacity = 4096;
        private static readonly Entry[] NoEntries = new Entry[0];
        private static readonly Revision[] NoRevisions = new Revision[0];
        private static Entry[] spareEntries;

        private readonly Dictionary<EvmAddress, AccountExecutionState> _accounts;
        private readonly HashSet<EvmAddress> _warmAddresses;
        private readonly HashSet<EvmAddress> _selfDestructedAddresses;
        private readonly Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> _transientStorage;

        private Entry[] _entries = NoEntries;
        private int _entryCount;
        private Revision[] _revisions = NoRevisions;
        private int _revisionCount;
        private int _nextRevisionId;

        public StateJournal(
            Dictionary<EvmAddress, AccountExecutionState> accounts,
            HashSet<EvmAddress> warmAddresses,
            HashSet<EvmAddress> selfDestructedAddresses,
            Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> transientStorage)
        {
            _accounts = accounts;
            _warmAddresses = warmAddresses;
            _selfDestructedAddresses = selfDestructedAddresses;
            _transientStorage = transientStorage;
        }

        public bool Recording => _revisionCount > 0;

        public int Take()
        {
            if (_revisionCount == _revisions.Length)
                Array.Resize(ref _revisions, _revisionCount == 0 ? 8 : _revisionCount * 2);
            _revisions[_revisionCount].Id = _nextRevisionId;
            _revisions[_revisionCount].EntryCount = _entryCount;
            _revisionCount++;
            return _nextRevisionId++;
        }

        public bool Revert(int id)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                DropAllRevisions();
                return false;
            }

            var entryCount = _revisions[index].EntryCount;
            while (_entryCount > entryCount)
            {
                _entryCount--;
                Undo(ref _entries[_entryCount]);
                _entries[_entryCount] = default(Entry);
            }
            _revisionCount = index + 1;
            return true;
        }

        public void Commit(int id)
        {
            var index = IndexOf(id);
            if (index < 0) return;

            _revisionCount--;
            for (var i = index; i < _revisionCount; i++) _revisions[i] = _revisions[i + 1];
            if (_revisionCount == 0) ClearEntries();
        }

        private int IndexOf(int id)
        {
            for (var i = _revisionCount - 1; i >= 0; i--)
            {
                if (_revisions[i].Id == id) return i;
            }
            return -1;
        }

        private void DropAllRevisions()
        {
            _revisionCount = 0;
            ClearEntries();
        }

        private void ClearEntries()
        {
            Array.Clear(_entries, 0, _entryCount);
            _entryCount = 0;
            ReleaseEntries();
        }

        private void ReleaseEntries()
        {
            if (_entries.Length == 0 || _entries.Length > MaxRetainedCapacity) return;
            Interlocked.CompareExchange(ref spareEntries, _entries, null);
            _entries = NoEntries;
        }

        private void GrowEntries()
        {
            if (_entryCount == 0)
                _entries = Interlocked.Exchange(ref spareEntries, null) ?? new Entry[InitialCapacity];
            else
                Array.Resize(ref _entries, _entryCount * 2);
        }

        private ref Entry Append(Kind kind)
        {
            if (_entryCount == _entries.Length) GrowEntries();
            ref var entry = ref _entries[_entryCount++];
            entry.Kind = kind;
            return ref entry;
        }

        public void AccountAdded(EvmAddress address)
        {
            if (_revisionCount == 0) return;
            Append(Kind.AccountAdded).Address = address;
        }

        public void WarmAddressAdded(EvmAddress address)
        {
            if (_revisionCount == 0) return;
            Append(Kind.WarmAddressAdded).Address = address;
        }

        public void SelfDestructAdded(EvmAddress address)
        {
            if (_revisionCount == 0) return;
            Append(Kind.SelfDestructAdded).Address = address;
        }

        public void TransientOuterAdded(EvmAddress address)
        {
            if (_revisionCount == 0) return;
            Append(Kind.TransientOuterAdded).Address = address;
        }

        public void TransientSet(EvmAddress address, EvmUInt256 key, bool hadPrior, byte[] prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.TransientSet);
            entry.Address = address;
            entry.Key = key;
            entry.Flag = hadPrior;
            entry.Bytes = prior;
        }

        public void NonceChanged(AccountExecutionState account, EvmUInt256? prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.Nonce);
            entry.Account = account;
            entry.Flag = prior.HasValue;
            entry.Number = prior.GetValueOrDefault();
        }

        public void CodeChanged(AccountExecutionState account, byte[] prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.Code);
            entry.Account = account;
            entry.Bytes = prior;
        }

        public void ExecutionBalanceChanged(AccountExecutionState account, EvmUInt256? prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.ExecutionBalance);
            entry.Account = account;
            entry.Flag = prior.HasValue;
            entry.Number = prior.GetValueOrDefault();
        }

        public void InitialBalanceChanged(AccountExecutionState account, EvmUInt256? prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.InitialBalance);
            entry.Account = account;
            entry.Flag = prior.HasValue;
            entry.Number = prior.GetValueOrDefault();
        }

        public void IsNewContractChanged(AccountExecutionState account, bool prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.IsNewContract);
            entry.Account = account;
            entry.Flag = prior;
        }

        public void IsTouchedChanged(AccountExecutionState account, bool prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.IsTouched);
            entry.Account = account;
            entry.Flag = prior;
        }

        public void IsRemovedChanged(AccountExecutionState account, bool prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.IsRemoved);
            entry.Account = account;
            entry.Flag = prior;
        }

        public void StorageSet(AccountExecutionState account, EvmUInt256 key, bool hadPrior, byte[] prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.StorageSet);
            entry.Account = account;
            entry.Key = key;
            entry.Flag = hadPrior;
            entry.Bytes = prior;
        }

        public void StorageCleared(AccountExecutionState account, KeyValuePair<EvmUInt256, byte[]>[] cleared)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.StorageCleared);
            entry.Account = account;
            entry.Reference = cleared;
        }

        public void StorageReplaced(AccountExecutionState account, Dictionary<EvmUInt256, byte[]> prior)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.StorageReplaced);
            entry.Account = account;
            entry.Reference = prior;
        }

        public void WarmStorageKeyAdded(AccountExecutionState account, EvmUInt256 key)
        {
            if (_revisionCount == 0) return;
            ref var entry = ref Append(Kind.WarmStorageKeyAdded);
            entry.Account = account;
            entry.Key = key;
        }

        private void Undo(ref Entry entry)
        {
            switch (entry.Kind)
            {
                case Kind.AccountAdded:
                    _accounts.Remove(entry.Address);
                    break;
                case Kind.WarmAddressAdded:
                    _warmAddresses.Remove(entry.Address);
                    break;
                case Kind.SelfDestructAdded:
                    _selfDestructedAddresses.Remove(entry.Address);
                    break;
                case Kind.TransientOuterAdded:
                    _transientStorage.Remove(entry.Address);
                    break;
                case Kind.TransientSet:
                    UndoTransientSet(ref entry);
                    break;
                case Kind.Nonce:
                    entry.Account.RestoreNonce(entry.Flag ? entry.Number : (EvmUInt256?)null);
                    break;
                case Kind.Code:
                    entry.Account.RestoreCode(entry.Bytes);
                    break;
                case Kind.ExecutionBalance:
                    entry.Account.Balance.RestoreExecutionBalance(entry.Flag ? entry.Number : (EvmUInt256?)null);
                    break;
                case Kind.InitialBalance:
                    entry.Account.Balance.RestoreInitialChainBalance(entry.Flag ? entry.Number : (EvmUInt256?)null);
                    break;
                case Kind.IsNewContract:
                    entry.Account.RestoreIsNewContract(entry.Flag);
                    break;
                case Kind.IsTouched:
                    entry.Account.RestoreIsTouched(entry.Flag);
                    break;
                case Kind.IsRemoved:
                    entry.Account.RestoreIsRemoved(entry.Flag);
                    break;
                case Kind.StorageSet:
                    entry.Account.RestoreStorageSlot(entry.Key, entry.Flag, entry.Bytes);
                    break;
                case Kind.StorageCleared:
                    entry.Account.RestoreClearedStorage((KeyValuePair<EvmUInt256, byte[]>[])entry.Reference);
                    break;
                case Kind.StorageReplaced:
                    entry.Account.RestoreStorageDictionary((Dictionary<EvmUInt256, byte[]>)entry.Reference);
                    break;
                case Kind.WarmStorageKeyAdded:
                    entry.Account.RestoreWarmStorageKeyRemoved(entry.Key);
                    break;
            }
        }

        private void UndoTransientSet(ref Entry entry)
        {
            var inner = _transientStorage[entry.Address];
            if (entry.Flag) inner[entry.Key] = entry.Bytes;
            else inner.Remove(entry.Key);
        }
    }
}
