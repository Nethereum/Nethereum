using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using edjCase.JsonRpc.Router.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace edjCase.JsonRpc.Router.Defaults
{
	/// <summary>
	/// Default Rpc method invoker that uses asynchronous processing
	/// </summary>
	public class DefaultRpcInvoker : IRpcInvoker
	{
		/// <summary>
		/// Optional logger for logging Rpc invocation
		/// </summary>
		public ILogger Logger { get; set; }
		
		/// <param name="logger">Optional logger for logging Rpc invocation</param>
		public DefaultRpcInvoker(ILogger logger = null)
		{
			this.Logger = logger;
		}

		/// <summary>
		/// Call the incoming Rpc requests methods and gives the appropriate respones
		/// </summary>
		/// <param name="requests">List of Rpc requests</param>
		/// <param name="route">Rpc route that applies to the current request</param>
		/// <param name="serviceProvider">(Optional)IoC Container for rpc method controllers</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>List of Rpc responses for the requests</returns>
		public List<RpcResponse> InvokeBatchRequest(List<RpcRequest> requests, RpcRoute route, IServiceProvider serviceProvider = null, JsonSerializerSettings jsonSerializerSettings = null)
		{
			this.Logger?.LogVerbose($"Invoking '{requests.Count}' batch requests");
			var invokingTasks = new List<Task<RpcResponse>>();
			foreach (RpcRequest request in requests)
			{
				Task<RpcResponse> invokingTask = Task.Run(() => this.InvokeRequest(request, route, serviceProvider, jsonSerializerSettings));
				invokingTasks.Add(invokingTask);
			}

			Task.WaitAll(invokingTasks.Cast<Task>().ToArray());

			List<RpcResponse> responses = invokingTasks
				.Select(t => t.Result)
				.Where(r => r != null)
				.ToList();

			this.Logger?.LogVerbose($"Finished '{requests.Count}' batch requests");

			return responses;
		}

		/// <summary>
		/// Call the incoming Rpc request method and gives the appropriate response
		/// </summary>
		/// <param name="request">Rpc request</param>
		/// <param name="route">Rpc route that applies to the current request</param>
		/// <param name="serviceProvider">(Optional)IoC Container for rpc method controllers</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>An Rpc response for the request</returns>
		public RpcResponse InvokeRequest(RpcRequest request, RpcRoute route, IServiceProvider serviceProvider = null, JsonSerializerSettings jsonSerializerSettings = null)
		{
			try
			{
				if (request == null)
				{
					throw new ArgumentNullException(nameof(request));
				}
				if (route == null)
				{
					throw new ArgumentNullException(nameof(route));
				}
			}
			catch (ArgumentNullException ex) // Dont want to throw any exceptions when doing async requests
			{
				return this.GetUnknownExceptionReponse(request, ex);
			}

			this.Logger?.LogVerbose($"Invoking request with id '{request.Id}'");
			RpcResponse rpcResponse;
			try
			{
				if (!string.Equals(request.JsonRpcVersion, JsonRpcContants.JsonRpcVersion))
				{
					throw new RpcInvalidRequestException($"Request must be jsonrpc version '{JsonRpcContants.JsonRpcVersion}'");
				}
				
				object[] parameterList;
				RpcMethod rpcMethod = this.GetMatchingMethod(route, request, out parameterList, serviceProvider, jsonSerializerSettings);

				this.Logger?.LogVerbose($"Attempting to invoke method '{request.Method}'");
				object result = rpcMethod.Invoke(parameterList);
				this.Logger?.LogVerbose($"Finished invoking method '{request.Method}'");

				JsonSerializer jsonSerializer = JsonSerializer.Create(jsonSerializerSettings);
				JToken resultJToken = JToken.FromObject(result, jsonSerializer);
				rpcResponse = new RpcResponse(request.Id, resultJToken);
			}
			catch (RpcException ex)
			{
				this.Logger?.LogError("An Rpc error occurred. Returning an Rpc error response", ex);
				RpcError error = new RpcError(ex);
				rpcResponse = new RpcResponse(request.Id, error);
			}
			catch (Exception ex)
			{
				rpcResponse = this.GetUnknownExceptionReponse(request, ex);
			}

			if (request.Id != null)
			{
				this.Logger?.LogVerbose($"Finished request with id '{request.Id}'");
				//Only give a response if there is an id
				return rpcResponse;
			}
			this.Logger?.LogVerbose($"Finished request with no id. Not returning a response");
			return null;
		}

		/// <summary>
		/// Converts an unknown caught exception into a Rpc response
		/// </summary>
		/// <param name="request">Current Rpc request</param>
		/// <param name="ex">Unknown exception</param>
		/// <returns>Rpc error response from the exception</returns>
		private RpcResponse GetUnknownExceptionReponse(RpcRequest request, Exception ex)
		{
			this.Logger?.LogError("An unknown error occurred. Returning an Rpc error response", ex);
#if DEBUG
			string message = ex.Message;
#else
			string message = "An internal server error has occurred";
#endif
			RpcUnknownException exception = new RpcUnknownException(message);
			RpcError error = new RpcError(exception);
			if (request?.Id == null)
			{
				return null;
			}
			RpcResponse rpcResponse = new RpcResponse(request.Id, error);
			return rpcResponse;
		}

		/// <summary>
		/// Finds the matching Rpc method for the current request
		/// </summary>
		/// <param name="route">Rpc route for the current request</param>
		/// <param name="request">Current Rpc request</param>
		/// <param name="parameterList">Paramter list parsed from the request</param>
		/// <param name="serviceProvider">(Optional)IoC Container for rpc method controllers</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>The matching Rpc method to the current request</returns>
		private RpcMethod GetMatchingMethod(RpcRoute route, RpcRequest request, out object[] parameterList, IServiceProvider serviceProvider = null, JsonSerializerSettings jsonSerializerSettings = null)
		{
			if (route == null)
			{
				throw new ArgumentNullException(nameof(route));
			}
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}
			this.Logger?.LogVerbose($"Attempting to match Rpc request to a method '{request.Method}'");
			List<RpcMethod> methods = DefaultRpcInvoker.GetRpcMethods(route, serviceProvider, jsonSerializerSettings);

			methods = methods
				.Where(m => string.Equals(m.Method, request.Method, StringComparison.OrdinalIgnoreCase))
				.ToList();

			RpcMethod rpcMethod = null;
			parameterList = null;
			if (methods.Count > 1)
			{
				foreach (RpcMethod method in methods)
				{
					bool matchingMethod;
					if (request.ParameterMap != null)
					{
						matchingMethod = method.HasParameterSignature(request.ParameterMap, out parameterList);
					}
					else
					{
						matchingMethod = method.HasParameterSignature(request.ParameterList);
						parameterList = request.ParameterList;
					}
					if (matchingMethod)
					{
						if (rpcMethod != null) //If already found a match
						{
							throw new RpcAmbiguousMethodException();
						}
						rpcMethod = method;
					}
				}
			}
			else if (methods.Count == 1)
			{
				//Only signature check for methods that have the same name for performance reasons
				rpcMethod = methods.First();
				if (request.ParameterMap != null)
				{
					bool signatureMatch = rpcMethod.TryParseParameterList(request.ParameterMap, out parameterList);
					if (!signatureMatch)
					{
						throw new RpcMethodNotFoundException();
					}
				}
				else
				{
					parameterList = request.ParameterList;
				}
			}
			if (rpcMethod == null)
			{
				throw new RpcMethodNotFoundException();
			}
			this.Logger?.LogVerbose("Request was matched to a method");
			return rpcMethod;
		}

		/// <summary>
		/// Gets all the predefined Rpc methods for a Rpc route
		/// </summary>
		/// <param name="route">The route to get Rpc methods for</param>
		/// <param name="serviceProvider">(Optional) IoC Container for rpc method controllers</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>List of Rpc methods for the specified Rpc route</returns>
		private static List<RpcMethod> GetRpcMethods(RpcRoute route, IServiceProvider serviceProvider = null, JsonSerializerSettings jsonSerializerSettings = null)
		{
			List<RpcMethod> rpcMethods = new List<RpcMethod>();
			foreach (Type type in route.GetClasses())
			{
				MethodInfo[] publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
				foreach (MethodInfo publicMethod in publicMethods)
				{
					RpcMethod rpcMethod = new RpcMethod(type, route, publicMethod, serviceProvider, jsonSerializerSettings);
					rpcMethods.Add(rpcMethod);
				}
			}
			return rpcMethods;
		}

	}
}
