using System.Collections.Generic;
using System.Text.Json;
using Nethereum.CoreChain.Tracing;
using Nethereum.Hex.HexTypes;

namespace Nethereum.CoreChain.Rpc
{
    internal static class StateOverrideJsonParser
    {
        public static Dictionary<string, StateOverride> ParseStateOverrideSet(JsonElement element)
        {
            var overrides = new Dictionary<string, StateOverride>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Null)
                    continue;

                if (prop.Value.ValueKind != JsonValueKind.Object)
                    throw RpcException.InvalidParams(
                        $"state override for account '{prop.Name}' must be an object");

                overrides[prop.Name] = ParseStateOverride(prop.Name, prop.Value);
            }
            return overrides;
        }

        public static StateOverride ParseStateOverride(string address, JsonElement element)
        {
            var stateOverride = new StateOverride();

            if (element.TryGetProperty("balance", out var balanceProp))
            {
                var balanceStr = balanceProp.GetString();
                if (!string.IsNullOrEmpty(balanceStr))
                    stateOverride.Balance = new HexBigInteger(balanceStr);
            }

            if (element.TryGetProperty("nonce", out var nonceProp))
                stateOverride.Nonce = nonceProp.GetString();

            if (element.TryGetProperty("code", out var codeProp))
                stateOverride.Code = codeProp.GetString();

            var hasState = element.TryGetProperty("state", out var stateProp)
                           && stateProp.ValueKind != JsonValueKind.Null;
            var hasStateDiff = element.TryGetProperty("stateDiff", out var stateDiffProp)
                               && stateDiffProp.ValueKind != JsonValueKind.Null;

            if (hasState && hasStateDiff)
                throw RpcException.InvalidParams(
                    $"account '{address}' cannot set both 'state' and 'stateDiff'");

            if (hasState)
            {
                if (stateProp.ValueKind != JsonValueKind.Object)
                    throw RpcException.InvalidParams(
                        $"state override field 'state' for account '{address}' must be an object");
                stateOverride.State = ParseStorageSlots(stateProp);
            }

            if (hasStateDiff)
            {
                if (stateDiffProp.ValueKind != JsonValueKind.Object)
                    throw RpcException.InvalidParams(
                        $"state override field 'stateDiff' for account '{address}' must be an object");
                stateOverride.StateDiff = ParseStorageSlots(stateDiffProp);
            }

            return stateOverride;
        }

        private static Dictionary<string, string> ParseStorageSlots(JsonElement element)
        {
            var slots = new Dictionary<string, string>();
            foreach (var prop in element.EnumerateObject())
            {
                slots[prop.Name] = prop.Value.GetString();
            }
            return slots;
        }
    }
}
