using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.CoreChain.Rpc;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBundlerRpcServer(
            this IServiceCollection services,
            BundlerRpcServerConfig config)
        {
            services.AddSingleton(config);

            services.AddSingleton<IWeb3>(provider =>
            {
                if (!string.IsNullOrEmpty(config.PrivateKey))
                {
                    var account = new Nethereum.Web3.Accounts.Account(config.PrivateKey, config.ChainId);
                    return new Web3.Web3(account, config.RpcUrl);
                }
                return new Web3.Web3(config.RpcUrl);
            });

            services.AddSingleton<BundlerService>(provider =>
            {
                var web3 = provider.GetRequiredService<IWeb3>();
                var bundlerConfig = config.ToBundlerConfig();
                return new BundlerService(web3, bundlerConfig);
            });

            services.AddSingleton<IBundlerService>(provider =>
                provider.GetRequiredService<BundlerService>());

            services.AddSingleton<IBundlerServiceExtended>(provider =>
                provider.GetRequiredService<BundlerService>());

            services.AddSingleton<RpcHandlerRegistry>(provider =>
            {
                var bundler = provider.GetRequiredService<BundlerService>();
                var registry = new RpcHandlerRegistry();

                registry.AddBundlerHandlers(bundler);

                if (config.EnableDebugMethods)
                {
                    registry.AddBundlerDebugHandlers(bundler);
                }

                return registry;
            });

            services.AddSingleton<RpcContext>(provider =>
            {
                return new RpcContext(null!, config.ChainId, provider);
            });

            services.AddSingleton<RpcDispatcher>(provider =>
            {
                var registry = provider.GetRequiredService<RpcHandlerRegistry>();
                var context = provider.GetRequiredService<RpcContext>();
                var logger = config.Verbose
                    ? provider.GetService<ILogger<RpcDispatcher>>()
                    : null;
                return new RpcDispatcher(registry, context, logger);
            });

            return services;
        }

        public static IServiceCollection AddBundlerRpcServer(
            this IServiceCollection services,
            BundlerRpcServerConfig config,
            BundlerService existingBundler)
        {
            services.AddSingleton(config);
            services.AddSingleton(existingBundler);
            services.AddSingleton<IBundlerService>(existingBundler);
            services.AddSingleton<IBundlerServiceExtended>(existingBundler);

            services.AddSingleton<RpcHandlerRegistry>(provider =>
            {
                var bundler = provider.GetRequiredService<BundlerService>();
                var registry = new RpcHandlerRegistry();

                registry.AddBundlerHandlers(bundler);

                if (config.EnableDebugMethods)
                {
                    registry.AddBundlerDebugHandlers(bundler);
                }

                return registry;
            });

            services.AddSingleton<RpcContext>(provider =>
            {
                return new RpcContext(null!, config.ChainId, provider);
            });

            services.AddSingleton<RpcDispatcher>(provider =>
            {
                var registry = provider.GetRequiredService<RpcHandlerRegistry>();
                var context = provider.GetRequiredService<RpcContext>();
                var logger = config.Verbose
                    ? provider.GetService<ILogger<RpcDispatcher>>()
                    : null;
                return new RpcDispatcher(registry, context, logger);
            });

            return services;
        }
    }
}
