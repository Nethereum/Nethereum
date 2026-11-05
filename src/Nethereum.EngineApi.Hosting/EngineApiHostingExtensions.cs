using System;
using Nethereum.Documentation;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.EngineApi.Hosting
{
    public static class EngineApiHostingExtensions
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "engine-api-hosting", "Register the JWT-authenticated Engine API server")]
        public static IServiceCollection AddEngineApiServer(
            this IServiceCollection services,
            EngineApiServerConfig config,
            Func<IServiceProvider, IEngineApiService> engineFactory,
            bool verbose = false)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (engineFactory == null) throw new ArgumentNullException(nameof(engineFactory));

            services.AddSingleton(config);
            services.AddSingleton<IEngineApiService>(engineFactory);

            services.AddSingleton(provider =>
                new EngineJwtValidator(EngineJwtSecret.LoadOrCreate(config.JwtSecretPath)));

            services.AddSingleton(provider =>
            {
                var registry = new RpcHandlerRegistry();
                registry.AddEngineHandlers();

                var rpcContext = provider.GetRequiredService<RpcContext>();
                var logger = verbose ? provider.GetRequiredService<ILogger<RpcDispatcher>>() : null;
                var dispatcher = new RpcDispatcher(registry, rpcContext, logger);
                var validator = provider.GetRequiredService<EngineJwtValidator>();

                return new EngineApiHost(registry, dispatcher, validator);
            });

            return services;
        }

        public static WebApplicationBuilder AddEngineApiServer(
            this WebApplicationBuilder builder,
            EngineApiServerConfig config,
            Func<IServiceProvider, IEngineApiService> engineFactory,
            bool verbose = false)
        {
            builder.Services.AddEngineApiServer(config, engineFactory, verbose);
            return builder;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "engine-api-hosting", "Map the Engine API endpoint on its own port")]
        public static WebApplication MapEngineApiEndpoints(this WebApplication app)
        {
            var engineConfig = app.Services.GetRequiredService<EngineApiServerConfig>();
            var engineHost = app.Services.GetRequiredService<EngineApiHost>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nethereum.EngineApi.Hosting");

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
                    logger.LogDebug("Engine API request rejected: {Reason}", authError);
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
