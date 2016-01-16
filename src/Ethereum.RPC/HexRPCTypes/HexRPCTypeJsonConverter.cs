using System;
using Newtonsoft.Json;

namespace Ethereum.RPC
{
    public class HexRPCTypeJsonConverter<T, TValue> : JsonConverter  where T : HexRPCType<TValue>
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            T hexRPCType = (T)value;
            writer.WriteValue(hexRPCType.HexValue);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return HexRPCTypeFactory.GetHexRPCType<TValue>((string)reader.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(T);
        }

    }
}