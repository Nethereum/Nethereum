using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.JsonRpc.Client.RpcMessages
{
    /*

RPC Model simplified and downported to net351 from EdjCase.JsonRPC.Core
https://github.com/edjCase/JsonRpc/tree/master/src/EdjCase.JsonRpc.Core

The MIT License (MIT)
Copyright(c) 2015 Gekctek

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
    public static class RpcResponseExtensions
    {
        /// <summary>
        /// Parses and returns the result of the rpc response as the type specified. 
        /// Otherwise throws a parsing exception
        /// </summary>
        /// <typeparam name="T">Type of object to parse the response as</typeparam>
        /// <param name="response">Rpc response object</param>
        /// <param name="returnDefaultIfNull">Returns the type's default value if the result is null. Otherwise throws parsing exception</param>
        /// <returns>Result of response as type specified</returns>
        public static T GetResult<T>(this RpcResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        {
            if (response.Result == null)
            {
                if (!returnDefaultIfNull && default(T) != null)
                {
                    throw new Exception("Unable to convert the result (null) to type " + typeof(T));
                }
                return default(T);
            }
            try
            {
                if (settings == null)
                {
                    return response.Result.ToObject<T>();
                }
                else
                {
                    JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
                    return response.Result.ToObject<T>(jsonSerializer);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException("Invalid format when trying to convert the result to type " + typeof(T), ex);
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to convert the result to type " + typeof(T), ex);
            }
        }

        /// <summary>
        /// Parses and returns the result of the rpc streaming response as the type specified. 
        /// Otherwise throws a parsing exception
        /// </summary>
        /// <typeparam name="T">Type of object to parse the response as</typeparam>
        /// <param name="response">Rpc response object</param>
        /// <param name="returnDefaultIfNull">Returns the type's default value if the result is null. Otherwise throws parsing exception</param>
        /// <returns>Result of response as type specified</returns>
        public static T GetStreamingResult<T>(this RpcStreamingResponseMessage response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
        {
            if(response.Method == null) {
                return GetResult<T>(response, returnDefaultIfNull, settings);
            }

            if (response.Params.Result == null)
            {
                if (!returnDefaultIfNull && default(T) != null)
                {
                    throw new Exception("Unable to convert the result (null) to type " + typeof(T));
                }
                return default(T);
            }
            try
            {
                if (settings == null)
                {
                    return response.Params.Result.ToObject<T>();
                }
                else
                {
                    JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
                    return response.Params.Result.ToObject<T>(jsonSerializer);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException("Invalid format when trying to convert the result to type " + typeof(T), ex);
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to convert the result to type " + typeof(T), ex);
            }
        }
    }

    [JsonObject]
    public class RpcResponseMessage
    {
        [JsonConstructor]
        protected RpcResponseMessage()
        {
            JsonRpcVersion = "2.0";
        }

        /// <param name="id">Request id</param>
        protected RpcResponseMessage(object id)
        {
            this.Id = id;
            JsonRpcVersion = "2.0";
        }

        /// <param name="id">Request id</param>
        /// <param name="error">Request error</param>
        public RpcResponseMessage(object id, RpcError error) : this(id)
        {
            this.Error = error;
        }

        /// <param name="id">Request id</param>
        /// <param name="result">Response result object</param>
        public RpcResponseMessage(object id, JToken result) : this(id)
        {
            this.Result = result;
        }

        /// <summary>
        /// Request id (Required but nullable)
        /// </summary>
        [JsonProperty("id", Required = Required.Default)]
        public object Id { get; private set; }

        /// <summary>
        /// Rpc request version (Required)
        /// </summary>
        [JsonProperty("jsonrpc", Required = Required.Always)]
        public string JsonRpcVersion { get; private set; }

        /// <summary>
        /// Reponse result object (Required)
        /// </summary>
        [JsonProperty("result", Required = Required.Default)]
        public JToken Result { get; private set; }

        /// <summary>
        /// Error from processing Rpc request (Required)
        /// </summary>
        [JsonProperty("error", Required = Required.Default)]
        public RpcError Error { get; protected set; }

        [JsonIgnore]
        public bool HasError { get { return this.Error != null; } }
    }

    [JsonObject]
    public class RpcStreamingResponseMessage : RpcResponseMessage
    {
        [JsonConstructor]
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
        [JsonConstructor]
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
        [JsonConstructor]
        private RpcRequestMessage()
        {

        }

        /// <param name="id">Request id</param>
        /// <param name="method">Target method name</param>
        /// <param name="parameterList">List of parameters for the target method</param>
        public RpcRequestMessage(object id, string method, params object[] parameterList)
        {
            this.Id = id;
            this.JsonRpcVersion = "2.0";
            this.Method = method;
            this.RawParameters = parameterList;
        }

        /// <param name="id">Request id</param>
        /// <param name="method">Target method name</param>
        /// <param name="parameterMap">Map of parameter name to parameter value for the target method</param>
        public RpcRequestMessage(object id, string method, Dictionary<string, object> parameterMap)
        {
            this.Id = id;
            this.JsonRpcVersion = "2.0";
            this.Method = method;
            this.RawParameters = parameterMap;
        }

        /// <summary>
        /// Request Id (Optional)
        /// </summary>
        [JsonProperty("id")]
        public object Id { get; set; }
        /// <summary>
        /// Version of the JsonRpc to be used (Required)
        /// </summary>
        [JsonProperty("jsonrpc", Required = Required.Always)]
        public string JsonRpcVersion { get; private set; }
        /// <summary>
        /// Name of the target method (Required)
        /// </summary>
        [JsonProperty("method", Required = Required.Always)]
        public string Method { get; private set; }
        /// <summary>
        /// Parameters to invoke the method with (Optional)
        /// </summary>
        [JsonProperty("params")]
        [JsonConverter(typeof(RpcParametersJsonConverter))]
        public object RawParameters { get; private set; }

    }
    /// <summary>
    /// Json converter for Rpc parameters
    /// </summary>
    public class RpcParametersJsonConverter : JsonConverter
    {
        /// <summary>
        /// Writes the value of the parameters to json format
        /// </summary>
        /// <param name="writer">Json writer</param>
        /// <param name="value">Value to be converted to json format</param>
        /// <param name="serializer">Json serializer</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
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
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
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
    public class RpcError
    {
        [JsonConstructor]
        private RpcError()
        {
        }
        /// <summary>
        /// Rpc error code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; private set; }

        /// <summary>
        /// Error message (Required)
        /// </summary>
        [JsonProperty("message", Required = Required.Always)]
        public string Message { get; private set; }

        /// <summary>
        /// Error data (Optional)
        /// </summary>
        [JsonProperty("data")]
        public JToken Data { get; private set; }
    }
}
