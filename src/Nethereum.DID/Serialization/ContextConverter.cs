using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.DID.Serialization
{
    public class ContextConverter : Newtonsoft.Json.JsonConverter<List<object>>
    {
        public override List<object> ReadJson(JsonReader reader, Type objectType, List<object> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var result = new List<object>();

            if (token.Type == JTokenType.String)
            {
                result.Add(token.Value<string>());
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)token)
                {
                    if (item.Type == JTokenType.String)
                    {
                        result.Add(item.Value<string>());
                    }
                    else
                    {
                        result.Add(item.ToObject<Dictionary<string, object>>(serializer));
                    }
                }
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, List<object> value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (value.Count == 1 && value[0] is string)
            {
                writer.WriteValue((string)value[0]);
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
            {
                if (item is string s)
                {
                    writer.WriteValue(s);
                }
                else
                {
                    serializer.Serialize(writer, item);
                }
            }
            writer.WriteEndArray();
        }
    }
}
