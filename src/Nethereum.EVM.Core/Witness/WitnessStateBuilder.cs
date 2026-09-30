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
            ExecutionStateService executionState,
            InMemoryStateReader stateReader,
            List<WitnessAccount> witnessAccounts)
        {
            foreach (var acc in witnessAccounts)
            {
                executionState.LoadBalanceNonceAndCodeFromStorage(acc.Address);
                var committed = stateReader.GetAccountState(acc.Address);
                if (committed?.Storage != null && committed.Storage.Count > 0)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in committed.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value);
                }
            }
        }
#endif

#if !EVM_SYNC
        public static async Task LoadAllAccountsAndStorageAsync(
            ExecutionStateService executionState,
            InMemoryStateReader stateReader,
            List<WitnessAccount> witnessAccounts)
        {
            foreach (var acc in witnessAccounts)
            {
                await executionState.LoadBalanceNonceAndCodeFromStorageAsync(acc.Address);
                var committed = stateReader.GetAccountState(acc.Address);
                if (committed?.Storage != null && committed.Storage.Count > 0)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in committed.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value);
                }
            }
        }
#endif
    }
}
