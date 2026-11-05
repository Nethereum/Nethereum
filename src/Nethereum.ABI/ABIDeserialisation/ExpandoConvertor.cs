using System;
using System.Collections.Generic;
//using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.ABI.ABIDeserialisation
{
    /// <summary>
    ///     This is a replication (copy) of Newtonsoft ExpandoObjectConverter to allow for PCL compilaton
    /// </summary>
    public class ExpandoObjectConverter : JsonConverter
    {
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        ///     Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        ///     <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IDictionary<string, object>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            return ReadValue(reader, 0);
        }

        internal const int MaxJsonDepth = 2 * AbiParsingLimits.MaxNestingDepth + 4;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // can write is set to false
        }

        private bool IsPrimitiveToken(JsonToken token)
        {
            switch (token)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Undefined:
                case JsonToken.Null:
                case JsonToken.Date:
                case JsonToken.Bytes:
                    return true;
                default:
                    return false;
            }
        }

        private object ReadList(JsonReader reader, int depth)
        {
            if (depth > MaxJsonDepth) throw new JsonReaderException("ABI JSON nesting exceeds the maximum depth");
            IList<object> list = new List<object>();

            while (reader.Read())
                switch (reader.TokenType)
                {
                    case JsonToken.Comment:
                        break;
                    default:
                        var v = ReadValue(reader, depth + 1);

                        list.Add(v);
                        break;
                    case JsonToken.EndArray:
                        return list;
                }

            throw new Exception("Unexpected end.");
        }

        private object ReadObject(JsonReader reader, int depth)
        {
            if (depth > MaxJsonDepth) throw new JsonReaderException("ABI JSON nesting exceeds the maximum depth");
            IDictionary<string, object> expandoObject = new Dictionary<string, object>();

            while (reader.Read())
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var propertyName = reader.Value.ToString();

                        if (!reader.Read())
                            throw new Exception("Unexpected end.");

                        var v = ReadValue(reader, depth + 1);

                        expandoObject[propertyName] = v;
                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.EndObject:
                        return expandoObject;
                }

            throw new Exception("Unexpected end.");
        }

        private object ReadValue(JsonReader reader, int depth)
        {
            while (reader.TokenType == JsonToken.Comment)
                if (!reader.Read())
                    throw new Exception("Unexpected end.");

            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    return ReadObject(reader, depth);
                case JsonToken.StartArray:
                    return ReadList(reader, depth);
                default:
                    if (IsPrimitiveToken(reader.TokenType))
                        return reader.Value;

                    throw new Exception("Unexpected token when converting ExpandoObject");
            }
        }
    }

    // Converting ABI as JArray, JObject into ExpandoObject style
    // This is only available for ABI, not generic JObject/ExpandoObject conversion
    public class JObjectToExpandoConverter {
        public List<IDictionary<string, object>> JObjectArray(JArray array) {
            var l = new List<IDictionary<string, object>>();
            foreach(JObject obj in array)
                l.Add(JObject(obj, 1));
            return l;
        }
        public List<object> JArray(JArray array) {
            return JArray(array, 0);
        }
        public IDictionary<string, object> JObject(JObject obj) {
            return JObject(obj, 0);
        }
        public object JToken(JToken token) {
            return JToken(token, 0);
        }
        private List<object> JArray(JArray array, int depth) {
            EnsureDepth(depth);
            var l = new List<object>();
            foreach(JToken token in array)
                l.Add(JToken(token, depth + 1));
            return l;
        }
        private IDictionary<string, object> JObject(JObject obj, int depth) {
            EnsureDepth(depth);
            var dic = new Dictionary<string, object>();
            foreach(var pair in obj) {
                dic[pair.Key] = JToken(pair.Value, depth + 1);
            }
            return dic;
        }
        private static void EnsureDepth(int depth) {
            if (depth > ExpandoObjectConverter.MaxJsonDepth)
                throw new JsonReaderException("ABI JSON nesting exceeds the maximum depth");
        }
        private object JToken(JToken token, int depth) {
            if(token is JObject)
                return JObject((JObject)token, depth);
            else if(token is JArray)
                return JArray((JArray)token, depth);
            else if(token.Type == JTokenType.String)
                return token.Value<string>();
            else if(token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            else if(token.Type == JTokenType.Integer)
                return token.Value<int>();
            else
                throw new Exception("unexpected token type " + token.Type);
        }
    }
}