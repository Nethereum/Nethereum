using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.CoreChain.Rpc;
using Nethereum.Documentation;
using Nethereum.EngineApi.Hosting;

namespace Nethereum.MainnetChain.Hosting
{
    public static class WebApplicationExtensions
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Map the JSON-RPC and health endpoints")]
        public static WebApplication MapMainnetChainEndpoints(this WebApplication app)
        {
            var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nethereum.MainnetChain.Rpc");

            if (app.Services.GetService<EngineApiServerConfig>() != null)
                app.MapEngineApiEndpoints();

            app.UseCors();

            app.MapRpcHttpEndpoint(
                dispatcher,
                JsonRpcEnvelope.CreateSerializerOptions(),
                app.Services.GetService<RpcHttpOptions>() ?? new RpcHttpOptions(),
                logger);

            app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Nethereum.MainnetChain" }));

            return app;
        }
    }
}
