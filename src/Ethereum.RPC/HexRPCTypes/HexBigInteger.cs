using System.Numerics;
using Newtonsoft.Json;

namespace Ethereum.RPC
{
    [JsonConverter(typeof(HexRPCTypeJsonConverter<HexBigInteger, BigInteger>))]
    public class HexBigInteger:HexRPCType<BigInteger>
    {
      
        public HexBigInteger(string hexValue) : base(hexValue, new HexBigIntegerBigEndianConvertor())
        {
           
        }

        public HexBigInteger(BigInteger value) : base(value, new HexBigIntegerBigEndianConvertor())
        {
            
        }
    }
}