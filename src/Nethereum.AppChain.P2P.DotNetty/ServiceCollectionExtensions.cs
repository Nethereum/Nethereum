using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.P2P;

namespace Nethereum.AppChain.P2P.DotNetty
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDotNettyTransport(
            this IServiceCollection services,
            Action<DotNettyConfig>? configureOptions = null)
        {
            var config = DotNettyConfig.Default;
            configureOptions?.Invoke(config);

            services.AddSingleton(config);
            services.AddSingleton<IP2PTransport, DotNettyTransport>(sp =>
            {
                var dotNettyConfig = sp.GetRequiredService<DotNettyConfig>();
                var logger = sp.GetService<ILogger<DotNettyTransport>>();
                return new DotNettyTransport(dotNettyConfig, logger);
            });

            return services;
        }
    }
}
