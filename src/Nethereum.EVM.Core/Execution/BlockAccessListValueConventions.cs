using System;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public static class BlockAccessListValueConventions
    {
        public static ulong NonceOf(EvmUInt256 value)
        {
            if (value.U1 != 0 || value.U2 != 0 || value.U3 != 0)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Nonce exceeds 64 bits; the account state is malformed.");
            return value.U0;
        }

        public static bool SameCode(byte[]? a, byte[]? b)
        {
            if (a == null || b == null) return (a == null || a.Length == 0) && (b == null || b.Length == 0);
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public static string NormaliseAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("An account in the block access list has no address.", nameof(address));
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }
    }
}
