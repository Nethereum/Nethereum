using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles
{
    public static class PrecompileRelocationResolver
    {
        private const int MaxIndexDigits = 8;

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
            index = 0;
            var position = HasHexPrefix(normalizedAddress) ? 2 : 0;
            while (position < normalizedAddress.Length && normalizedAddress[position] == '0') position++;
            var digits = normalizedAddress.Length - position;
            if (digits == 0 || digits > MaxIndexDigits)
            {
                index = -1;
                return false;
            }

            long value = 0;
            for (; position < normalizedAddress.Length; position++)
            {
                var digit = HexDigit(normalizedAddress[position]);
                if (digit < 0)
                {
                    index = -1;
                    return false;
                }
                value = (value << 4) | (uint)digit;
            }
            index = (int)value;
            return true;
        }

        private static bool HasHexPrefix(string value) =>
            value.Length >= 2 && value[0] == '0' && (value[1] == 'x' || value[1] == 'X');

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
