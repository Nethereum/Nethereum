using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.EngineApi.Hosting;

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

            var httpOptions = serverConfig.Node.Rpc.ToHttpOptions();
            if (httpOptions.AllowedHosts.Count == 0) httpOptions.AllowedHosts.Add(RpcHostPolicy.Wildcard);
            app.MapRpcHttpEndpoint(dispatcher, JsonRpcEnvelope.CreateSerializerOptions(), httpOptions, logger);

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
    }
}
