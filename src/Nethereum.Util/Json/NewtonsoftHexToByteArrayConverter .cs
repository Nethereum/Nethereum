using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Util.Json
{
    public class NewtonsoftHexToByteArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(byte[]);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    => ((string)reader.Value).HexToByteArray();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => writer.WriteValue(((byte[])value).ToHex(true));
    }
}
