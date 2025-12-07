using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Blazor.Services;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Configuration;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey;
using Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly;
using Nethereum.Wallet.UI.Components.AccountList;
using Nethereum.Wallet.UI.Components.CreateAccount;
using Nethereum.Wallet.UI.Components.Core.Registry;
using Nethereum.Wallet.UI.Components.Blazor.WalletAccounts.Mnemonic;
using Nethereum.Wallet.UI.Components.Blazor.WalletAccounts.PrivateKey;
using Nethereum.Wallet.UI.Components.Blazor.WalletAccounts.ViewOnly;
using Nethereum.Wallet.UI.Components.AccountDetails;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.Blazor.AccountList;
using Nethereum.Wallet.UI.Components.Blazor.CreateAccount;
using Nethereum.Wallet.UI.Components.WalletOverview;
using Nethereum.Wallet.UI.Components.Dashboard.Services;
using Nethereum.Wallet.UI.Components.Blazor.Dashboard;
using Nethereum.Wallet.UI.Components.Networks;
using Nethereum.Wallet.UI.Components.Utils;
using Nethereum.Wallet.UI.Components.SendTransaction;
using Nethereum.Wallet.Services.Transaction;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.UI.Components.Transactions;
using Nethereum.Wallet.UI;
using Nethereum.UI;
using Nethereum.Wallet.UI.Components.Prompts;
using Nethereum.Wallet.UI.Components.Blazor.Prompts;
using Nethereum.Wallet.UI.Components.Shared;
using System;


namespace Nethereum.Wallet.UI.Components.Blazor.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumWalletUIScoped(this IServiceCollection services)
        {
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
                config.SnackbarConfiguration.PreventDuplicates = false;
                config.SnackbarConfiguration.NewestOnTop = false;
                config.SnackbarConfiguration.ShowCloseIcon = true;
                config.SnackbarConfiguration.VisibleStateDuration = 10000;
                config.SnackbarConfiguration.HideTransitionDuration = 500;
                config.SnackbarConfiguration.ShowTransitionDuration = 500;
                config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            });

            services.AddScoped<IWalletNotificationService, BlazorWalletNotificationService>();
            services.TryAddSingleton<SelectedEthereumHostProviderService>();
            services.AddScoped<IWalletDialogService, BlazorWalletDialogService>();
            services.AddSingleton<ILoginSessionState, LoginSessionState>();
            services.AddScoped<ILoginPromptService, LoginPromptService>();

            services.AddScoped<IWalletLoadingService, MudLoadingService>();
            
            services.AddWalletPromptServices(ServiceLifetime.Scoped);
            services.AddScoped<IChainAdditionPromptService, QueuedChainAdditionPromptService>();
            services.AddSingleton<IWalletDialogAccessor, WalletDialogAccessor>();

            services.AddScoped<NethereumWalletViewModel>();
            services.AddScoped<AccountListViewModel>();
            services.AddScoped<CreateAccountViewModel>();
            services.AddScoped<WalletDashboardViewModel>();
            services.AddScoped<WalletOverviewViewModel>();
            
            services.AddPendingTransactionNotifications();

            services.AddSingleton<IComponentRegistry, ComponentRegistry>();
            services.AddScoped<IAccountCreationRegistry, AccountCreationRegistry>();
            services.AddScoped<IAccountDetailsRegistry, AccountDetailsRegistry>();
            services.AddScoped<IGroupDetailsRegistry, GroupDetailsRegistry>();
            
            services.AddSingleton<IAccountTypeMetadataRegistry, AccountTypeMetadataRegistry>();
            services.AddScoped<Nethereum.Wallet.UI.Components.Dashboard.IDashboardPluginRegistry, Nethereum.Wallet.UI.Components.Dashboard.StaticDashboardPluginRegistry>();

            // Register account creation ViewModels as IAccountCreationViewModel
            services.AddScoped<IAccountCreationViewModel, MnemonicAccountCreationViewModel>();
            services.AddScoped<IAccountCreationViewModel, PrivateKeyAccountCreationViewModel>();
            services.AddScoped<IAccountCreationViewModel, ViewOnlyAccountCreationViewModel>();
            services.AddScoped<IAccountCreationViewModel, VaultMnemonicAccountViewModel>();

            services.AddScoped<VaultMnemonicAccountViewModel>();
            services.AddScoped<MnemonicDetailsViewModel>();
            services.AddScoped<MnemonicListViewModel>();

            // Register dashboard plugin ViewModels as IDashboardPluginViewModel
            services.AddScoped<IDashboardPluginViewModel, AccountListPluginViewModel>();
            services.AddScoped<IDashboardPluginViewModel, CreateAccountPluginViewModel>();
            services.AddScoped<IDashboardPluginViewModel, WalletOverviewPluginViewModel>();

            services.AddScoped<IAccountDetailsViewModel, MnemonicAccountDetailsViewModel>();
            services.AddScoped<MnemonicAccountDetailsViewModel>();
            services.AddScoped<IAccountDetailsViewModel, PrivateKeyAccountDetailsViewModel>();
            services.AddScoped<PrivateKeyAccountDetailsViewModel>();
            services.AddScoped<IAccountDetailsViewModel, ViewOnlyAccountDetailsViewModel>();
            services.AddScoped<ViewOnlyAccountDetailsViewModel>();

            services.AddScoped<IGroupDetailsViewModel, MnemonicDetailsViewModel>();

            RegisterWalletUILocalization(services);

            services.AddSingleton<NethereumWalletConfiguration>();

            services.AddSingleton<MnemonicAccountDetailsConfiguration>();

            services.AddTransient<TransactionViewModel>();
            services.AddTransient<SendNativeTokenViewModel>();
            
            services.AddSingleton<WalletOverviewConfiguration>();
            services.AddSingleton<AccountListConfiguration>(); 
            services.AddSingleton<BaseWalletConfiguration>(sp => sp.GetRequiredService<AccountListConfiguration>());

            services.AddNetworkManagement();

            services.AddTokenTransferServices();

            services.AddTransactionServices();

            services.AddPromptsServices();

            services.AddScoped<WalletUIBootstrapper>();

            services.AddScoped<IGasConfigurationPersistenceService, LocalStorageGasConfigurationPersistenceService>();

            // Register default network icon provider (can be overridden by applications)
            services.AddSingleton<INetworkIconProvider, DefaultNetworkIconProvider>();


            return services;
        }

        public static IServiceCollection AddNethereumWalletUI(this IServiceCollection services)
            => services.AddNethereumWalletUIScoped();

        public static IServiceCollection AddNethereumWalletUISingleton(this IServiceCollection services)
        {
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
                config.SnackbarConfiguration.PreventDuplicates = false;
                config.SnackbarConfiguration.NewestOnTop = false;
                config.SnackbarConfiguration.ShowCloseIcon = true;
                config.SnackbarConfiguration.VisibleStateDuration = 10000;
                config.SnackbarConfiguration.HideTransitionDuration = 500;
                config.SnackbarConfiguration.ShowTransitionDuration = 500;
                config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            });

            services.AddScoped<IWalletNotificationService, BlazorWalletNotificationService>();
            services.TryAddSingleton<SelectedEthereumHostProviderService>();
            services.AddScoped<IWalletDialogService, BlazorWalletDialogService>();
            services.TryAddSingleton<ILoginSessionState, LoginSessionState>();
            services.AddScoped<ILoginPromptService, LoginPromptService>();
            services.AddScoped<IWalletLoadingService, MudLoadingService>();

            services.AddWalletPromptServices(ServiceLifetime.Singleton);
            services.AddSingleton<IChainAdditionPromptService, QueuedChainAdditionPromptService>();
            services.AddSingleton<IWalletDialogAccessor, WalletDialogAccessor>();

            services.AddTransient<NethereumWalletViewModel>();
            services.AddTransient<AccountListViewModel>();
            services.AddTransient<CreateAccountViewModel>();
            services.AddTransient<WalletDashboardViewModel>();
            services.AddTransient<WalletOverviewViewModel>();

            services.AddPendingTransactionNotifications();
            services.AddScoped<WalletUIBootstrapper>();

            services.AddSingleton<IComponentRegistry, ComponentRegistry>();
            services.AddSingleton<IAccountCreationRegistry, AccountCreationRegistry>();
            services.AddSingleton<IAccountDetailsRegistry, AccountDetailsRegistry>();
            services.AddSingleton<IAccountTypeMetadataRegistry, AccountTypeMetadataRegistry>();
            services.AddSingleton<Nethereum.Wallet.UI.Components.Dashboard.IDashboardPluginRegistry, Nethereum.Wallet.UI.Components.Dashboard.StaticDashboardPluginRegistry>();

            services.AddTransient<IAccountCreationViewModel, MnemonicAccountCreationViewModel>();
            services.AddTransient<IAccountCreationViewModel, PrivateKeyAccountCreationViewModel>();
            services.AddTransient<IAccountCreationViewModel, ViewOnlyAccountCreationViewModel>();
            services.AddTransient<IAccountCreationViewModel, VaultMnemonicAccountViewModel>();

            services.AddTransient<VaultMnemonicAccountViewModel>();
            services.AddTransient<MnemonicDetailsViewModel>();
            services.AddTransient<MnemonicListViewModel>();

            services.AddTransient<IDashboardPluginViewModel, AccountListPluginViewModel>();
            services.AddTransient<IDashboardPluginViewModel, CreateAccountPluginViewModel>();
            services.AddTransient<IDashboardPluginViewModel, WalletOverviewPluginViewModel>();

            services.AddTransient<IAccountDetailsViewModel, MnemonicAccountDetailsViewModel>();
            services.AddTransient<MnemonicAccountDetailsViewModel>();
            services.AddTransient<IAccountDetailsViewModel, PrivateKeyAccountDetailsViewModel>();
            services.AddTransient<PrivateKeyAccountDetailsViewModel>();
            services.AddTransient<IAccountDetailsViewModel, ViewOnlyAccountDetailsViewModel>();
            services.AddTransient<ViewOnlyAccountDetailsViewModel>();

            services.AddSingleton<IGroupDetailsRegistry, GroupDetailsRegistry>();
            services.AddTransient<IGroupDetailsViewModel, MnemonicDetailsViewModel>();

            RegisterWalletUILocalization(services);

            services.AddSingleton<NethereumWalletConfiguration>();
            services.AddSingleton<MnemonicAccountDetailsConfiguration>();

            services.AddTransient<TransactionViewModel>();
            services.AddTransient<SendNativeTokenViewModel>();

            services.AddSingleton<WalletOverviewConfiguration>();
            services.AddSingleton<AccountListConfiguration>();
            services.AddSingleton<BaseWalletConfiguration>(sp => sp.GetRequiredService<AccountListConfiguration>());

            services.AddNetworkManagement();
            services.AddTokenTransferServices();
            services.AddTransactionServices();
            services.AddPromptsServices();
            services.AddScoped<WalletUIBootstrapper>();

            services.AddSingleton<IGasConfigurationPersistenceService, LocalStorageGasConfigurationPersistenceService>();
            services.AddSingleton<INetworkIconProvider, DefaultNetworkIconProvider>();

            return services;
        }

        private static void RegisterWalletUILocalization(IServiceCollection services)
        {
            services.TryAddSingleton<ILocalizationStorageProvider, BrowserLocalizationStorageProvider>();
            services.TryAddSingleton<IWalletLocalizationService>(provider =>
            {
                var storageProvider = provider.GetService<ILocalizationStorageProvider>();
                return new WalletLocalizationService(storageProvider);
            });

            services.TryAddSingleton<IComponentLocalizer<NethereumWalletViewModel>, NethereumWalletLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<MnemonicAccountCreationViewModel>, MnemonicAccountEditorLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<VaultMnemonicAccountViewModel>, VaultMnemonicAccountEditorLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<PrivateKeyAccountCreationViewModel>, PrivateKeyAccountEditorLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<ViewOnlyAccountCreationViewModel>, ViewOnlyAccountEditorLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<AccountListViewModel>, AccountListLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<AccountListPluginViewModel>, AccountListPluginLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<CreateAccountViewModel>, CreateAccountLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<CreateAccountPluginViewModel>, CreateAccountPluginLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<WalletDashboardViewModel>, WalletDashboardLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<WalletOverviewViewModel>, WalletOverviewLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<WalletOverviewPluginViewModel>, WalletOverviewPluginLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<MnemonicAccountDetailsViewModel>, MnemonicAccountDetailsLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<MnemonicDetailsViewModel>, MnemonicDetailsViewModelLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<MnemonicListViewModel>, MnemonicListViewModelLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<PrivateKeyAccountDetailsViewModel>, PrivateKeyAccountDetailsLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<ViewOnlyAccountDetailsViewModel>, ViewOnlyAccountDetailsLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<SendNativeTokenViewModel>, SendNativeTokenLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<TransactionViewModel>, TransactionLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<DAppSignaturePromptView>, DAppSignaturePromptLocalizer>();
            services.TryAddSingleton<IComponentLocalizer<DAppChainSwitchPromptView>, DAppChainSwitchPromptLocalizer>();
        }

        public static IServiceCollection AddNethereumWalletUICore(this IServiceCollection services)
        {
            services.AddTransient<NethereumWalletViewModel>();
            services.AddTransient<AccountListViewModel>();
            services.AddTransient<CreateAccountViewModel>();

            // Note: Account creation system is now handled by unified registry with IAccountCreationViewModel

            return services;
        }
        public static void InitializeAccountTypes(this IServiceProvider serviceProvider)
        {
            serviceProvider.ConfigureAccountCreationRegistry();
            serviceProvider.ConfigureAccountDetailsRegistry();
            serviceProvider.ConfigureGroupDetailsRegistry();
            serviceProvider.ConfigureDashboardPluginRegistry();
        }
        public static void ConfigureAccountCreationRegistry(this IServiceProvider serviceProvider)
        {
            var registry = serviceProvider.GetRequiredService<IAccountCreationRegistry>();

            registry.Register<MnemonicAccountCreationViewModel, MnemonicAccountCreation>();
            registry.Register<PrivateKeyAccountCreationViewModel, PrivateKeyAccountCreation>();
            registry.Register<ViewOnlyAccountCreationViewModel, ViewOnlyAccountCreation>();
            registry.Register<VaultMnemonicAccountViewModel, VaultMnemonicAccountEditor>();
        }
        public static void ConfigureAccountDetailsRegistry(this IServiceProvider serviceProvider)
        {
            var registry = serviceProvider.GetRequiredService<IAccountDetailsRegistry>();

            registry.Register<MnemonicAccountDetailsViewModel, MnemonicAccountDetails>();
            registry.Register<PrivateKeyAccountDetailsViewModel, PrivateKeyAccountDetails>();
            registry.Register<ViewOnlyAccountDetailsViewModel, ViewOnlyAccountDetails>();
            // registry.Register<SmartContractAccountDetailsViewModel, SmartContractAccountDetails>();
        }
        public static void ConfigureGroupDetailsRegistry(this IServiceProvider serviceProvider)
        {
            var registry = serviceProvider.GetRequiredService<IGroupDetailsRegistry>();

            // Register Group type to ViewModel and Component mappings
            registry.Register<MnemonicDetailsViewModel, MnemonicDetailsView>();
        }
        public static void ConfigureDashboardPluginRegistry(this IServiceProvider serviceProvider)
        {
            var componentRegistry = serviceProvider.GetRequiredService<IComponentRegistry>();
            
            componentRegistry.Register<AccountListPluginViewModel, Nethereum.Wallet.UI.Components.Blazor.AccountList.AccountList>();
            componentRegistry.Register<CreateAccountPluginViewModel, Nethereum.Wallet.UI.Components.Blazor.CreateAccount.CreateAccount>();
            componentRegistry.Register<WalletOverviewPluginViewModel, Nethereum.Wallet.UI.Components.Blazor.WalletOverview.WalletOverview>();
            componentRegistry.Register<NetworkManagementPluginViewModel, Nethereum.Wallet.UI.Components.Blazor.Networks.NetworkManagement>();
            componentRegistry.Register<SendNativeTokenViewModel, Nethereum.Wallet.UI.Components.Blazor.SendTransaction.TokenTransfer>();
            componentRegistry.Register<PromptsPluginViewModel, Nethereum.Wallet.UI.Components.Blazor.Prompts.PromptsPlugin>();
        }

    }
}

