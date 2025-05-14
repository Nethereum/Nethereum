using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors;
using Newtonsoft.Json;

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.Hex.HexTypes
{
    [Newtonsoft.Json.JsonConverter(typeof(HexRPCTypeJsonConverter<HexBigInteger, BigInteger>))]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonHexRPCTypeJsonConverter<HexBigInteger, BigInteger>))]
#endif
    public class HexBigInteger : HexRPCType<BigInteger>
    {
        public HexBigInteger(string hex) : base(new HexBigIntegerBigEndianConvertor(), hex)
        {
        }

        public HexBigInteger(BigInteger value) : base(value, new HexBigIntegerBigEndianConvertor())
        {
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}