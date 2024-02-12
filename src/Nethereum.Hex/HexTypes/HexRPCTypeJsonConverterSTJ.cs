using System;

namespace Nethereum.Hex.HexTypes
{
#if NET6_0_OR_GREATER
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class HexRPCTypeJsonConverterSTJ<T, TValue> : JsonConverter<T> where T : HexRPCType<TValue>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string hexString = reader.GetString();
                return HexTypeFactory.CreateFromHex<TValue>(hexString) as T;
            }

            TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            return HexTypeFactory.CreateFromObject<TValue>(value) as T;
           
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.HexValue);
        }
    }
#endif
}