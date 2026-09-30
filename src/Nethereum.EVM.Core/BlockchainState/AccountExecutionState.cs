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

        public Dictionary<EvmUInt256, byte[]> Storage { get; set; } = new Dictionary<EvmUInt256, byte[]>();
        public Dictionary<EvmUInt256, byte[]> OriginalStorageValues { get; } = new();
        public HashSet<EvmUInt256> WarmStorageKeys { get; } = new();

        public EvmUInt256? Nonce { get; set; }
        public byte[] Code { get; set; }
        public bool IsNewContract { get; set; }

        public bool IsTouched { get; set; }

        public bool IsRemoved { get; set; }

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
            Storage.Clear();
            OriginalStorageValues.Clear();
            IsStorageReset = true;
        }

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
            Storage[key] = value;
        }

        public void UpsertStorageValue(EvmUInt256 key, byte[] value)
        {
            IsTouched = true;
            if (!Storage.ContainsKey(key))
            {
                Storage.Add(key, value);
            }
            else
            {
                Storage[key] = value;
            }
        }

        public void SetPreStateStorage(EvmUInt256 key, byte[] value)
        {
            Storage[key] = value;
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
            Storage.Clear();
            OriginalStorageValues.Clear();
        }

        public bool IsStorageKeyWarm(EvmUInt256 key) => WarmStorageKeys.Contains(key);

        public void MarkStorageKeyAsWarm(EvmUInt256 key) => WarmStorageKeys.Add(key);

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