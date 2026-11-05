using System;
using Nethereum.Documentation;
using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.ChainNode.Hosting.Rpc
{
    public static class RpcHttpEndpoint
    {
        private static readonly string[] AcceptedContentTypes =
        {
            "application/json",
            "application/json-rpc",
            "application/jsonrequest"
        };

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-node-rpc", "Map the shared JSON-RPC HTTP endpoint")]
        public static void MapRpcHttpEndpoint(
            this WebApplication app,
            IRpcDispatcher dispatcher,
            JsonSerializerOptions serializerOptions,
            RpcHttpOptions options,
            ILogger? logger)
        {
            var envelope = new JsonRpcEnvelope(dispatcher, serializerOptions, logger, options.MaxResponseSize);
            var hostPolicy = options.HostPolicy;
            app.MapPost("/", context => HandleAsync(context, envelope, serializerOptions, options, hostPolicy, logger));
        }

        private static async Task HandleAsync(
            HttpContext context,
            JsonRpcEnvelope envelope,
            JsonSerializerOptions serializerOptions,
            RpcHttpOptions options,
            RpcHostPolicy hostPolicy,
            ILogger? logger)
        {
            if (!hostPolicy.IsAllowed(context.Request.Host.Value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("invalid host specified", context.RequestAborted);
                return;
            }

            context.Response.ContentType = "application/json";
            if (context.Request.ContentLength > options.MaxRequestBodySize)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            if (!IsAcceptedContentType(context.Request.ContentType))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit != null && !bodyLimit.IsReadOnly)
            {
                bodyLimit.MaxRequestBodySize = options.MaxRequestBodySize;
            }

            try
            {
                using var buffer = new MemoryStream();
                try
                {
                    await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
                }
                catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }

                var result = await envelope.HandleAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), context.RequestAborted);
                context.Response.StatusCode = result.Status;
                if (result.Body != null)
                {
                    await context.Response.WriteAsync(result.Body, context.RequestAborted);
                }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Internal error");
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                var response = new JsonRpcResponse { Id = null, Error = new JsonRpcError { Code = -32603, Message = "Internal error" } };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, serializerOptions));
            }
        }

        private static bool IsAcceptedContentType(string? contentType)
        {
            if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed) || parsed.MediaType == null) return false;
            foreach (var accepted in AcceptedContentTypes)
            {
                if (string.Equals(accepted, parsed.MediaType, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
