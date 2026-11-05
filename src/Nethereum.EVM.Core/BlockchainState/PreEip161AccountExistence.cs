using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.BlockchainState
{
    internal static class PreEip161AccountExistence
    {
#if EVM_SYNC
        public static AccountExistenceFacts ReadFacts(ExecutionStateService state, EvmAddress address)
        {
            var balance = state.GetTotalBalance(address);
            var code = state.GetCode(address);
            var nonce = state.GetNonce(address);
            var hasAnAccountRecord = Exists(state, address);
            return AccountExistenceFacts.Resolved(balance, code, nonce, hasAnAccountRecord);
        }

        public static bool Exists(ExecutionStateService state, EvmAddress address)
        {
            if (state.AccountsState.TryGetValue(address, out var acct))
            {
                if (acct.WasInPreState) return true;
                if (acct.IsNewContract) return true;
                if (acct.Balance.GetTotalBalance() > 0) return true;
                if (acct.Nonce.HasValue && acct.Nonce.Value > 0) return true;
                if (acct.WasMaterialisedByCallFrame) return true;
                if (acct.Balance.InitialChainBalance.HasValue && acct.Balance.InitialChainBalance.Value > 0) return true;
                if (acct.IsTouched) return true;
            }

            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var chainBalance = state.StateReader.GetBalance(address.ToByteArray());
                if (chainBalance > 0) return true;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var chainNonce = state.StateReader.GetTransactionCount(address.ToByteArray());
                if (chainNonce > 0) return true;
            }
            if (acct == null || acct.Code == null)
            {
                var chainCode = state.StateReader.GetCode(address.ToByteArray());
                if (chainCode != null && chainCode.Length > 0) return true;
            }

            return state.StateReader.AccountExists(address.ToHexLower());
        }
#else
        public static async Task<AccountExistenceFacts> ReadFactsAsync(ExecutionStateService state, EvmAddress address)
        {
            var balance = await state.GetTotalBalanceAsync(address);
            var code = await state.GetCodeAsync(address);
            var nonce = await state.GetNonceAsync(address);
            var hasAnAccountRecord = await ExistsAsync(state, address);
            return AccountExistenceFacts.Resolved(balance, code, nonce, hasAnAccountRecord);
        }

        public static async Task<bool> ExistsAsync(ExecutionStateService state, EvmAddress address)
        {
            if (state.AccountsState.TryGetValue(address, out var acct))
            {
                if (acct.WasInPreState) return true;
                if (acct.IsNewContract) return true;
                if (acct.Balance.GetTotalBalance() > 0) return true;
                if (acct.Nonce.HasValue && acct.Nonce.Value > 0) return true;
                if (acct.WasMaterialisedByCallFrame) return true;
                if (acct.Balance.InitialChainBalance.HasValue && acct.Balance.InitialChainBalance.Value > 0) return true;
                if (acct.IsTouched) return true;
            }

            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var chainBalance = await state.StateReader.GetBalanceAsync(address.ToByteArray());
                if (chainBalance > 0) return true;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var chainNonce = await state.StateReader.GetTransactionCountAsync(address.ToByteArray());
                if (chainNonce > 0) return true;
            }
            if (acct == null || acct.Code == null)
            {
                var chainCode = await state.StateReader.GetCodeAsync(address.ToByteArray());
                if (chainCode != null && chainCode.Length > 0) return true;
            }

            return await state.StateReader.AccountExistsAsync(address.ToHexLower());
        }
#endif
    }
}
