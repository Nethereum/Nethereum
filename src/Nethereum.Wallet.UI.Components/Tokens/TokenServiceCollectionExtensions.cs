using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public static class TokenServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenServices(this IServiceCollection services)
        {
            return services.AddTokenServices(null);
        }

        public static IServiceCollection AddTokenServices(this IServiceCollection services, Action<TokenStorageOptions> configureOptions)
        {
            var storageOptions = new TokenStorageOptions();
            configureOptions?.Invoke(storageOptions);

            services.TryAddSingleton(storageOptions);

            services.TryAddSingleton<ITokenStorageService>(provider =>
            {
                var options = provider.GetService<TokenStorageOptions>() ?? TokenStorageOptions.Default;
                return new FileTokenStorageService(options.BaseDirectory);
            });

            services.AddWalletTokenServices(options =>
            {
                options.ConfigureFromServiceProvider = true;
                options.UseFileCache = true;
            });

            services.AddTransient<TokenListViewModel>();
            services.AddTransient<AddCustomTokenViewModel>();
            services.AddTransient<TokenSettingsViewModel>();

            services.AddSingleton<TokenListLocalizer>();
            services.AddSingleton<AddCustomTokenLocalizer>();
            services.AddSingleton<TokenSettingsLocalizer>();

            services.AddTransient<IComponentLocalizer<TokenListViewModel>>(provider =>
                provider.GetRequiredService<TokenListLocalizer>());
            services.AddTransient<IComponentLocalizer<AddCustomTokenViewModel>>(provider =>
                provider.GetRequiredService<AddCustomTokenLocalizer>());
            services.AddTransient<IComponentLocalizer<TokenSettingsViewModel>>(provider =>
                provider.GetRequiredService<TokenSettingsLocalizer>());

            return services;
        }
    }
}
