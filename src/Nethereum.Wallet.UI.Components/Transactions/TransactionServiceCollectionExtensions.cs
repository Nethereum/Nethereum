using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public static class TransactionServiceCollectionExtensions
    {
        public static IServiceCollection AddTransactionServices(this IServiceCollection services)
        {
            services.AddScoped<IPendingTransactionService, PendingTransactionService>();
            
            services.AddTransient<TransactionHistoryViewModel>();
            
            services.AddSingleton<TransactionHistoryLocalizer>();
            services.AddTransient<IComponentLocalizer<TransactionHistoryViewModel>>(provider =>
                provider.GetRequiredService<TransactionHistoryLocalizer>());
            
            services.AddHostedService<TransactionMonitoringService>();
            
            return services;
        }
    }
}