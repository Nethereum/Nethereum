#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class RpcParametersSystemTextJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var doc = JsonDocument.ParseValue(ref reader);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(doc.RootElement.GetRawText(), options);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var doc = JsonDocument.ParseValue(ref reader);
            return JsonSerializer.Deserialize<object[]>(doc.RootElement.GetRawText(), options);
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        throw new JsonException("Request parameters can only be an associative array, list, or null.");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
#endif
