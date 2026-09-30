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
using Nethereum.CoreChain.Storage;
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

        public static WebApplicationBuilder AddEngineApiServer(this WebApplicationBuilder builder, EngineApiServerConfig config)
        {
            builder.Services.AddEngineApiServer(config);
            return builder;
        }

        public static Task<WebApplication> MapDevChainEndpointsAsync(this WebApplication app)
        {
            var accountManager = app.Services.GetRequiredService<DevAccountManager>();
            return app.MapDevChainEndpointsAsync(
                node => node.StartAsync(accountManager.Accounts.Select(a => a.Address)));
        }

        public static async Task<WebApplication> MapDevChainEndpointsAsync(
            this WebApplication app, System.Func<DevChainNode, Task> startNodeAsync)
        {
            var node = app.Services.GetRequiredService<DevChainNode>();
            var bundle = app.Services.GetRequiredService<IChainStoreBundle>();
            var serverConfig = app.Services.GetRequiredService<DevChainServerConfig>();
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

            await DevChainComposition.ComposeAsync(serverConfig, node, bundle, startNodeAsync, loggerFactory);

            var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
            var logger = loggerFactory.CreateLogger("Nethereum.DevChain.Rpc");

            if (serverConfig.EngineApiEnabled)
            {
                app.MapEngineApiEndpoints();
            }

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
                            var jsonResponses = responses.Select(r => r.ToJsonRpcResponse()).ToArray();
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
                    var jsonResponse = response.ToJsonRpcResponse();

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

            app.MapGet("/eth/v1/beacon/blob_sidecars/{blockId}", async (string blockId, HttpContext httpContext) =>
            {
                try
                {
                    var sidecars = await CoreChain.Rpc.BeaconBlobSidecarsHandler.GetBlobSidecarsAsync(node, blockId);
                    var data = sidecars.Select(s => new
                    {
                        index = s.Index.ToString(),
                        blob = s.Blob,
                        kzg_commitment = s.KzgCommitment,
                        kzg_proof = s.KzgProof
                    }).ToArray();

                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new { version = "deneb", data },
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
                }
                catch (Exception ex)
                {
                    httpContext.Response.StatusCode = 404;
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new { code = 404, message = $"Block not found: {ex.Message}" }));
                }
            });

            return app;
        }

        public static WebApplication MapEngineApiEndpoints(this WebApplication app)
        {
            var engineConfig = app.Services.GetRequiredService<EngineApiServerConfig>();
            var engineHost = app.Services.GetRequiredService<EngineApiHost>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nethereum.DevChain.EngineApi");

            app.Urls.Add($"http://{engineConfig.BindAddress}:{engineConfig.Port}");

            app.Use(async (httpContext, next) =>
            {
                if (httpContext.Connection.LocalPort != engineConfig.Port)
                {
                    await next();
                    return;
                }

                if (!engineHost.Validator.TryValidate(httpContext.Request.Headers["Authorization"].ToString(), out var authError))
                {
                    logger.LogWarning("Engine API request rejected: {Reason}", authError);
                    httpContext.Response.StatusCode = 401;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync("{\"error\":\"unauthorized\"}");
                    return;
                }

                if (!HttpMethods.IsPost(httpContext.Request.Method))
                {
                    httpContext.Response.StatusCode = 404;
                    return;
                }

                httpContext.Response.ContentType = "application/json";

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

                    var request = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequest);
                    if (request == null)
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("{\"error\":\"Invalid JSON-RPC request\"}");
                        return;
                    }

                    logger.LogInformation("{Method}", request.Method);

                    var rpcRequest = ToRpcRequestMessage(request);
                    var response = await engineHost.Dispatcher.DispatchAsync(rpcRequest);
                    var jsonResponse = response.ToJsonRpcResponse();

                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (JsonException)
                {
                    httpContext.Response.StatusCode = 400;
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
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError { Code = -32603, Message = "Internal error" }
                    };
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
            });

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

    }
}
