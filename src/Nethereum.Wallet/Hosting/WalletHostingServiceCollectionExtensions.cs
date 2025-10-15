using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.UI;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services;
using System;

namespace Nethereum.Wallet.Hosting
{
    public static class WalletHostingServiceCollectionExtensions
    {
        
        public static IServiceCollection AddNethereumWalletHostProvider(this IServiceCollection services, long defaultChainId = 1, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            services.AddSingleton<RpcHandlerRegistry>();

            services.TryAdd(new ServiceDescriptor(typeof(IDappPermissionService), typeof(PermissiveDappPermissionService), lifetime));
            services.TryAdd(new ServiceDescriptor(typeof(IDappPermissionPromptService), typeof(NoOpDappPermissionPromptService), lifetime));
            services.TryAdd(new ServiceDescriptor(typeof(IChainAdditionPromptService), typeof(NoOpChainAdditionPromptService), lifetime));
            services.TryAdd(new ServiceDescriptor(typeof(IChainSwitchPromptService), typeof(NoOpChainSwitchPromptService), lifetime));

            var providerDescriptor = new ServiceDescriptor(typeof(NethereumWalletHostProvider), sp =>
                new NethereumWalletHostProvider(
                    sp.GetRequiredService<IWalletVaultService>(),
                    sp.GetRequiredService<IRpcClientFactory>(),
                    sp.GetRequiredService<IWalletStorageService>(),
                    sp.GetRequiredService<IChainManagementService>(),
                    sp.GetRequiredService<RpcHandlerRegistry>(),
                    sp.GetRequiredService<ITransactionPromptService>(),
                    sp.GetRequiredService<ISignaturePromptService>(),
                    sp.GetRequiredService<IWalletConfigurationService>(),
                    sp.GetRequiredService<ILoginPromptService>(),
                    sp.GetRequiredService<IDappPermissionService>(),
                    sp.GetRequiredService<IDappPermissionPromptService>(),
                    sp.GetRequiredService<IChainAdditionPromptService>(),
                    sp.GetRequiredService<IChainSwitchPromptService>(),
                    defaultChainId),
                lifetime);

            services.TryAdd(providerDescriptor);

            // Expose as generic host provider for polymorphic resolution (MetaMask / WalletConnect etc.)
            services.TryAdd(new ServiceDescriptor(typeof(IEthereumHostProvider), sp => sp.GetRequiredService<NethereumWalletHostProvider>(), lifetime));

            // Expose as IWalletContext 
            services.TryAdd(new ServiceDescriptor(typeof(IWalletContext), sp => sp.GetRequiredService<NethereumWalletHostProvider>(), lifetime));

            return services;
        }


    }
}
