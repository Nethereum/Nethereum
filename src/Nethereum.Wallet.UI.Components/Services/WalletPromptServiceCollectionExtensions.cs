using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Services
{
    public static class WalletPromptServiceCollectionExtensions
    {
        public static IServiceCollection AddWalletPromptServices(
            this IServiceCollection services,
            ServiceLifetime promptServiceLifetime)
        {
            services.TryAddSingleton<PromptInfrastructureLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<PromptInfrastructureLocalizer>>(provider =>
                provider.GetRequiredService<PromptInfrastructureLocalizer>());

            services.TryAddSingleton<IPromptQueueService, PromptQueueService>();
            services.TryAddSingleton<IPromptOverlayService, PromptOverlayService>();

            RegisterPromptService<ITransactionPromptService, QueuedTransactionPromptService>(services, promptServiceLifetime);
            RegisterPromptService<ISignaturePromptService, QueuedSignaturePromptService>(services, promptServiceLifetime);
            RegisterPromptService<IDappPermissionPromptService, QueuedDappPermissionPromptService>(services, promptServiceLifetime);
            RegisterPromptService<IChainAdditionPromptService, QueuedChainAdditionPromptService>(services, promptServiceLifetime);
            RegisterPromptService<IChainSwitchPromptService, QueuedChainSwitchPromptService>(services, promptServiceLifetime);

            return services;
        }

        private static void RegisterPromptService<TService, TImplementation>(
            IServiceCollection services,
            ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService
        {
            var descriptor = new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime);
            services.Replace(descriptor);
        }

        public static IServiceCollection AddNethereumWalletServicesSingleton(
            this IServiceCollection services,
            long defaultChainId = 1,
            Action<IServiceCollection>? configure = null)
        {
            services.AddWalletPromptServices(ServiceLifetime.Singleton);
            services.AddNethereumWalletHostProvider(defaultChainId, ServiceLifetime.Singleton);
            configure?.Invoke(services);
            return services;
        }

        public static IServiceCollection AddNethereumWalletServicesScoped(
            this IServiceCollection services,
            long defaultChainId = 1,
            Action<IServiceCollection>? configure = null)
        {
            services.AddWalletPromptServices(ServiceLifetime.Scoped);
            services.AddNethereumWalletHostProvider(defaultChainId, ServiceLifetime.Scoped);
            configure?.Invoke(services);
            return services;
        }
    }
}
