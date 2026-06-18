using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.MainnetChain.Server.Hosting
{
    public static class WebApplicationExtensions
    {
        public static WebApplication MapMainnetChainEndpoints(this WebApplication app)
        {
            var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nethereum.MainnetChain.Rpc");

            app.UseCors();

            app.MapPost("/", async (HttpContext httpContext) =>
            {
                try
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var json = await reader.ReadToEndAsync();

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("{\"error\":\"Empty request body\"}");
                        return;
                    }

                    httpContext.Response.ContentType = "application/json";

                    if (json.TrimStart().StartsWith('['))
                    {
                        var requests = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequestArray);
                        if (requests != null)
                        {
                            var rpcRequests = requests.Select(ToRpcRequestMessage).ToArray();
                            var responses = await dispatcher.DispatchBatchAsync(rpcRequests);
                            var jsonResponses = responses.Select(ToJsonRpcResponse).ToArray();
                            await httpContext.Response.WriteAsync(
                                JsonSerializer.Serialize(jsonResponses, CoreChainJsonContext.Default.JsonRpcResponseArray));
                            return;
                        }
                    }

                    var request = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequest);
                    if (request == null)
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("{\"error\":\"Invalid JSON-RPC request\"}");
                        return;
                    }

                    var rpcRequest = ToRpcRequestMessage(request);
                    var response = await dispatcher.DispatchAsync(rpcRequest);
                    var jsonResponse = ToJsonRpcResponse(response);

                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "JSON parse error");
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new JsonRpcResponse
                        {
                            Id = null,
                            Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
                        }, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Internal error");
                    httpContext.Response.StatusCode = 500;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new JsonRpcResponse
                        {
                            Id = null,
                            Error = new JsonRpcError { Code = -32603, Message = "Internal error" }
                        }, CoreChainJsonContext.Default.JsonRpcResponse));
                }
            });

            app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Nethereum.MainnetChain" }));

            return app;
        }

        private static RpcRequestMessage ToRpcRequestMessage(JsonRpcRequest request)
        {
            return new RpcRequestMessage
            {
                Id = request.Id,
                Method = request.Method,
                JsonRpcVersion = request.Jsonrpc,
                RawParameters = request.Params.HasValue ? request.Params.Value : null
            };
        }

        private static JsonRpcResponse ToJsonRpcResponse(RpcResponseMessage response)
        {
            if (response.HasError)
            {
                return new JsonRpcResponse
                {
                    Id = response.Id,
                    Error = new JsonRpcError
                    {
                        Code = response.Error.Code,
                        Message = response.Error.Message,
                        Data = response.Error.Data
                    }
                };
            }

            return new JsonRpcResponse
            {
                Id = response.Id,
                Result = response.Result
            };
        }
    }
}
