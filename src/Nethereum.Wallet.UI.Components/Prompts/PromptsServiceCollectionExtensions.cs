using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.Shared;
using Nethereum.Wallet.UI.Components.SendTransaction;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public static class PromptsServiceCollectionExtensions
    {
        public static IServiceCollection AddPromptsServices(this IServiceCollection services)
        {
            services.AddTransient<NotificationBadgeViewModel>();
            services.AddSingleton<NotificationBadgeLocalizer>();
            services.AddTransient<IComponentLocalizer<NotificationBadgeViewModel>>(provider =>
                provider.GetRequiredService<NotificationBadgeLocalizer>());
            
            services.AddTransient<IDashboardPluginViewModel, PromptsPluginViewModel>();
            services.AddTransient<PromptsPluginViewModel>();
            services.AddSingleton<PromptsPluginLocalizer>();
            services.AddTransient<IComponentLocalizer<PromptsPluginViewModel>>(provider =>
                provider.GetRequiredService<PromptsPluginLocalizer>());
            
            services.AddTransient<DAppTransactionPromptViewModel>();
            services.AddSingleton<DAppTransactionPromptLocalizer>();
            services.AddTransient<IComponentLocalizer<DAppTransactionPromptViewModel>>(provider =>
                provider.GetRequiredService<DAppTransactionPromptLocalizer>());

            services.AddTransient<DAppPermissionPromptViewModel>();
            services.AddSingleton<DAppPermissionPromptLocalizer>();
            services.AddTransient<IComponentLocalizer<DAppPermissionPromptViewModel>>(provider =>
                provider.GetRequiredService<DAppPermissionPromptLocalizer>());

            return services;
        }
    }
}
