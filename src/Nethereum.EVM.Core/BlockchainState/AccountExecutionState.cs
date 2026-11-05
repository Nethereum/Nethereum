using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionState
    {
        public string Address { get; set; }
        private AccountExecutionBalance _balance;
        public AccountExecutionBalance Balance
        {
            get => _balance;
            set
            {
                _balance = value;
                if (_balance != null) _balance.Owner = this;
            }
        }

        public AccountExecutionState()
        {
            Balance = new AccountExecutionBalance();
        }

        internal StateJournal Journal;

        private Dictionary<EvmUInt256, byte[]> _storage = new Dictionary<EvmUInt256, byte[]>();
        public Dictionary<EvmUInt256, byte[]> Storage
        {
            get => _storage;
            set
            {
                Journal?.StorageReplaced(this, _storage);
                _storage = value;
            }
        }
        public Dictionary<EvmUInt256, byte[]> OriginalStorageValues { get; } = new();
        public HashSet<EvmUInt256> WarmStorageKeys { get; } = new();

        private EvmUInt256? _nonce;
        public EvmUInt256? Nonce
        {
            get => _nonce;
            set
            {
                Journal?.NonceChanged(this, _nonce);
                _nonce = value;
            }
        }

        private byte[] _code;
        public byte[] Code
        {
            get => _code;
            set
            {
                Journal?.CodeChanged(this, _code);
                _code = value;
            }
        }

        private bool _chainHoldsNoCode;

        internal bool IsCodeResolved => _code != null || _chainHoldsNoCode;

        internal void RecordChainCode(byte[] chainCode)
        {
            _chainHoldsNoCode = chainCode == null;
            Code = chainCode;
        }

        private bool _isNewContract;
        public bool IsNewContract
        {
            get => _isNewContract;
            set
            {
                if (_isNewContract == value) return;
                Journal?.IsNewContractChanged(this, _isNewContract);
                _isNewContract = value;
            }
        }

        private bool _isTouched;
        public bool IsTouched
        {
            get => _isTouched;
            set
            {
                if (_isTouched == value) return;
                Journal?.IsTouchedChanged(this, _isTouched);
                _isTouched = value;
            }
        }

        private bool _isRemoved;
        public bool IsRemoved
        {
            get => _isRemoved;
            set
            {
                if (_isRemoved == value) return;
                Journal?.IsRemovedChanged(this, _isRemoved);
                _isRemoved = value;
            }
        }

        /// <summary>
        /// EIP-161 §Specification: <i>"An account is considered empty when it has no code and
        /// zero nonce and zero balance."</i> The one definition — the end-of-transaction sweep
        /// (<c>Eip161TouchedEmptyCleanupRule</c>) and the CALL new-account gas charge
        /// (<see cref="ExecutionStateService.IsAccountEmpty"/>) both ask it here.
        ///
        /// <para>It answers over the fields this entry HOLDS. A field the transaction never
        /// resolved reads as zero, so an entry must be materialised by a read of the account
        /// — <see cref="ExecutionStateService.LoadBalanceNonceAndCodeFromStorage"/> — before
        /// this answer means anything. <see cref="ExecutionStateService.IsAccountEmpty"/> is
        /// the variant that resolves the missing fields itself, for callers holding only an
        /// address.</para>
        /// </summary>
        public bool IsEmptyPerEip161()
        {
            if (Nonce.HasValue && !Nonce.Value.IsZero) return false;
            if (!Balance.GetTotalBalance().IsZero) return false;
            return Code == null || Code.Length == 0;
        }

        public bool HoldsTheFieldsEip161Judges()
        {
            return Balance.InitialChainBalance.HasValue && Nonce.HasValue && Code != null;
        }

        public bool WasInPreState { get; set; }

        public bool WasMaterialisedByCallFrame { get; set; }

        public bool IsStorageReset { get; set; }

        public void ResetStorageForOverride()
        {
            ClearStorage();
            OriginalStorageValues.Clear();
            IsStorageReset = true;
        }

        private void PutStorage(EvmUInt256 key, byte[] value)
        {
            var journal = Journal;
            if (journal != null && journal.Recording)
            {
                var hadPrior = _storage.TryGetValue(key, out var prior);
                journal.StorageSet(this, key, hadPrior, prior);
            }
            _storage[key] = value;
        }

        private void ClearStorage()
        {
            var journal = Journal;
            if (journal != null && journal.Recording && _storage.Count > 0)
            {
                var cleared = new KeyValuePair<EvmUInt256, byte[]>[_storage.Count];
                ((ICollection<KeyValuePair<EvmUInt256, byte[]>>)_storage).CopyTo(cleared, 0);
                journal.StorageCleared(this, cleared);
            }
            _storage.Clear();
        }

        internal void RestoreNonce(EvmUInt256? nonce) => _nonce = nonce;

        internal void RestoreCode(byte[] code) => _code = code;

        internal void RestoreIsNewContract(bool value) => _isNewContract = value;

        internal void RestoreIsTouched(bool value) => _isTouched = value;

        internal void RestoreIsRemoved(bool value) => _isRemoved = value;

        internal void RestoreStorageSlot(EvmUInt256 key, bool hadPrior, byte[] prior)
        {
            if (hadPrior) _storage[key] = prior;
            else _storage.Remove(key);
        }

        internal void RestoreClearedStorage(KeyValuePair<EvmUInt256, byte[]>[] cleared)
        {
            _storage.Clear();
            for (var i = 0; i < cleared.Length; i++) _storage[cleared[i].Key] = cleared[i].Value;
        }

        internal void RestoreStorageDictionary(Dictionary<EvmUInt256, byte[]> storage) => _storage = storage;

        internal void RestoreWarmStorageKeyRemoved(EvmUInt256 key) => WarmStorageKeys.Remove(key);

        public bool StorageContainsKey(EvmUInt256 key)
        {
            return Storage.ContainsKey(key);
        }

        public void TrackAndWriteStorage(EvmUInt256 key, byte[] value)
        {
            if (!OriginalStorageValues.ContainsKey(key))
            {
                OriginalStorageValues[key] = value;
            }
            PutStorage(key, value);
        }

        public void UpsertStorageValue(EvmUInt256 key, byte[] value)
        {
            IsTouched = true;
            PutStorage(key, value);
        }

        public void SetPreStateStorage(EvmUInt256 key, byte[] value)
        {
            PutStorage(key, value);
            OriginalStorageValues[key] = value;
        }

        public byte[] GetStorageValue(EvmUInt256 key)
        {
            if (StorageContainsKey(key))
            {
                return Storage[key];
            }
            return null;
        }

        /// <summary>
        /// EIP-2929: "The sets are transaction-context-wide, implemented identically to other
        /// transaction-scoped constructs such as the self-destruct-list and global <c>refund</c>
        /// counter. In particular, if a scope reverts, the access lists should be in the state
        /// they were in before that scope was entered."
        ///
        /// <para>So <see cref="WarmStorageKeys"/> is not account storage and a creation does not
        /// discard it: the sender already paid EIP-2930 intrinsic gas for every slot the
        /// transaction pre-declared.</para>
        ///
        /// <para>WHAT THIS CAN ACTUALLY CLEAR, because the name suggests more than it does: a
        /// creation onto an address that holds chain storage is refused before it gets here, by
        /// <c>AccountDeployability</c> on BOTH creation paths — the transaction path at
        /// <c>TransactionExecutor.Execute.cs:338/341</c> and the CREATE/CREATE2 opcode path at
        /// <c>EVMSimulator.SetupCreateFrame.cs:193</c>. So the only entries present here are slots
        /// this transaction itself cached, and they are provably zero, or the deployability check
        /// would have refused. It is not the EIP's storage-destruction rule and must not be read as
        /// one: <c>destroy_storage</c> at Amsterdam removes no persisted storage either, and
        /// <c>BlockDiff.storage_clears</c> was removed at Cancun with EIP-6780.</para>
        ///
        /// <para>It is not dead, though: it also clears <see cref="OriginalStorageValues"/>, which
        /// <c>SstoreSlotResolvingGasCost</c> and <c>EvmStorageMemoryExecution</c> meter SSTORE net
        /// gas and refunds against. Removing it is a gas change, not a cleanup.</para>
        /// </summary>
        public void ClearStorageForNewContract()
        {
            ClearStorage();
            OriginalStorageValues.Clear();
        }

        public bool IsStorageKeyWarm(EvmUInt256 key) => WarmStorageKeys.Contains(key);

        public void MarkStorageKeyAsWarm(EvmUInt256 key)
        {
            if (WarmStorageKeys.Add(key)) Journal?.WarmStorageKeyAdded(this, key);
        }

        public Dictionary<string, string> GetContractStorageAsHex()
        {
            var storage = Storage;
            if (storage == null) return null;
            var dictionary = new Dictionary<string, string>();
            foreach (var item in storage)
            {
                if (item.Value != null)
                {
                    dictionary.Add(item.Key.ToBigEndian().ToHex(), item.Value.ToHex());
                }
            }
            return dictionary;
        }
    }
}