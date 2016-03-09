using System.Collections.Generic;
using edjCase.JsonRpc.Core.JsonConverters;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedMember.Local

namespace edjCase.JsonRpc.Core
{
	/// <summary>
	/// Model representing a Rpc request
	/// </summary>
	[JsonObject]
	public class RpcRequest
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
			this.JsonRpcVersion = JsonRpcContants.JsonRpcVersion;
			this.Method = method;
			this.RawParameters = parameterList;
		}

		/// <param name="id">Request id</param>
		/// <param name="method">Target method name</param>
		/// <param name="parameterMap">Map of parameter name to parameter value for the target method</param>
		public RpcRequest(object id, string method, Dictionary<string, object> parameterMap)
		{
			this.Id = id;
			this.JsonRpcVersion = JsonRpcContants.JsonRpcVersion;
			this.Method = method;
			this.RawParameters = parameterMap;
		}

		/// <summary>
		/// Request Id (Optional)
		/// </summary>
		[JsonProperty("id")]
		[JsonConverter(typeof(RpcIdJsonConverter))]
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

		/// <summary>
		/// Gets the raw parameters as an object array
		/// </summary>
		[JsonIgnore]
		public object[] ParameterList => this.RawParameters as object[];

		/// <summary>
		/// Gets the raw parameters as a parameter map
		/// </summary>
		[JsonIgnore]
		public Dictionary<string, object> ParameterMap => this.RawParameters as Dictionary<string, object>;
	}
}
