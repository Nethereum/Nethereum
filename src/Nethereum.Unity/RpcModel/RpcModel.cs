using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Nethereum.Unity.RpcModel
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
    internal static class RpcResponseExtensions
	{
		/// <summary>
		/// Parses and returns the result of the rpc response as the type specified. 
		/// Otherwise throws a parsing exception
		/// </summary>
		/// <typeparam name="T">Type of object to parse the response as</typeparam>
		/// <param name="response">Rpc response object</param>
		/// <param name="returnDefaultIfNull">Returns the type's default value if the result is null. Otherwise throws parsing exception</param>
		/// <returns>Result of response as type specified</returns>
		public static T GetResult<T>(this RpcResponse response, bool returnDefaultIfNull = true, JsonSerializerSettings settings = null)
		{
			if (response.Result == null)
			{
				if(!returnDefaultIfNull && default(T) != null)
				{
					throw new Exception("Unable to convert the result (null) to type " + typeof(T));
				}
				return default(T);
			}
			try
			{
				if(settings == null)
				{
					return response.Result.ToObject<T>();
				}
				else
				{
					JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
					return response.Result.ToObject<T>(jsonSerializer);
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Unable to convert the result to type " +  typeof(T), ex);
			}
		}
	}

    [JsonObject]
	public class RpcResponse
	{
		[JsonConstructor]
		protected RpcResponse()
		{
             JsonRpcVersion = "2.0";
		}

		/// <param name="id">Request id</param>
		protected RpcResponse(object id)
		{
			this.Id = id;
            JsonRpcVersion = "2.0";
		}

		/// <param name="id">Request id</param>
		/// <param name="error">Request error</param>
		public RpcResponse(object id, RpcError error) : this(id)
		{
			this.Error = error;
		}

		/// <param name="id">Request id</param>
		/// <param name="result">Response result object</param>
		public RpcResponse(object id, JToken result) : this(id)
		{
			this.Result = result;
		}

		/// <summary>
		/// Request id (Required but nullable)
		/// </summary>
		[JsonProperty("id", Required = Required.AllowNull)]
		public object Id { get; private set; }

		/// <summary>
		/// Rpc request version (Required)
		/// </summary>
		[JsonProperty("jsonrpc", Required = Required.Always)]
		public string JsonRpcVersion { get; private set; } 

		/// <summary>
		/// Reponse result object (Required)
		/// </summary>
		[JsonProperty("result", Required = Required.Default)] //TODO somehow enforce this or an error, not both
		public JToken Result { get; private set; }

		/// <summary>
		/// Error from processing Rpc request (Required)
		/// </summary>
		[JsonProperty("error", Required = Required.Default)]
		public RpcError Error { get; private set; }

		[JsonIgnore]
		public bool HasError { get{ return this.Error != null;}}
	}

    [JsonObject]
	internal class RpcRequest
	{
		[JsonConstructor]
		private RpcRequest()
		{

		}
		
		/// <param name="id">Request id</param>
		/// <param name="method">Target method name</param>
		/// <param name="parameterList">List of parameters for the target method</param>
		public RpcRequest(object id, string method, params object[] parameterList)
		{
			this.Id = id;
			this.JsonRpcVersion =  "2.0";
			this.Method = method;
			this.RawParameters = parameterList;
		}

		/// <param name="id">Request id</param>
		/// <param name="method">Target method name</param>
		/// <param name="parameterMap">Map of parameter name to parameter value for the target method</param>
		public RpcRequest(object id, string method, Dictionary<string, object> parameterMap)
		{
			this.Id = id;
			this.JsonRpcVersion =  "2.0";
			this.Method = method;
			this.RawParameters = parameterMap;
		}

		/// <summary>
		/// Request Id (Optional)
		/// </summary>
		[JsonProperty("id")]
		public object Id { get; private set; }
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
	internal class RpcParametersJsonConverter : JsonConverter
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
		/// Rpc error code (Required)
		/// </summary>
		[JsonProperty("code", Required = Required.Always)]
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