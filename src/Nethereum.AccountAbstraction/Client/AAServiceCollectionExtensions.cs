using System;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Client
{
    public static class AAServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumAccountAbstraction(
            this IServiceCollection services, Action<AAOptions> configure)
        {
            services.AddSingleton<IAAClient>(serviceProvider =>
            {
                var options = new AAOptions();
                configure(options);

                var web3 = options.Web3 ?? serviceProvider.GetService<IWeb3>()
                    ?? throw new InvalidOperationException(
                        "AddNethereumAccountAbstraction requires a Web3 instance: call AAOptions.UseWeb3(...) " +
                        "or register an IWeb3 in the service collection.");

                var deploymentAddresses = options.DeploymentAddresses
                    ?? throw new InvalidOperationException(
                        "AddNethereumAccountAbstraction requires an AADeploymentAddresses: call AAOptions.UseDeploymentAddresses(...).");

                var bundler = options.Bundler
                    ?? throw new InvalidOperationException(
                        "AddNethereumAccountAbstraction requires a bundler: call AAOptions.UseBundler(...) or UseBundlerUrl(...).");

                return new AAClient(web3, deploymentAddresses, bundler);
            });

            return services;
        }
    }
}
