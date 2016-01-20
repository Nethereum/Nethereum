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
}