using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.UI.Components.Networks;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Blazor.Extensions
{
    public static class NetworkServiceCollectionExtensions
    {
        public static IServiceCollection AddNetworkManagement(this IServiceCollection services)
        {
            // Register core network service (this should be registered at a higher level)
            
            services.AddTransient<NetworkListViewModel>();
            services.AddTransient<NetworkDetailsViewModel>();
            services.AddTransient<AddCustomNetworkViewModel>();
            
            services.AddScoped<NetworkManagementPluginViewModel>();
            services.AddScoped<IDashboardPluginViewModel, NetworkManagementPluginViewModel>();
            
            services.AddSingleton<NetworkListLocalizer>();
            services.AddSingleton<NetworkDetailsLocalizer>();
            services.AddSingleton<AddCustomNetworkLocalizer>();
            services.AddSingleton<NetworkManagementPluginLocalizer>();
            
            services.AddTransient<IComponentLocalizer<NetworkListViewModel>>(provider =>
                provider.GetRequiredService<NetworkListLocalizer>());
            services.AddTransient<IComponentLocalizer<NetworkDetailsViewModel>>(provider =>
                provider.GetRequiredService<NetworkDetailsLocalizer>());
            services.AddTransient<IComponentLocalizer<AddCustomNetworkViewModel>>(provider =>
                provider.GetRequiredService<AddCustomNetworkLocalizer>());
            services.AddTransient<IComponentLocalizer<NetworkManagementPluginViewModel>>(provider =>
                provider.GetRequiredService<NetworkManagementPluginLocalizer>());
            
            return services;
        }
    }
}