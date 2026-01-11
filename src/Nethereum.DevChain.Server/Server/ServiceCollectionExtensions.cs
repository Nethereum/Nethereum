using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Rpc.Handlers;

namespace Nethereum.DevChain.Server.Server
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevChainServer(this IServiceCollection services, DevChainServerConfig config)
        {
            services.AddSingleton(config);

            services.AddSingleton(provider =>
            {
                var devChainConfig = config.ToDevChainConfig();
                return new DevChainNode(devChainConfig);
            });

            services.AddSingleton<DevAccountManager>();

            services.AddSingleton<RpcHandlerRegistry>(provider =>
            {
                var registry = new RpcHandlerRegistry();

                registry.AddStandardHandlers();
                registry.AddDevHandlers();

                registry.Override(new EthAccountsHandler());
                registry.Register(new HardhatImpersonateAccountHandler());
                registry.Register(new HardhatStopImpersonatingAccountHandler());

                return registry;
            });

            services.AddSingleton<RpcContext>(provider =>
            {
                var node = provider.GetRequiredService<DevChainNode>();
                return new RpcContext(node, config.ChainId, provider);
            });

            services.AddSingleton<RpcDispatcher>(provider =>
            {
                var registry = provider.GetRequiredService<RpcHandlerRegistry>();
                var context = provider.GetRequiredService<RpcContext>();
                var logger = provider.GetRequiredService<ILogger<RpcDispatcher>>();

                Action<string>? logInfo = config.Verbose ? msg => logger.LogInformation("{Message}", msg) : null;
                Action<string, Exception>? logError = (msg, ex) => logger.LogError(ex, "{Message}", msg);

                return new RpcDispatcher(registry, context, logInfo, logError);
            });

            return services;
        }
    }
}
