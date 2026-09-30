using Nethereum.EVM.BlockchainState;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Create
{
    /// <summary>
    /// EIP-7610: an address is deployable only when it has "neither a nonzero nonce, a
    /// nonzero code length, [nor] non-empty storage". The EIP applies retroactively to all
    /// existing blocks, so there is no fork to select on.
    ///
    /// <para>The two callers act on the same answer differently — a creation transaction
    /// halts and forfeits its whole gas grant, a CREATE/CREATE2 opcode pushes zero and lets
    /// the calling frame carry on — so this states the condition and nothing else.</para>
    ///
    /// <para><b>THE STORAGE CLAUSE IS A DELIBERATE DIVERGENCE FROM CURRENT EELS, AND WHICH
    /// EELS YOU READ DECIDES WHETHER IT IS ONE AT ALL.</b> The pinned checkout this repository
    /// runs differentials against still carries it — <c>forks/amsterdam/state_tracker.py</c>
    /// <c>account_deployable</c> calls <c>account_has_storage</c> and returns False, and that
    /// method is declared REQUIRED on the abstract state interface (<c>state.py</c>, comment
    /// "Only needed for EIP-7610"). EELS <i>master</i> has since dropped it and checks nonce and
    /// code_hash only, consistent with EIP-7610 not being scheduled for Glamsterdam. Verified
    /// against both on 2026-08-30.</para>
    ///
    /// <para>So against the pin we AGREE with EELS and a local differential run will not flag
    /// this; against master the storage clause is ours. EEST calls the cell "undefined in
    /// protocol". Nethereum enforces it retroactively and ungated, on both creation paths —
    /// <c>EVMSimulator.SetupCreateFrame</c> and <c>TransactionExecutor.Execute</c>. Do not
    /// re-litigate this without first checking which EELS you are reading; the date matters more
    /// than the sentence.</para>
    ///
    /// <para>A reader that cannot see storage answers "none" and this returns deployable.
    /// <see cref="IAccountStorageReader"/> carries the capability, and it is deliberately kept
    /// off <c>IStateReader</c>, which is implemented outside this repository. On the node path
    /// the answer is sound: the store carries the capability and the flat store is backfilled
    /// forward by execution once snap heal completes. On diagnostic paths — eth_call, trace,
    /// replay — a reader without it degrades to "no storage" rather than failing, because
    /// refusing there would break callers that work today.</para>
    /// </summary>
    public static class AccountDeployability
    {
#if EVM_SYNC
        public static bool IsDeployable(ExecutionStateService executionState, string address)
        {
            if (HasNonceOrCode(executionState, address)) return false;
            return !executionState.AccountHasStorage(address);
        }

        private static bool HasNonceOrCode(ExecutionStateService executionState, string address)
        {
            if (executionState.GetNonce(address) > 0) return true;
            var code = executionState.GetCode(address);
            return code != null && code.Length > 0;
        }
#else
        public static async Task<bool> IsDeployableAsync(ExecutionStateService executionState, string address)
        {
            if (await HasNonceOrCodeAsync(executionState, address)) return false;
            return !await executionState.AccountHasStorageAsync(address);
        }

        private static async Task<bool> HasNonceOrCodeAsync(ExecutionStateService executionState, string address)
        {
            if (await executionState.GetNonceAsync(address) > 0) return true;
            var code = await executionState.GetCodeAsync(address);
            return code != null && code.Length > 0;
        }
#endif
    }
}
