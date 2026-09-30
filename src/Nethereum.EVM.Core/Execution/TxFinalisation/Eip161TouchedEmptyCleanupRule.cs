using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// EIP-161 §Specification item (d): "At the end of the transaction, any account
    /// touched by the execution of that transaction which is now empty SHALL instead
    /// become non-existent (i.e. deleted)." Spurious Dragon onward.
    /// </summary>
    public sealed class Eip161TouchedEmptyCleanupRule : ITouchedEmptyCleanupRule
    {
        public static readonly Eip161TouchedEmptyCleanupRule Instance = new Eip161TouchedEmptyCleanupRule();
        private Eip161TouchedEmptyCleanupRule() { }

        public void Apply(ExecutionStateService executionState)
        {
            var toRemove = new HashSet<EvmAddress>();
            CollectTouchedEmptyAccounts(executionState, toRemove);
            CollectTxGloballyTouchedEmptyAccounts(executionState, toRemove);

            foreach (var address in toRemove)
            {
                executionState.DeleteAccount(address);
            }
        }

        private static void CollectTouchedEmptyAccounts(
            ExecutionStateService executionState, HashSet<EvmAddress> toRemove)
        {
            foreach (var kvp in executionState.AccountsState)
            {
                if (!kvp.Value.IsTouched) continue;
                if (!kvp.Value.IsEmptyPerEip161()) continue;
                toRemove.Add(kvp.Key);
            }
        }

        private static void CollectTxGloballyTouchedEmptyAccounts(
            ExecutionStateService executionState, HashSet<EvmAddress> toRemove)
        {
            foreach (var address in executionState.TxGloballyTouchedAddresses)
            {
                if (!executionState.AccountsState.TryGetValue(address, out var account)) continue;
                if (!account.HoldsTheFieldsEip161Judges()) continue;
                if (!account.IsEmptyPerEip161()) continue;
                toRemove.Add(address);
            }
        }
    }
}
