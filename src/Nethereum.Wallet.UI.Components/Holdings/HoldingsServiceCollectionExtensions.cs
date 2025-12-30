using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public static class HoldingsServiceCollectionExtensions
    {
        public static IServiceCollection AddHoldingsServices(this IServiceCollection services)
        {
            services.TryAddSingleton<IHoldingsSettingsStorage>(provider =>
            {
                return new FileHoldingsSettingsStorage();
            });

            services.AddTransient<HoldingsViewModel>();
            services.AddTransient<EditHoldingsViewModel>();

            services.AddSingleton<HoldingsLocalizer>();
            services.AddSingleton<EditHoldingsLocalizer>();

            services.AddSingleton<IComponentLocalizer<HoldingsViewModel>>(provider =>
                provider.GetRequiredService<HoldingsLocalizer>());
            services.AddSingleton<IComponentLocalizer<EditHoldingsViewModel>>(provider =>
                provider.GetRequiredService<EditHoldingsLocalizer>());

            services.AddScoped<HoldingsPluginViewModel>();
            services.AddScoped<IDashboardPluginViewModel, HoldingsPluginViewModel>();
            services.AddSingleton<HoldingsPluginLocalizer>();
            services.AddSingleton<IComponentLocalizer<HoldingsPluginViewModel>>(provider =>
                provider.GetRequiredService<HoldingsPluginLocalizer>());

            return services;
        }
    }
}
