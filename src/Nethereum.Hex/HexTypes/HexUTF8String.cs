using Nethereum.Hex.HexConvertors;
using Newtonsoft.Json;

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.Hex.HexTypes
{
    [Newtonsoft.Json.JsonConverter(typeof(HexRPCTypeJsonConverter<HexUTF8String, string>))] // Newtonsoft

#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonHexRPCTypeJsonConverter<HexUTF8String, string>))]
#endif
    public class HexUTF8String : HexRPCType<string>
    {
        private HexUTF8String() : base(new HexUTF8StringConvertor()) { }

        public HexUTF8String(string value) : base(value, new HexUTF8StringConvertor()) { }

        public static HexUTF8String CreateFromHex(string hex)
        {
            return new HexUTF8String { HexValue = hex };
        }
    }
}
