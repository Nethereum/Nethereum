using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Util
{
    public readonly partial struct EvmUInt256
    {
        // --- Constructors from BigInteger ---

        public EvmUInt256(BigInteger value)
        {
            if (value.Sign < 0)
                value += BigIntegerExtensions.TWO_256;
#if NETCOREAPP3_0_OR_GREATER
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
#else
            var bytes = value.ToByteArray();
            Array.Reverse(bytes);
            if (bytes.Length > 0 && bytes[0] == 0)
            {
                var trimmed = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, trimmed, 0, trimmed.Length);
                bytes = trimmed;
            }
#endif
            var tmp = FromBigEndian(bytes);
            _u3 = tmp._u3; _u2 = tmp._u2; _u1 = tmp._u1; _u0 = tmp._u0;
        }

        // --- Implicit conversions ---

        public static implicit operator EvmUInt256(BigInteger value) => new EvmUInt256(value);

        public static implicit operator BigInteger(EvmUInt256 value)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new BigInteger(value.ToBigEndian(), isUnsigned: true, isBigEndian: true);
#else
            var bytes = value.ToBigEndian();
            var le = new byte[33];
            for (int i = 0; i < 32; i++)
                le[31 - i] = bytes[i];
            le[32] = 0;
            return new BigInteger(le);
#endif
        }

        public static implicit operator EvmUInt256(HexBigInteger value)
        {
            if (value == null) return Zero;
            var hex = value.HexValue;
            if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0") return Zero;
            return FromBigEndian(hex.HexToByteArray());
        }

        // Comparison and arithmetic with BigInteger is handled via implicit conversions
        // Explicit operators removed to avoid ambiguity with int/long operands
    }
}
