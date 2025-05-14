#if NET6_0_OR_GREATER

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.Hex.HexTypes
{
    /// <summary>
    /// System.Text.Json converter for HexRPCType&lt;TValue&gt; types like HexBigInteger.
    /// Mirrors the Newtonsoft.Json behaviour.
    /// </summary>
    public sealed class SystemTextJsonHexRPCTypeJsonConverter<T, TValue> : JsonConverter<T>
        where T : HexRPCType<TValue>
    {
        public SystemTextJsonHexRPCTypeJsonConverter() { }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.HexValue);
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var hex = reader.GetString()!;
                return (T)HexTypeFactory.CreateFromHex<TValue>(hex);
            }

            // fallback for numeric values (e.g. 123 → 0x7b)
            object numberValue = reader.TokenType switch
            {
                JsonTokenType.Number when reader.TryGetInt64(out var i) => i,
                JsonTokenType.Number => reader.GetDecimal(),
                _ => throw new JsonException($"Unexpected token {reader.TokenType} for {typeof(T).Name}")
            };

            return (T)HexTypeFactory.CreateFromObject<TValue>(numberValue);
        }
    }
}

#endif

