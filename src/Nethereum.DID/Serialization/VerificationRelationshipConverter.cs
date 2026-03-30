using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.DID.Serialization
{
    public class VerificationRelationshipConverter : Newtonsoft.Json.JsonConverter<VerificationRelationship>
    {
        public override VerificationRelationship ReadJson(JsonReader reader, Type objectType, VerificationRelationship existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.String)
            {
                return new VerificationRelationship(token.Value<string>());
            }

            if (token.Type == JTokenType.Object)
            {
                var method = token.ToObject<VerificationMethod>(serializer);
                return new VerificationRelationship(method);
            }

            throw new JsonSerializationException("Unexpected token type for VerificationRelationship: " + token.Type);
        }

        public override void WriteJson(JsonWriter writer, VerificationRelationship value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (value.IsReference)
            {
                writer.WriteValue(value.VerificationMethodReference);
            }
            else if (value.IsEmbedded)
            {
                serializer.Serialize(writer, value.EmbeddedVerificationMethod);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
