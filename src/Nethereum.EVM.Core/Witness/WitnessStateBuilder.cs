using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Witness
{
    public static class WitnessStateBuilder
    {
        public static Dictionary<string, AccountState> BuildAccountState(List<WitnessAccount> witnessAccounts)
        {
            var accounts = new Dictionary<string, AccountState>();
            foreach (var acc in witnessAccounts)
            {
                var state = new AccountState
                {
                    Balance = acc.Balance,
                    Nonce = acc.Nonce,
                    Code = acc.Code ?? new byte[0]
                };
                if (acc.Storage != null)
                    foreach (var slot in acc.Storage)
                        state.Storage[slot.Key] = slot.Value.ToBigEndian();
                accounts[acc.Address.ToLower()] = state;
            }
            return accounts;
        }

#if EVM_SYNC
        public static void LoadAllAccountsAndStorage(
            ExecutionStateService executionState, List<WitnessAccount> witnessAccounts)
        {
            foreach (var acc in witnessAccounts)
            {
                executionState.LoadBalanceNonceAndCodeFromStorage(acc.Address);
                if (acc.Storage != null)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in acc.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value.ToBigEndian());
                }
            }
        }
#endif

#if !EVM_SYNC
        public static async Task LoadAllAccountsAndStorageAsync(
            ExecutionStateService executionState, List<WitnessAccount> witnessAccounts)
        {
            foreach (var acc in witnessAccounts)
            {
                await executionState.LoadBalanceNonceAndCodeFromStorageAsync(acc.Address);
                if (acc.Storage != null)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in acc.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value.ToBigEndian());
                }
            }
        }
#endif
    }
}
