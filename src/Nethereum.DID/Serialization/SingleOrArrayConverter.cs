using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.DID.Serialization
{
    public class SingleOrArrayConverter : Newtonsoft.Json.JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var result = new List<string>();

            if (token.Type == JTokenType.String)
            {
                result.Add(token.Value<string>());
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)token)
                {
                    result.Add(item.Value<string>());
                }
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (value.Count == 1)
            {
                writer.WriteValue(value[0]);
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteValue(item);
            }
            writer.WriteEndArray();
        }
    }
}
