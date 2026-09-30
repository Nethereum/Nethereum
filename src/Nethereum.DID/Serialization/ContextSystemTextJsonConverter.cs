#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.DID.Serialization
{
    public class ContextSystemTextJsonConverter : JsonConverter<List<object>>
    {
        public override List<object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new List<object>();

            if (reader.TokenType == JsonTokenType.String)
            {
                result.Add(reader.GetString());
                return result;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;

                    if (reader.TokenType == JsonTokenType.String)
                    {
                        result.Add(reader.GetString());
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        var element = JsonDocument.ParseValue(ref reader).RootElement;
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in element.EnumerateObject())
                        {
                            dict[prop.Name] = GetObjectValue(prop.Value);
                        }
                        result.Add(dict);
                    }
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<object> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Count == 1 && value[0] is string s)
            {
                writer.WriteStringValue(s);
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
            {
                if (item is string str)
                {
                    writer.WriteStringValue(str);
                }
                else
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
            }
            writer.WriteEndArray();
        }

        private static object GetObjectValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l))
                        return l;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.Clone();
            }
        }
    }
}
#endif
