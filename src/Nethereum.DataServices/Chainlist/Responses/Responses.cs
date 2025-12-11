using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using System.Text.Json;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.DataServices.Chainlist.Responses
{

        public class ChainlistChainInfo
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chain")]
#else
            [JsonProperty("chain")]
#endif
            public string Chain { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("icon")]
#else
            [JsonProperty("icon")]
#endif
            [JsonConverter(typeof(ChainlistFlexibleStringConverter))]
            public string Icon { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("rpc")]
#else
            [JsonProperty("rpc")]
#endif
            public List<ChainlistRpc> Rpc { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("features")]
#else
            [JsonProperty("features")]
#endif
            public List<ChainlistFeature> Features { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("faucets")]
#else
            [JsonProperty("faucets")]
#endif
            [JsonConverter(typeof(ChainlistFlexibleStringListConverter))]
            public List<string> Faucets { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("nativeCurrency")]
#else
            [JsonProperty("nativeCurrency")]
#endif
            public ChainlistNativeCurrency NativeCurrency { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("infoURL")]
#else
            [JsonProperty("infoURL")]
#endif
            public string InfoURL { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("shortName")]
#else
            [JsonProperty("shortName")]
#endif
            public string ShortName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chainId")]
#else
            [JsonProperty("chainId")]
#endif
            public long ChainId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("networkId")]
#else
            [JsonProperty("networkId")]
#endif
            public long NetworkId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("slip44")]
#else
            [JsonProperty("slip44")]
#endif
            public long? Slip44 { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ens")]
#else
            [JsonProperty("ens")]
#endif
            public ChainlistEns Ens { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("explorers")]
#else
            [JsonProperty("explorers")]
#endif
            public List<ChainlistExplorer> Explorers { get; set; }
        }

        public class ChainlistRpc
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("url")]
#else
            [JsonProperty("url")]
#endif
            public string Url { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("tracking")]
#else
            [JsonProperty("tracking")]
#endif
            public string Tracking { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("isOpenSource")]
#else
            [JsonProperty("isOpenSource")]
#endif
            public bool? IsOpenSource { get; set; }
        }

        [JsonConverter(typeof(ChainlistFeatureConverter))]
        public class ChainlistFeature
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }
        }
#if NET8_0_OR_GREATER
        public class ChainlistFeatureConverter : JsonConverter<ChainlistFeature>
        {
            public override ChainlistFeature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    return new ChainlistFeature { Name = reader.GetString() ?? string.Empty };
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    using var document = JsonDocument.ParseValue(ref reader);
                    if (document.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        return new ChainlistFeature { Name = nameProp.GetString() ?? string.Empty };
                    }
                }

                throw new JsonException("Invalid Chainlist feature entry");
            }

            public override void Write(Utf8JsonWriter writer, ChainlistFeature value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("name", value?.Name);
                writer.WriteEndObject();
            }
        }
#else
        public class ChainlistFeatureConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(ChainlistFeature);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.String:
                        return new ChainlistFeature { Name = reader.Value?.ToString() ?? string.Empty };
                    case JsonToken.StartObject:
                        var obj = JObject.Load(reader);
                        return new ChainlistFeature { Name = obj["name"]?.ToString() ?? string.Empty };
                    default:
                        throw new JsonSerializationException("Invalid Chainlist feature entry");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var feature = value as ChainlistFeature;
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                writer.WriteValue(feature?.Name);
                writer.WriteEnd();
            }
        }
#endif

#if NET8_0_OR_GREATER
        public class ChainlistFlexibleStringConverter : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString() ?? string.Empty;
                }

                if (reader.TokenType == JsonTokenType.Null)
                {
                    return string.Empty;
                }

                using var document = JsonDocument.ParseValue(ref reader);
                return document.RootElement.ToString();
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
#else
        public class ChainlistFlexibleStringConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(string);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    return reader.Value?.ToString();
                }

                if (reader.TokenType == JsonToken.Null)
                {
                    return string.Empty;
                }

                return JToken.ReadFrom(reader).ToString();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(value?.ToString());
            }
        }
#endif

#if NET8_0_OR_GREATER
        public class ChainlistFlexibleStringListConverter : JsonConverter<List<string>>
        {
            public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return new List<string>();
                }

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            list.Add(reader.GetString() ?? string.Empty);
                        }
                        else if (reader.TokenType == JsonTokenType.Null)
                        {
                            list.Add(string.Empty);
                        }
                        else
                        {
                            using var element = JsonDocument.ParseValue(ref reader);
                            list.Add(element.RootElement.ToString());
                        }
                    }

                    return list;
                }

                using var singleValue = JsonDocument.ParseValue(ref reader);
                return new List<string> { singleValue.RootElement.ToString() };
            }

            public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                if (value != null)
                {
                    foreach (var entry in value)
                    {
                        writer.WriteStringValue(entry);
                    }
                }
                writer.WriteEndArray();
            }
        }
#else
        public class ChainlistFlexibleStringListConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return new List<string>();
                }

                if (reader.TokenType == JsonToken.StartArray)
                {
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        if (reader.TokenType == JsonToken.String)
                        {
                            list.Add(reader.Value?.ToString() ?? string.Empty);
                        }
                        else if (reader.TokenType == JsonToken.Null)
                        {
                            list.Add(string.Empty);
                        }
                        else
                        {
                            list.Add(JToken.ReadFrom(reader).ToString());
                        }
                    }

                    return list;
                }

                return new List<string> { JToken.ReadFrom(reader).ToString() };
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                if (value is List<string> list)
                {
                    foreach (var entry in list)
                    {
                        writer.WriteValue(entry);
                    }
                }
                writer.WriteEndArray();
            }
        }
#endif

        public class ChainlistNativeCurrency
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("symbol")]
#else
            [JsonProperty("symbol")]
#endif
            public string Symbol { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("decimals")]
#else
            [JsonProperty("decimals")]
#endif
            public long Decimals { get; set; }
        }

        public class ChainlistEns
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("registry")]
#else
            [JsonProperty("registry")]
#endif
            public string Registry { get; set; }
        }

        public class ChainlistExplorer
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("url")]
#else
            [JsonProperty("url")]
#endif
            public string Url { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("standard")]
#else
            [JsonProperty("standard")]
#endif
            public string Standard { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("icon")]
#else
            [JsonProperty("icon")]
#endif
            public string Icon { get; set; }
        }
    }

