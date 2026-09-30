using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.CoreChain.Rpc
{
    public sealed class JsonRpcResponseConverter : JsonConverter<JsonRpcResponse>
    {
        public override JsonRpcResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var response = new JsonRpcResponse();

            if (root.TryGetProperty("jsonrpc", out var jsonrpcProperty) && jsonrpcProperty.ValueKind == JsonValueKind.String)
                response.Jsonrpc = jsonrpcProperty.GetString() ?? "2.0";

            if (root.TryGetProperty("id", out var idProperty))
                response.Id = ReadId(idProperty);

            if (root.TryGetProperty("error", out var errorProperty) && errorProperty.ValueKind != JsonValueKind.Null)
            {
                response.Error = errorProperty.Deserialize<JsonRpcError>(options);
            }
            else if (root.TryGetProperty("result", out var resultProperty))
            {
                response.Result = resultProperty.ValueKind == JsonValueKind.Null
                    ? null
                    : resultProperty.Deserialize<object>(options);
            }

            return response;
        }

        public override void Write(Utf8JsonWriter writer, JsonRpcResponse value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("jsonrpc", value.Jsonrpc ?? "2.0");

            writer.WritePropertyName("id");
            JsonSerializer.Serialize(writer, value.Id, options);

            if (value.Error != null)
            {
                writer.WritePropertyName("error");
                JsonSerializer.Serialize(writer, value.Error, options);
            }
            else
            {
                writer.WritePropertyName("result");
                JsonSerializer.Serialize(writer, value.Result, options);
            }

            writer.WriteEndObject();
        }

        private static object? ReadId(JsonElement idProperty)
        {
            return idProperty.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => idProperty.TryGetInt64(out var longId) ? (object)longId : idProperty.GetDouble(),
                JsonValueKind.String => idProperty.GetString(),
                _ => idProperty.Clone()
            };
        }
    }
}
