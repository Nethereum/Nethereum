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

        /// <summary>
        /// EIP-161 STATE_CLEARING dirty bit. Set by every state mutation
        /// (Credit/Debit balance, SSTORE write, explicit nonce/code set,
        /// CREATE materialisation). Captured/restored by snapshots so
        /// reverted sub-calls roll back their touches. Consumed by
        /// HardforkConfig.TouchedEmptyCleanupRule at end-of-tx.
        /// Unused at pre-EIP-161 forks (NoOp cleanup rule).
        /// </summary>
        public bool IsTouched { get; set; }

        /// <summary>
        /// True when this account entry was created by loading from pre-state
        /// (test fixture's "pre" section or chain state in production sync),
        /// as opposed to being materialised on demand by a chain read
        /// (GetCode/GetTotalBalance for an EXTCODEHASH or BALANCE opcode).
        /// Distinguishes preexisting empties (should remain in the trie at
        /// pre-EIP-161 forks per spec) from phantoms (materialisation
        /// artifacts that a normal state lookup never creates). Set by the
        /// pre-state load path only.
        /// </summary>
        public bool WasInPreState { get; set; }

        /// <summary>
        /// True when this account entry was materialised by an inbound
        /// CALL frame setup, as opposed to materialised by a read opcode
        /// (BALANCE / EXTCODESIZE / EXTCODECOPY / EXTCODEHASH). The
        /// Frontier–Tangerine G_NEWACCOUNT gate
        /// (<see cref="Gas.Opcodes.Costs.CallGasCosts"/>) treats read-only
        /// materialisation as "address does not exist" — the 25 000-gas
        /// surcharge fires on the first inbound CALL only when this flag
        /// is unset. Pure read materialisation must not satisfy the
        /// "exists" predicate.
        /// </summary>
        public bool WasMaterialisedByCallFrame { get; set; }

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
            // SSTORE is a state mutation — EIP-161 touch. Pre-state load
            // (SetPreStateStorage) and SLOAD-warm-cache (TrackAndWriteStorage)
            // deliberately do not flip IsTouched.
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

        public void ClearStorageForNewContract()
        {
            Storage.Clear();
            OriginalStorageValues.Clear();
            WarmStorageKeys.Clear();
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
                    dictionary.Add(item.Key.ToString(), item.Value.ToHex());
                }
            }
            return dictionary;
        }
    }
}