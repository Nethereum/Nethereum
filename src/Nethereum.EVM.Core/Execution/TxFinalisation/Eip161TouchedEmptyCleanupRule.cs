using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// EIP-161 STATE_CLEARING (Spurious Dragon+). At end of tx, delete
    /// every account that was touched during the tx and is now empty
    /// (nonce=0, balance=0, code.empty). Mirrors geth's
    /// <c>StateDB.Finalise(deleteEmptyObjects:true)</c> over journal.dirties.
    /// </summary>
    public sealed class Eip161TouchedEmptyCleanupRule : ITouchedEmptyCleanupRule
    {
        public static readonly Eip161TouchedEmptyCleanupRule Instance = new Eip161TouchedEmptyCleanupRule();
        private Eip161TouchedEmptyCleanupRule() { }

        public void Apply(ExecutionStateService executionState)
        {
            var toRemove = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in executionState.AccountsState)
            {
                if (!kvp.Value.IsTouched) continue;
                if (!IsEmpty(kvp.Value)) continue;
                toRemove.Add(kvp.Key);
            }
            // Tx-global touched set: addresses touched in a sub-call that later
            // reverted. AccountExecutionState.IsTouched was rolled back by the
            // revert, but geth keeps these in the spec's substate.touched —
            // walk them explicitly.
            foreach (var address in executionState.TxGloballyTouchedAddresses)
            {
                if (!executionState.AccountsState.TryGetValue(address, out var account)) continue;
                if (!IsEmpty(account)) continue;
                toRemove.Add(address);
            }
            foreach (var address in toRemove)
            {
                executionState.DeleteAccount(address);
            }
        }

        private static bool IsEmpty(AccountExecutionState account)
        {
            if (account.Nonce.HasValue && account.Nonce.Value != EvmUInt256.Zero) return false;
            if (!account.Balance.GetTotalBalance().IsZero) return false;
            if (account.Code != null && account.Code.Length > 0) return false;
            return true;
        }
    }
}
