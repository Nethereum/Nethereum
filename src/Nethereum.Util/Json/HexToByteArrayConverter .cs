using System;
using System.Collections.Generic;
using System.Text;


namespace Nethereum.Util.Json
{
    using Nethereum.Hex.HexConvertors.Extensions;
    using System.Numerics;
#if NET6_0_OR_GREATER
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class HexToByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetString().HexToByteArray();

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToHex(true));
    }

    public class BigIntegerJsonConverter : System.Text.Json.Serialization.JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => BigInteger.Parse(reader.GetString()),
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? new BigInteger(l) : BigInteger.Parse(reader.GetDouble().ToString()),
                _ => throw new JsonException($"Unexpected token parsing BigInteger: {reader.TokenType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
}
#endif


}
