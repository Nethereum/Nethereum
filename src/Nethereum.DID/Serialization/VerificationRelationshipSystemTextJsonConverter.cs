#if NET6_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.DID.Serialization
{
    public class VerificationRelationshipSystemTextJsonConverter : JsonConverter<VerificationRelationship>
    {
        public override VerificationRelationship Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new VerificationRelationship(reader.GetString());
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var method = JsonSerializer.Deserialize<VerificationMethod>(ref reader, options);
                return new VerificationRelationship(method);
            }

            throw new JsonException("Unexpected token type for VerificationRelationship: " + reader.TokenType);
        }

        public override void Write(Utf8JsonWriter writer, VerificationRelationship value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.IsReference)
            {
                writer.WriteStringValue(value.VerificationMethodReference);
            }
            else if (value.IsEmbedded)
            {
                JsonSerializer.Serialize(writer, value.EmbeddedVerificationMethod, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
#endif
