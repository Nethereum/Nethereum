using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey;
using Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly;
using Nethereum.Wallet.Services;

namespace Nethereum.Wallet.UI.Components.Configuration
{
    public static class NethereumWalletUIConfigurationRegistry
    {
        public static IServiceCollection AddNethereumWalletUIConfiguration(
            this IServiceCollection services, 
            Action<NethereumWalletUIConfiguration>? configure = null,
            string? mainnetRpcUrl = null)
        {
            var globalConfig = new NethereumWalletUIConfiguration();
            configure?.Invoke(globalConfig);
            
            services.AddSingleton<INethereumWalletUIConfiguration>(globalConfig);
            
            services.AddSingleton<NethereumWalletConfiguration>(globalConfig.WalletConfig);
            
            services.AddSingleton<IAccountTypeMetadataRegistry, AccountTypeMetadataRegistry>();
            services.AddTransient<IAccountMetadataViewModel, MnemonicAccountMetadataViewModel>();
            services.AddTransient<IAccountMetadataViewModel, PrivateKeyAccountMetadataViewModel>();
            services.AddTransient<IAccountMetadataViewModel, ViewOnlyAccountMetadataViewModel>();
            
            // Register ENS service with mainnet RPC (only if provided)
            if (!string.IsNullOrEmpty(mainnetRpcUrl))
            {
                services.AddSingleton<IEnsService>(provider => new EnsService(mainnetRpcUrl));
            }
            
            // Register centralized viewport service for responsive behavior
            services.AddSingleton<IViewportService, ViewportService>();
            
            return services;
        }
    }
}