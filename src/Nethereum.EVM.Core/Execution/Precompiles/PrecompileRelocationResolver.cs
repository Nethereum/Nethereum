using System.Collections.Generic;
using System.Globalization;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles
{
    public static class PrecompileRelocationResolver
    {
        public static string Normalize(string address)
        {
            if (string.IsNullOrEmpty(address)) return address;
            return EvmAddress.FromHex(address).ToHexLower();
        }

        public static bool TryResolve(
            IReadOnlyDictionary<string, int> relocations,
            PrecompileRegistry registry,
            string normalizedAddress,
            out int precompileIndex)
        {
            precompileIndex = -1;
            if (registry == null || string.IsNullOrEmpty(normalizedAddress)) return false;

            if (relocations != null && relocations.TryGetValue(normalizedAddress, out var mapped))
            {
                if (mapped <= 0) return false;
                if (!registry.CanHandle(mapped)) return false;
                precompileIndex = mapped;
                return true;
            }

            if (TryParseAddressIndex(normalizedAddress, out var natural) && registry.CanHandle(natural))
            {
                precompileIndex = natural;
                return true;
            }
            return false;
        }

        private static bool TryParseAddressIndex(string normalizedAddress, out int index)
        {
            index = -1;
            var compact = normalizedAddress.ToHexCompact();
            return int.TryParse(compact, NumberStyles.HexNumber, null, out index);
        }
    }
}
