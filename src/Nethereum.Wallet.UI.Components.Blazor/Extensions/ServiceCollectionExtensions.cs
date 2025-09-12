using Microsoft.Extensions.DependencyInjection;
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
using Nethereum.Wallet.UI.Components.Prompts;
using Nethereum.Wallet.UI.Components.Shared;

namespace Nethereum.Wallet.UI.Components.Blazor.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumWalletUI(this IServiceCollection services)
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
            services.AddScoped<IWalletDialogService, BlazorWalletDialogService>();

            services.AddScoped<IWalletLoadingService, MudLoadingService>();
            
            services.AddSingleton<IPromptQueueService, PromptQueueService>();
            services.AddSingleton<IPromptOverlayService, PromptOverlayService>();
            
            services.AddScoped<Nethereum.Wallet.UI.ITransactionPromptService, QueuedTransactionPromptService>();
            services.AddScoped<Nethereum.Wallet.UI.ISignaturePromptService, QueuedSignaturePromptService>();

            services.AddScoped<NethereumWalletViewModel>();
            services.AddScoped<AccountListViewModel>();
            services.AddScoped<CreateAccountViewModel>();
            services.AddScoped<WalletDashboardViewModel>();
            services.AddScoped<WalletOverviewViewModel>();
            
            services.AddPendingTransactionNotifications();

            services.AddSingleton<IComponentRegistry, ComponentRegistry>();
            services.AddScoped<IAccountCreationRegistry, AccountCreationRegistry>();
            services.AddScoped<IAccountDetailsRegistry, AccountDetailsRegistry>();
            
            services.AddSingleton<IAccountTypeMetadataRegistry, AccountTypeMetadataRegistry>();
            services.AddScoped<Nethereum.Wallet.UI.Components.Dashboard.IDashboardPluginRegistry, Nethereum.Wallet.UI.Components.Dashboard.DashboardPluginRegistry>();

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

            services.AddScoped<IGroupDetailsRegistry, GroupDetailsRegistry>();
            services.AddScoped<IGroupDetailsViewModel, MnemonicDetailsViewModel>();

            services.AddSingleton<ILocalizationStorageProvider, BrowserLocalizationStorageProvider>();
            services.AddSingleton<IWalletLocalizationService>(provider =>
            {
                var storageProvider = provider.GetService<ILocalizationStorageProvider>();
                return new WalletLocalizationService(storageProvider);
            });
            
            services.AddSingleton<IComponentLocalizer<NethereumWalletViewModel>, NethereumWalletLocalizer>();
            services.AddSingleton<IComponentLocalizer<MnemonicAccountCreationViewModel>, MnemonicAccountEditorLocalizer>();
            services.AddSingleton<IComponentLocalizer<VaultMnemonicAccountViewModel>, VaultMnemonicAccountEditorLocalizer>();
            services.AddSingleton<IComponentLocalizer<PrivateKeyAccountCreationViewModel>, PrivateKeyAccountEditorLocalizer>();
            services.AddSingleton<IComponentLocalizer<ViewOnlyAccountCreationViewModel>, ViewOnlyAccountEditorLocalizer>();
            services.AddSingleton<IComponentLocalizer<AccountListViewModel>, AccountListLocalizer>();
            services.AddSingleton<IComponentLocalizer<MnemonicAccountDetailsViewModel>, MnemonicAccountDetailsLocalizer>();
            services.AddSingleton<IComponentLocalizer<MnemonicDetailsViewModel>, MnemonicDetailsViewModelLocalizer>();
            services.AddSingleton<IComponentLocalizer<MnemonicListViewModel>, MnemonicListViewModelLocalizer>();
            services.AddSingleton<IComponentLocalizer<PrivateKeyAccountDetailsViewModel>, PrivateKeyAccountDetailsLocalizer>();
            services.AddSingleton<IComponentLocalizer<ViewOnlyAccountDetailsViewModel>, ViewOnlyAccountDetailsLocalizer>();
            services.AddSingleton<IComponentLocalizer<WalletDashboardViewModel>, WalletDashboardLocalizer>();
            services.AddSingleton<IComponentLocalizer<WalletOverviewViewModel>, WalletOverviewLocalizer>();
            
            services.AddSingleton<IComponentLocalizer<AccountListViewModel>, AccountListLocalizer>();
            services.AddSingleton<IComponentLocalizer<AccountListPluginViewModel>, AccountListPluginLocalizer>();
            services.AddSingleton<IComponentLocalizer<CreateAccountViewModel>, CreateAccountLocalizer>();
            services.AddSingleton<IComponentLocalizer<CreateAccountPluginViewModel>, CreateAccountPluginLocalizer>();
            services.AddSingleton<IComponentLocalizer<WalletOverviewViewModel>, WalletOverviewLocalizer>();
            services.AddSingleton<IComponentLocalizer<WalletOverviewPluginViewModel>, WalletOverviewPluginLocalizer>();
            
            services.AddSingleton<NethereumWalletConfiguration>();
            
            services.AddSingleton<MnemonicAccountDetailsConfiguration>();
            
            services.AddTransient<TransactionViewModel>();
            services.AddTransient<SendNativeTokenViewModel>();
            services.AddSingleton<IComponentLocalizer<SendNativeTokenViewModel>, SendNativeTokenLocalizer>();
            services.AddSingleton<IComponentLocalizer<TransactionViewModel>, TransactionLocalizer>();
            
            services.AddSingleton<WalletOverviewConfiguration>();
            services.AddSingleton<AccountListConfiguration>(); 
            services.AddSingleton<BaseWalletConfiguration>(sp => sp.GetRequiredService<AccountListConfiguration>());

            services.AddNetworkManagement();
            
            services.AddTokenTransferServices();
            
            services.AddTransactionServices();
            
            services.AddPromptsServices();
            
            services.AddScoped<IGasConfigurationPersistenceService, LocalStorageGasConfigurationPersistenceService>();

            // Register default network icon provider (can be overridden by applications)
            services.AddSingleton<INetworkIconProvider, DefaultNetworkIconProvider>();

            return services;
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