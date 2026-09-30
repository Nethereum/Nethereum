using System.Collections.Generic;
using Nethereum.CoreChain.Tracing;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    internal static class AccountOverrideMapper
    {
        public static Dictionary<string, StateOverride> ToStateOverrideSet(Dictionary<string, AccountOverride> overrides)
        {
            if (overrides == null) return null;

            var result = new Dictionary<string, StateOverride>();
            foreach (var kvp in overrides)
            {
                result[kvp.Key] = ToStateOverride(kvp.Key, kvp.Value);
            }
            return result;
        }

        private static StateOverride ToStateOverride(string address, AccountOverride accountOverride)
        {
            if (accountOverride.State != null && accountOverride.StateDiff != null)
                throw RpcException.InvalidParams(
                    $"account '{address}' cannot set both 'state' and 'stateDiff'");

            return new StateOverride
            {
                Balance = accountOverride.Balance,
                Nonce = accountOverride.Nonce?.HexValue,
                Code = accountOverride.Code,
                State = accountOverride.State,
                StateDiff = accountOverride.StateDiff
            };
        }
    }
}
