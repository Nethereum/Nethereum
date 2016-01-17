using System;
using System.Numerics;
using Newtonsoft.Json;

namespace Ethereum.RPC
{
    [JsonConverter(typeof(HexRPCTypeJsonConverter<HexBigInteger, BigInteger>))]
    public class HexBigInteger:HexRPCType<BigInteger>
    {
       

        public HexBigInteger(string hex) : base(new HexBigIntegerBigEndianConvertor(), hex)
        {
           
        }

        public HexBigInteger(BigInteger value) : base(value, new HexBigIntegerBigEndianConvertor())
        {

        }


    }

    [JsonConverter(typeof(HexRPCTypeJsonConverter<HexString, String>))]
    public class HexString : HexRPCType<String>
    {
        public static HexString CreateFromHex(string hex)
        {
            return new HexString() { HexValue = hex };
        }

        private HexString() : base(new HexUTF8StringConvertor())
        {

        }

        public HexString(String value) : base(value, new HexUTF8StringConvertor())
        {

        }
    }

}