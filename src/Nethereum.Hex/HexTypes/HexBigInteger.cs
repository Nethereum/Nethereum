using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors;
using Newtonsoft.Json;

namespace Nethereum.Hex.HexTypes
{
    [JsonConverter(typeof(HexRPCTypeJsonConverter<HexBigInteger, BigInteger>))]
    public class HexBigInteger : HexRPCType<BigInteger>, IEquatable<HexBigInteger>
    {
        public HexBigInteger(string hex) : base(new HexBigIntegerBigEndianConvertor(), hex)
        {
        }

        public HexBigInteger(BigInteger value) : base(value, new HexBigIntegerBigEndianConvertor())
        {
        }

        public override bool Equals(object obj)
        {
            if (obj is HexBigInteger val)
            {
                return Equals(val);
            }

            return false;
        }

        public bool Equals(HexBigInteger other)
        {
            return other == null ? false : other.Value == Value;
        }

        public static bool operator == (HexBigInteger lhs, HexBigInteger rhs)
        {
            // Check for null on left side.
            if (Object.ReferenceEquals(lhs, null))
            {
                if (Object.ReferenceEquals(rhs, null))
                {
                    // null == null = true.
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator != (HexBigInteger lhs, HexBigInteger rhs)
        {
            return !(lhs == rhs);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}