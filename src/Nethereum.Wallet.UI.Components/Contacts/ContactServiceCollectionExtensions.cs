using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Wallet.Services.Contacts;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Contacts
{
    public static class ContactServiceCollectionExtensions
    {
        public static IServiceCollection AddContactServices(this IServiceCollection services)
        {
            services.TryAddSingleton<IContactService>(provider =>
            {
                return new FileContactStorage();
            });

            services.AddTransient<ContactListViewModel>();

            services.AddSingleton<ContactListLocalizer>();
            services.AddSingleton<IComponentLocalizer<ContactListViewModel>>(provider =>
                provider.GetRequiredService<ContactListLocalizer>());

            services.AddScoped<ContactListPluginViewModel>();
            services.AddScoped<IDashboardPluginViewModel, ContactListPluginViewModel>();

            return services;
        }
    }
}
