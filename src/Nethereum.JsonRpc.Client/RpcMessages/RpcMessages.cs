using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
#endif

namespace Nethereum.JsonRpc.Client.RpcMessages
{
    public static class RpcResponseExtensions
    {
        public static T GetResult<T>(this RpcResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        {
            return ConvertToNewtonsoft<T>(response.Result, returnDefaultIfNull, settings);
        }


        public static T GetStreamingResult<T>(this RpcStreamingResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        {
            if (response.Method == null)
            {
                return GetResult<T>(response, returnDefaultIfNull, settings);
            }

            return ConvertToNewtonsoft<T>(response.Params?.Result, returnDefaultIfNull, settings);
        }


#if NET6_0_OR_GREATER

        public static T GetResultSTJ<T>(this RpcResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerOptions options = null)
        {
            return ConvertToSTJ<T>(response.Result, returnDefaultIfNull, options);
        }

        private static T ConvertToSTJ<T>(object result, bool returnDefaultIfNull, JsonSerializerOptions options = null)
        {
            if (result == null)
            {
                if (!returnDefaultIfNull && default(T) != null)
                {
                    throw new Exception($"Unable to convert the result (null) to type {typeof(T)}");
                }
                return default;
            }

            try
            {
                if (result is string str)
                {
                    if (typeof(T).IsSubclassOfGeneric(typeof(HexRPCType<>)) && str.IsHex())
                    {
                        return (T)HexTypeFactory.CreateFromHex<T>(str);
                    }

                    var bytes = Encoding.UTF8.GetBytes($"\"{str}\"");
                    return System.Text.Json.JsonSerializer.Deserialize<T>(bytes, options);
                }

                if (result is JsonElement jsonElement)
                {
                    return System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement, options);
                }

                if (result is System.Text.Json.Nodes.JsonNode jsonNode)
                {
                    return jsonNode.Deserialize<T>(options);
                }

                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Invalid format when trying to convert the result to type {typeof(T)}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to convert the result to type {typeof(T)}", ex);
            }
        }

        private static bool IsSubclassOfGeneric(this Type type, Type genericBase)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericBase)
                {
                    return true;
                }
                type = type.BaseType!;
            }
            return false;
        }
#endif
        private static T ConvertToNewtonsoft<T>(object result, bool returnDefaultIfNull, JsonSerializerSettings settings = null)
        {
            if (result == null)
            {
                if (!returnDefaultIfNull && default(T) != null)
                    throw new Exception($"Unable to convert the result (null) to type {typeof(T)}");
                return default;
            }

            try
            {
                JToken token = result as JToken ?? JToken.FromObject(result);

                if (settings == null)
                {
                    return token.ToObject<T>();
                }
                else
                {
                    var jsonSerializer = Newtonsoft.Json.JsonSerializer.Create(settings);
                    return token.ToObject<T>(jsonSerializer);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Invalid format when trying to convert the result to type {typeof(T)}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to convert the result to type {typeof(T)}", ex);
            }
        }

 
    }


        //public static class RpcResponseExtensions
        //{
        //    /// <summary>
        //    /// Parses and returns the result of the rpc response as the type specified. 
        //    /// Otherwise throws a parsing exception
        //    /// </summary>
        //    /// <typeparam name="T">Type of object to parse the response as</typeparam>
        //    /// <param name="response">Rpc response object</param>
        //    /// <param name="returnDefaultIfNull">Returns the type's default value if the result is null. Otherwise throws parsing exception</param>
        //    /// <returns>Result of response as type specified</returns>
        //    public static T GetResult<T>(this RpcResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        //    {
        //        if (response.Result == null)
        //        {
        //            if (!returnDefaultIfNull && default(T) != null)
        //            {
        //                throw new Exception("Unable to convert the result (null) to type " + typeof(T));
        //            }
        //            return default(T);
        //        }
        //        try
        //        {
        //            if (settings == null)
        //            {
        //                return response.Result.ToObject<T>();
        //            }
        //            else
        //            {
        //                JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
        //                return response.Result.ToObject<T>(jsonSerializer);
        //            }
        //        }
        //        catch (FormatException ex)
        //        {
        //            throw new FormatException("Invalid format when trying to convert the result to type " + typeof(T), ex);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new Exception("Unable to convert the result to type " + typeof(T), ex);
        //        }
        //    }

        //    /// <summary>
        //    /// Parses and returns the result of the rpc streaming response as the type specified. 
        //    /// Otherwise throws a parsing exception
        //    /// </summary>
        //    /// <typeparam name="T">Type of object to parse the response as</typeparam>
        //    /// <param name="response">Rpc response object</param>
        //    /// <param name="returnDefaultIfNull">Returns the type's default value if the result is null. Otherwise throws parsing exception</param>
        //    /// <returns>Result of response as type specified</returns>
        //    public static T GetStreamingResult<T>(this RpcStreamingResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        //    {
        //        if(response.Method == null) {
        //            return GetResult<T>(response, returnDefaultIfNull, settings);
        //        }

        //        if (response.Params.Result == null)
        //        {
        //            if (!returnDefaultIfNull && default(T) != null)
        //            {
        //                throw new Exception("Unable to convert the result (null) to type " + typeof(T));
        //            }
        //            return default(T);
        //        }
        //        try
        //        {
        //            if (settings == null)
        //            {
        //                return response.Params.Result.ToObject<T>();
        //            }
        //            else
        //            {
        //                JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
        //                return response.Params.Result.ToObject<T>(jsonSerializer);
        //            }
        //        }
        //        catch (FormatException ex)
        //        {
        //            throw new FormatException("Invalid format when trying to convert the result to type " + typeof(T), ex);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new Exception("Unable to convert the result to type " + typeof(T), ex);
        //        }
        //    }
        //}

       

    [JsonObject]
    public class RpcStreamingResponseMessage : RpcResponseMessage
    {
        [Newtonsoft.Json.JsonConstructor]
        protected RpcStreamingResponseMessage()
        {
            
        }

        /// <param name="error">Request error</param>
        public RpcStreamingResponseMessage(RpcError error) : base()
        {
            this.Error = error;
        }

        /// <param name="method">method name</param>
        /// <param name="params">Response result object</param>
        public RpcStreamingResponseMessage(string method, RpcStreamingParams @params) : base()
        {
            this.Method = method;
            this.Params = @params;
        }

        /// <summary>
        /// Rpc request version (Required)
        /// </summary>
        [JsonProperty("method", Required = Required.Default)]
        public string Method { get; private set; }

        /// <summary>
        /// Reponse result object (Required)
        /// </summary>
        [JsonProperty("params", Required = Required.Default)]
        public RpcStreamingParams Params { get; private set; }

    }

    [JsonObject]
    public class RpcStreamingParams
    {
        [Newtonsoft.Json.JsonConstructor]
        public RpcStreamingParams()
        {

        }

        /// <summary>
        /// Response Subscription Id (Required)
        /// </summary>
        [JsonProperty("subscription", Required = Required.Always)]
        public string Subscription { get; private set; }

        /// <summary>
        /// Reponse result object (Required)
        /// </summary>
        [JsonProperty("result", Required = Required.Always)]
        public JToken Result { get; private set; }
    }

    [JsonObject]
    public class RpcRequestMessage
    {
        [Newtonsoft.Json.JsonConstructor]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonConstructor]
#endif
        private RpcRequestMessage() { }

        public RpcRequestMessage(object id, string method, params object[] parameterList)
        {
            Id = id;
            JsonRpcVersion = "2.0";
            Method = method;
            RawParameters = parameterList;
        }

        public RpcRequestMessage(object id, string method, Dictionary<string, object> parameterMap)
        {
            Id = id;
            JsonRpcVersion = "2.0";
            Method = method;
            RawParameters = parameterMap;
        }

        [JsonProperty("id")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("id")]
#endif
        public object Id { get; set; }

        [JsonProperty("jsonrpc", Required = Required.Always)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("jsonrpc")]
#endif
        public string JsonRpcVersion { get; set; }

        [JsonProperty("method", Required = Required.Always)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("method")]
#endif
        public string Method { get; set; }

        [JsonProperty("params")]
        [Newtonsoft.Json.JsonConverter(typeof(RpcParametersJsonConverter))] // Newtonsoft
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonConverter(typeof(RpcParametersSystemTextJsonConverter))] // STJ
        [JsonPropertyName("params")]
#endif
        public object RawParameters { get; set; }
    }
    /// <summary>
    /// Json converter for Rpc parameters
    /// </summary>
    public class RpcParametersJsonConverter : Newtonsoft.Json.JsonConverter
    {
        /// <summary>
        /// Writes the value of the parameters to json format
        /// </summary>
        /// <param name="writer">Json writer</param>
        /// <param name="value">Value to be converted to json format</param>
        /// <param name="serializer">Json serializer</param>
        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
          
            serializer.Serialize(writer, value);
        }

        /// <summary>
        /// Read the json format and return the correct object type/value for it
        /// </summary>
        /// <param name="reader">Json reader</param>
        /// <param name="objectType">Type of property being set</param>
        /// <param name="existingValue">The current value of the property being set</param>
        /// <param name="serializer">Json serializer</param>
        /// <returns>The object value of the converted json value</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    try
                    {
                        JObject jObject = JObject.Load(reader);
                        return jObject.ToObject<Dictionary<string, object>>();
                    }
                    catch (Exception)
                    {
                        throw new Exception("Request parameters can only be an associative array, list or null.");
                    }
                case JsonToken.StartArray:
                    return JArray.Load(reader).ToObject<object[]>(serializer);
                case JsonToken.Null:
                    return null;
            }
            throw new Exception("Request parameters can only be an associative array, list or null.");
        }

        /// <summary>
        /// Determines if the type can be convertered with this converter
        /// </summary>
        /// <param name="objectType">Type of the object</param>
        /// <returns>True if the converter converts the specified type, otherwise False</returns>
        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }

    [JsonObject]
    public class RpcResponseMessage
    {

        [Newtonsoft.Json.JsonConstructor]
        public RpcResponseMessage()
        {
            JsonRpcVersion = "2.0";
        }

        public RpcResponseMessage(object id)
        {
            Id = id;
            JsonRpcVersion = "2.0";
        }

        public RpcResponseMessage(object id, RpcError error) : this(id)
        {
            Error = error;
        }

        public RpcResponseMessage(object id, object result) : this(id)
        {
            ResultNewtonsoft = result;
#if NET6_0_OR_GREATER
            if (result is JsonElement element)
            {
                ResultSystemTextJson = element;
            }
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result);
                ResultSystemTextJson = System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
            }
#endif
        }

        [JsonProperty("id")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("id")]
#endif
        public object Id { get; set; }

        [JsonProperty("jsonrpc")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("jsonrpc")]
#endif
        public string JsonRpcVersion { get; set; }

#if NET6_0_OR_GREATER
        [JsonPropertyName("result")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement ResultSystemTextJson { get; set; }
#endif

        [JsonProperty("result")]
        public object ResultNewtonsoft { get; set; }

        [Newtonsoft.Json.JsonIgnore]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.Always)]
#endif
        public object Result =>
#if NET6_0_OR_GREATER
            ResultSystemTextJson.ValueKind != JsonValueKind.Undefined ? ResultSystemTextJson : ResultNewtonsoft;
#else
        ResultNewtonsoft;
#endif

        [JsonProperty("error")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("error")]
#endif
        public RpcError Error { get; set; }

        [Newtonsoft.Json.JsonIgnore]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.Always)]
#endif
        public bool HasError => Error != null;
    }

    [JsonObject]
    public class RpcError
    {
        [Newtonsoft.Json.JsonConstructor]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonConstructor]
#endif
        public RpcError() { }

        /// <summary>
        /// Rpc error code
        /// </summary>
        [JsonProperty("code")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("code")]
#endif
        public int Code { get; set; }

        /// <summary>
        /// Error message (Required)
        /// </summary>
        [JsonProperty("message", Required = Required.Always)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("message")]
#endif
        public string Message { get; set; }

        /// <summary>
        /// Error data (Optional): may be a hex string, an object, or null
        /// </summary>
        [JsonProperty("data")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("data")]
#endif
        public object Data { get; set; }
    }

}

