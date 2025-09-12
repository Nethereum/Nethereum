using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public static class PendingTransactionNotificationServiceCollectionExtensions
    {
        public static IServiceCollection AddPendingTransactionNotifications(this IServiceCollection services)
        {
            services.AddSingleton<PendingTransactionNotificationLocalizer>();
            services.AddTransient<IComponentLocalizer<PendingTransactionNotificationService>>(provider =>
                provider.GetRequiredService<PendingTransactionNotificationLocalizer>());
            
            services.AddScoped<PendingTransactionNotificationService>();
            
            return services;
        }
    }
}