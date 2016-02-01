using System;
using Nethereum.Hex.HexConvertors;
using Newtonsoft.Json;

namespace Nethereum.Hex.HexTypes
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