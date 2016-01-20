using System;
using Newtonsoft.Json;

namespace Ethereum.RPC
{
    [JsonConverter(typeof(HexRPCTypeJsonConverter<HexString, string>))]
    public class HexString : HexRPCType<string>
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