using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Util
{
    public readonly partial struct EvmInt256
    {
        // --- Constructor from BigInteger ---

        public EvmInt256(BigInteger value)
        {
            _value = new EvmUInt256(value);
        }

        // --- Implicit conversions ---

        public static implicit operator EvmInt256(BigInteger value) => new EvmInt256(value);

        public static implicit operator BigInteger(EvmInt256 value) => (BigInteger)value._value;

        public static implicit operator EvmInt256(HexBigInteger value)
        {
            if (value == null) return Zero;
            return new EvmInt256((EvmUInt256)value);
        }

        // Comparison with BigInteger is handled via implicit conversions
        // Explicit operators removed to avoid ambiguity with int/long operands
    }
}
