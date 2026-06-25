using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class SelfDestructGasCost : IOpcodeGasCostAsync
    {
        private readonly bool _hasColdWarmAccess;
        private readonly bool _newAccountRequiresValue;

        /// <param name="hasColdWarmAccess">Berlin+ EIP-2929 cold-account access cost.</param>
        /// <param name="newAccountRequiresValue">
        /// EIP-161 (Spurious Dragon+): G_NEWACCOUNT only charged when self has
        /// balance to transfer AND recipient is empty. At Tangerine Whistle
        /// (EIP-150) the rule is simpler: charge G_NEWACCOUNT whenever the
        /// recipient doesn't exist, regardless of self balance.
        /// </param>
        public SelfDestructGasCost(bool hasColdWarmAccess = true, bool newAccountRequiresValue = true)
        {
            _hasColdWarmAccess = hasColdWarmAccess;
            _newAccountRequiresValue = newAccountRequiresValue;
        }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var addressBytes = program.StackPeekAt(0);
            var recipientAddress = addressBytes.ConvertToEthereumChecksumAddress();

            long gas = GasConstants.SELFDESTRUCT_COST;

            if (_hasColdWarmAccess)
            {
                var isWarm = program.IsAddressWarm(addressBytes);
                if (!isWarm)
                {
                    program.MarkAddressAsWarm(addressBytes);
                    gas += GasConstants.COLD_ACCOUNT_ACCESS_COST;
                }
            }

#if EVM_SYNC
            var recipientBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(recipientAddress);
            var recipientCode = program.ProgramContext.ExecutionStateService.GetCode(recipientAddress);
            var recipientNonce = program.ProgramContext.ExecutionStateService.GetNonce(recipientAddress);
#else
            var recipientBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(recipientAddress);
            var recipientCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(recipientAddress);
            var recipientNonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(recipientAddress);
#endif
            // Two different "exists" semantics depending on fork.
            // EIP-150 (Tangerine Whistle) uses an account-exists check:
            //   "exists" means "loaded from trie" — i.e. an account record is
            //   present at all, regardless of whether balance/code/nonce are
            //   zero. A pre-existing empty entry counts as existing.
            // EIP-161 (Spurious Dragon+) flips to `Empty()`: balance == 0 AND
            //   code.empty AND nonce == 0 — pre-existing empties are treated
            //   as non-existent and charge G_NEWACCOUNT.
            // WasInPreState / IsNewContract are set only by the runner's
            // pre-state load and CREATE materialisation respectively; the
            // on-demand Get*Async materialisation paths above leave both
            // false, so the flags faithfully report "did this entry exist
            // before the SELFDESTRUCT opcode read it?".
            bool existsInTrie = program.ProgramContext.ExecutionStateService.AccountsState.TryGetValue(
                recipientAddress.ToLower(), out var recipientState)
                && (recipientState.WasInPreState || recipientState.IsNewContract);

            bool emptyByEip161 = recipientBalance == 0
                && (recipientCode == null || recipientCode.Length == 0)
                && recipientNonce == 0;

            if (_newAccountRequiresValue)
            {
                // EIP-161 (Spurious Dragon+): charge G_NEWACCOUNT only when
                // recipient is empty AND self has balance to transfer.
                if (emptyByEip161)
                {
#if EVM_SYNC
                    var selfBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(program.ProgramContext.AddressContract);
#else
                    var selfBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(program.ProgramContext.AddressContract);
#endif
                    if (selfBalance > 0)
                    {
                        gas += GasConstants.CALL_NEW_ACCOUNT;
                    }
                }
            }
            else
            {
                // EIP-150 (Tangerine Whistle): charge whenever recipient is
                // not in the trie at all, regardless of self balance. Matches
                // the account-exists branch — "exists" means a state object is
                // present at all, not that it is non-empty.
                if (!existsInTrie)
                {
                    gas += GasConstants.CALL_NEW_ACCOUNT;
                }
            }

            return gas;
        }

        private static bool IsPrecompileAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            var hex = address.StartsWith("0x") || address.StartsWith("0X") ? address.Substring(2) : address;
            var compact = hex.TrimStart('0');
            if (compact.Length == 0) return false;
            if (int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out int addressNum))
            {
                return addressNum >= 1 && addressNum <= 17;
            }
            return false;
        }
    }
}
