using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.DataServices.FourByteDirectory;
using Nethereum.Wallet.Services.Transaction;
using Nethereum.Wallet.UI.Components.SendTransaction.Components;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Validation;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public static class TokenTransferServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenTransferServices(this IServiceCollection services)
        {
            
            services.AddTransient<FourByteDirectoryService>();
            services.AddTransient<ITransactionDataDecodingService, FourByteDataDecodingService>();
            
            services.AddTransient<IGasPriceProvider, NethereumGasPriceProvider>();
            services.TryAddSingleton<IGasConfigurationPersistenceService, InMemoryGasConfigurationPersistenceService>();
            
            services.AddTransient<TokenNativeTransferModel>();
            services.AddTransient<TransactionModel>();
            
            services.AddTransient<TransactionViewModel>();
            services.AddTransient<SendNativeTokenViewModel>();
            services.AddTransient<TransactionStatusViewModel>();
            
            // ViewModels - OBSOLETE (commented out until removal)
            
            // Register shared validation localizer (if not already registered)
            services.AddSingleton<SharedValidationLocalizer>();
            
            services.AddTransient<IComponentLocalizer>(provider =>
                provider.GetRequiredService<SharedValidationLocalizer>());
            
            services.AddSingleton<TransactionLocalizer>();
            services.AddSingleton<SendNativeTokenLocalizer>();
            services.AddSingleton<TransactionStatusLocalizer>();
            
            // Localizers - OBSOLETE (commented out until removal)
            
            services.AddTransient<IComponentLocalizer<TransactionViewModel>>(provider =>
                provider.GetRequiredService<TransactionLocalizer>());
            services.AddTransient<IComponentLocalizer<SendNativeTokenViewModel>>(provider =>
                provider.GetRequiredService<SendNativeTokenLocalizer>());
            services.AddTransient<IComponentLocalizer<TransactionStatusViewModel>>(provider =>
                provider.GetRequiredService<TransactionStatusLocalizer>());
            
            // Component Localizers - OBSOLETE (commented out until removal)
            //     provider.GetRequiredService<TokenTransferLocalizer>());
            //     provider.GetRequiredService<TransactionInputLocalizer>());
            //     provider.GetRequiredService<TransactionConfirmationLocalizer>();
            
            return services;
        }
    }
}