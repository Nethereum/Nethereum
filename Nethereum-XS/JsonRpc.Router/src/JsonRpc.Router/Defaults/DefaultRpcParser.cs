using System;
using System.Collections.Generic;
using System.Linq;
using edjCase.JsonRpc.Core;
using edjCase.JsonRpc.Router.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace edjCase.JsonRpc.Router.Defaults
{
	/// <summary>
	/// Default Rpc parser that uses <see cref="Newtonsoft.Json"/>
	/// </summary>
	public class DefaultRpcParser : IRpcParser
	{
		/// <summary>
		/// Optional logger for logging Rpc parsing
		/// </summary>
		public ILogger Logger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="logger">Optional logger for logging Rpc parsing</param>
		public DefaultRpcParser(ILogger logger = null)
		{
			this.Logger = logger;
		}

		/// <summary>
		/// Indicates if the incoming request matches any predefined routes
		/// </summary>
		/// <param name="routes">Predefined routes for Rpc requests</param>
		/// <param name="requestUrl">The current request url</param>
		/// <param name="route">The matching route corresponding to the request url if found, otherwise it is null</param>
		/// <returns>True if the request url matches any Rpc routes, otherwise False</returns>
		public bool MatchesRpcRoute(RpcRouteCollection routes, string requestUrl, out RpcRoute route)
		{
			if (routes == null)
			{
				throw new ArgumentNullException(nameof(routes));
			}
			if (requestUrl == null)
			{
				throw new ArgumentNullException(nameof(requestUrl));
			}
			this.Logger?.LogVerbose($"Attempting to match Rpc route for the request url '{requestUrl}'");
			RpcPath requestPath = RpcPath.Parse(requestUrl);
			RpcPath routePrefix = RpcPath.Parse(routes.RoutePrefix);
			
			foreach (RpcRoute rpcRoute in routes)
			{
				RpcPath routePath = RpcPath.Parse(rpcRoute.Name);
				routePath = routePrefix.Add(routePath);
				if (requestPath == routePath)
				{
					this.Logger?.LogVerbose($"Matched the request url '{requestUrl}' to the route '{rpcRoute.Name}'");
					route = rpcRoute;
					return true;
				}
			}
			this.Logger?.LogVerbose($"Failed to match the request url '{requestUrl}' to a route");
			route = null;
			return false;
		}


		/// <summary>
		/// Parses all the requests from the json in the request
		/// </summary>
		/// <param name="jsonString">Json from the http request</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>List of Rpc requests that were parsed from the json</returns>
		public List<RpcRequest> ParseRequests(string jsonString, JsonSerializerSettings jsonSerializerSettings = null)
		{
			this.Logger?.LogVerbose($"Attempting to parse Rpc request from the json string '{jsonString}'");
			List<RpcRequest> rpcRequests;
			if (string.IsNullOrWhiteSpace(jsonString))
			{
				throw new RpcInvalidRequestException("Json request was empty");
			}
			try
			{
				if (!DefaultRpcParser.IsSingleRequest(jsonString))
				{
					rpcRequests = JsonConvert.DeserializeObject<List<RpcRequest>>(jsonString, jsonSerializerSettings);
				}
				else
				{
					rpcRequests = new List<RpcRequest>();
					RpcRequest rpcRequest = JsonConvert.DeserializeObject<RpcRequest>(jsonString, jsonSerializerSettings);
					if (rpcRequest != null)
					{
						rpcRequests.Add(rpcRequest);
					}
				}
			}
			catch (Exception ex) when (!(ex is RpcException))
			{
				string errorMessage = "Unable to parse json request into an rpc format.";
#if DEBUG
				errorMessage += "\tException: " + ex.Message;
#endif
				throw new RpcInvalidRequestException(errorMessage);
			}

			if (rpcRequests == null || !rpcRequests.Any())
			{
				throw new RpcInvalidRequestException("No rpc json requests found");
			}
			this.Logger?.LogVerbose($"Successfully parsed {rpcRequests.Count} Rpc request(s)");
			HashSet<object> uniqueIds = new HashSet<object>();
			foreach (RpcRequest rpcRequest in rpcRequests)
			{
				bool unique = uniqueIds.Add(rpcRequest.Id);
				if (!unique && rpcRequest.Id != null)
				{
					throw new RpcInvalidRequestException("Duplicate ids in batch requests are not allowed");
				}
			}
			return rpcRequests;
		}

		/// <summary>
		/// Detects if the json string is a single Rpc request versus a batch request
		/// </summary>
		/// <param name="jsonString">Json of Rpc request</param>
		/// <returns>True if json is a single Rpc request, otherwise False</returns>
		private static bool IsSingleRequest(string jsonString)
		{
			if (string.IsNullOrEmpty(jsonString))
			{
				throw new RpcInvalidRequestException(nameof(jsonString));
			}
			for (int i = 0; i < jsonString.Length; i++)
			{
				char character = jsonString[i];
				switch (character)
				{
					case '{':
						return true;
					case '[':
						return false;
				}
			}
			return true;
		}
	}
}
