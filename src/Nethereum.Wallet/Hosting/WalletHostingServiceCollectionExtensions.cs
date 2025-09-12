using Microsoft.Extensions.DependencyInjection;
using Nethereum.UI;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.Services.Network;
using System;

namespace Nethereum.Wallet.Hosting
{
    public static class WalletHostingServiceCollectionExtensions
    {
        
        public static IServiceCollection AddNethereumWalletHostProvider(this IServiceCollection services, long defaultChainId = 1)
        {
            
            services.AddSingleton<RpcHandlerRegistry>();

            
            services.AddScoped<NethereumWalletHostProvider>(sp =>
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
                    defaultChainId));

            // Expose as generic host provider for polymorphic resolution (MetaMask / WalletConnect etc.)
            services.AddScoped<IEthereumHostProvider>(sp => sp.GetRequiredService<NethereumWalletHostProvider>());

            // Expose as IWalletContext 
            services.AddScoped<IWalletContext>(sp => sp.GetRequiredService<NethereumWalletHostProvider>());

            return services;
        }

       
    }
}