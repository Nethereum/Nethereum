using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using edjCase.JsonRpc.Router.Abstractions;
using Microsoft.AspNet.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace edjCase.JsonRpc.Router
{
	/// <summary>
	/// Router for Asp.Net to direct Http Rpc requests to the correct method, invoke it and return the proper response
	/// </summary>
	public class RpcRouter : IRouter
	{
		/// <summary>
		/// Configuration data for the router
		/// </summary>
		private RpcRouterConfiguration configuration { get; }
		/// <summary>
		/// Component that invokes Rpc requests target methods and returns a response
		/// </summary>
		private IRpcInvoker invoker { get; }
		/// <summary>
		/// Component that parses Http requests into Rpc requests
		/// </summary>
		private IRpcParser parser { get; }
		/// <summary>
		/// Component that compresses Rpc responses
		/// </summary>
		private IRpcCompressor compressor { get; }
		/// <summary>
		/// Component that logs actions from the router
		/// </summary>
		private ILogger logger { get; }

		/// <param name="configuration">Configuration data for the router</param>
		/// <param name="invoker">Component that invokes Rpc requests target methods and returns a response</param>
		/// <param name="parser">Component that parses Http requests into Rpc requests</param>
		/// <param name="compressor">Component that compresses Rpc responses</param>
		/// <param name="logger">Component that logs actions from the router</param>
		public RpcRouter(RpcRouterConfiguration configuration, IRpcInvoker invoker, IRpcParser parser, IRpcCompressor compressor, ILogger logger)
		{
			if (configuration == null)
			{
				throw new ArgumentNullException(nameof(configuration));
			}
			if (invoker == null)
			{
				throw new ArgumentNullException(nameof(invoker));
			}
			if (parser == null)
			{
				throw new ArgumentNullException(nameof(parser));
			}
			if (compressor == null)
			{
				throw new ArgumentNullException(nameof(compressor));
			}
			this.configuration = configuration;
			this.invoker = invoker;
			this.parser = parser;
			this.compressor = compressor;
			this.logger = logger;
		}

		/// <summary>
		/// Generates the virtual path data for the router
		/// </summary>
		/// <param name="context">Virtual path context</param>
		/// <returns>Virtual path data for the router</returns>
		public VirtualPathData GetVirtualPath(VirtualPathContext context)
		{
			// We return null here because we're not responsible for generating the url, the route is.
			return null;
		}

		/// <summary>
		/// Takes a route/http contexts and attempts to parse, invoke, respond to an Rpc request
		/// </summary>
		/// <param name="context">Route context</param>
		/// <returns>Task for async routing</returns>
		public async Task RouteAsync(RouteContext context)
		{
			try
			{
				RpcRoute route;
				bool matchesRoute = this.parser.MatchesRpcRoute(this.configuration.Routes, context.HttpContext.Request.Path, out route);
				if (!matchesRoute)
				{
					return;
				}
				this.logger?.LogInformation($"Rpc request route '{route.Name}' matched");
				try
				{
					Stream contentStream = context.HttpContext.Request.Body;

					string jsonString;
					if (contentStream == null)
					{
						jsonString = null;
					}
					else
					{
						using (StreamReader streamReader = new StreamReader(contentStream))
						{
							jsonString = streamReader.ReadToEnd().Trim();
						}
					}
					List<RpcRequest> requests = this.parser.ParseRequests(jsonString, this.configuration.JsonSerializerSettings);
					this.logger?.LogInformation($"Processing {requests.Count} Rpc requests");

					List<RpcResponse> responses = this.invoker.InvokeBatchRequest(requests, route, context.HttpContext.RequestServices, this.configuration.JsonSerializerSettings);

					this.logger?.LogInformation($"Sending '{responses.Count}' Rpc responses");
					await this.SetResponse(context, responses, this.configuration.JsonSerializerSettings);
					context.IsHandled = true;

					this.logger?.LogInformation("Rpc request complete");
				}
				catch (RpcException ex)
				{
					context.IsHandled = true;
					this.logger?.LogError("Error occurred when proccessing Rpc request. Sending Rpc error response", ex);
					await this.SetErrorResponse(context, ex);
					return;
				}
			}
			catch (Exception ex)
			{
				string errorMessage = "Unknown exception occurred when trying to process Rpc request. Marking route unhandled";
                this.logger?.LogError(errorMessage, ex);
				context.IsHandled = false;
			}
		}

		/// <summary>
		/// Sets the http response to the corresponding Rpc exception
		/// </summary>
		/// <param name="context">Route context</param>
		/// <param name="exception">Exception from Rpc request processing</param>
		/// <returns>Task for async call</returns>
		private async Task SetErrorResponse(RouteContext context, RpcException exception)
		{
			var responses = new List<RpcResponse>
			{
				new RpcResponse(null, new RpcError(exception))
			};
			await this.SetResponse(context, responses, this.configuration.JsonSerializerSettings);
		}

		/// <summary>
		/// Sets the http response with the given Rpc responses
		/// </summary>
		/// <param name="context">Route context</param>
		/// <param name="responses">Responses generated from the Rpc request(s)</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		/// <returns>Task for async call</returns>
		private async Task SetResponse(RouteContext context, List<RpcResponse> responses, JsonSerializerSettings jsonSerializerSettings = null)
		{
			if (responses == null || !responses.Any())
			{
				return;
			}

			string resultJson = responses.Count == 1
				? JsonConvert.SerializeObject(responses.First(), jsonSerializerSettings)
				: JsonConvert.SerializeObject(responses, jsonSerializerSettings);

			string acceptEncoding = context.HttpContext.Request.Headers["Accept-Encoding"];
			if (!string.IsNullOrWhiteSpace(acceptEncoding))
			{
				string[] encodings = acceptEncoding.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string encoding in encodings)
				{
					CompressionType compressionType;
					bool haveType = Enum.TryParse(encoding, true, out compressionType);
					if (!haveType)
					{
						continue;
					}
					context.HttpContext.Response.Headers.Add("Content-Encoding", new[] {encoding});
					this.compressor.CompressText(context.HttpContext.Response.Body, resultJson, Encoding.UTF8, compressionType);
					return;
				}
			}

			Stream responseStream = context.HttpContext.Response.Body;
			using (StreamWriter streamWriter = new StreamWriter(responseStream))
			{
				await streamWriter.WriteAsync(resultJson);
			}
		}
	}
}