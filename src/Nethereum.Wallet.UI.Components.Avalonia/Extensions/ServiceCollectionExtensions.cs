using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Avalonia.Options;
using Nethereum.Wallet.UI.Components.Avalonia.Services;
using Nethereum.Wallet.UI.Components.Avalonia.ViewModels;
using Nethereum.Wallet.UI.Components.Avalonia.Views;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.AccountList;
using Nethereum.Wallet.UI.Components.CreateAccount;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Transactions;
using Nethereum.Wallet.UI.Components.Abstractions;

namespace Nethereum.Wallet.UI.Components.Avalonia.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumWalletAvaloniaComponents(this IServiceCollection services, Action<AvaloniaWalletComponentOptions>? configure = null)
        {
            var options = new AvaloniaWalletComponentOptions();
            configure?.Invoke(options);

            services.TryAddSingleton(options);

            services.TryAddSingleton<IWalletVaultService, AvaloniaWalletVaultService>();
            services.TryAddSingleton<IWalletStorageService, AvaloniaWalletStorageService>();
            services.TryAddSingleton<IDappPermissionService, DefaultDappPermissionService>();
            services.TryAddScoped<IWalletDialogService, AvaloniaWalletDialogService>();

            services.AddWalletPromptServices(ServiceLifetime.Singleton);
            services.TryAddScoped<IChainAdditionPromptService, QueuedChainAdditionPromptService>();

            // Add login session management services
            services.TryAddSingleton<ILoginSessionState, LoginSessionState>();
            services.TryAddScoped<ILoginPromptService, LoginPromptService>();

            // Add localization services
            services.TryAddSingleton<ILocalizationStorageProvider, SystemLocalizationStorageProvider>();
            services.TryAddSingleton<IWalletLocalizationService>(provider =>
            {
                var storageProvider = provider.GetService<ILocalizationStorageProvider>();
                return new WalletLocalizationService(storageProvider);
            });

            // Add component localizers
            services.AddSingleton<NethereumWalletLocalizer>();
            services.AddSingleton<IComponentLocalizer<NethereumWalletViewModel>>(provider =>
                provider.GetRequiredService<NethereumWalletLocalizer>());

            services.AddNethereumWalletServicesSingleton(options.DefaultChainId);
            services.AddPendingTransactionNotifications();

            // Core UI services - equivalent to AddNethereumWalletUICore
            services.AddTransient<NethereumWalletViewModel>();
            services.AddTransient<AccountListViewModel>();
            services.AddTransient<CreateAccountViewModel>();
            services.AddTransient<WalletHostViewModel>();
            services.AddTransient<WalletHostControl>();

            services.AddSingleton<IHostedService, RpcHandlerBootstrapper>();

            return services;
        }
    }

    internal class RpcHandlerBootstrapper : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public RpcHandlerBootstrapper(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var registry = scope.ServiceProvider.GetService<RpcHandlerRegistry>();
            if (registry != null)
            {
                WalletRpcHandlerRegistration.RegisterAll(registry);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
