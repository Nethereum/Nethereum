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
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain.Hosting
{
    public static class WebApplicationExtensions
    {
        public static WebApplicationBuilder AddDevChainServer(this WebApplicationBuilder builder, DevChainServerConfig config)
        {
            builder.Services.AddDevChainServer(config);

            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            builder.Services.AddHostedService(sp =>
                new DevChainHostedService(
                    sp.GetRequiredService<DevChainNode>(),
                    sp.GetRequiredService<DevAccountManager>(),
                    sp,
                    sp.GetService<ILoggerFactory>()?.CreateLogger<DevChainHostedService>())
                { AlreadyStarted = true });

            return builder;
        }

        public static async Task<WebApplication> MapDevChainEndpointsAsync(this WebApplication app)
        {
            var node = app.Services.GetRequiredService<DevChainNode>();
            var accountManager = app.Services.GetRequiredService<DevAccountManager>();
            await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

            var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nethereum.DevChain.Rpc");

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
                            logger.LogInformation("batch[{Count}]: {Methods}",
                                requests.Length,
                                string.Join(", ", requests.Select(r => r.Method)));

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

                    logger.LogInformation("{Method}", request.Method);

                    var rpcRequest = ToRpcRequestMessage(request);
                    var response = await dispatcher.DispatchAsync(rpcRequest);
                    var jsonResponse = ToJsonRpcResponse(response);

                    if (jsonResponse.Error != null)
                    {
                        logger.LogWarning("{Method} -> error {Code}: {Message}",
                            request.Method, jsonResponse.Error.Code, jsonResponse.Error.Message);
                    }

                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "JSON parse error");
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.ContentType = "application/json";
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
                    };
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Internal error");
                    httpContext.Response.StatusCode = 500;
                    httpContext.Response.ContentType = "application/json";
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError { Code = -32603, Message = "Internal error" }
                    };
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
            });

            app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Nethereum.DevChain" }));

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
