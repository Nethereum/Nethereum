using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;

namespace Nethereum.Wallet.UI.Components.Blazor.NethereumWallet
{
    public static class NethereumWalletServiceExtensions
    {
        public static IServiceCollection AddWalletConnector(this IServiceCollection services)
        {
            return services.AddWalletConnector(config => { });
        }
        public static IServiceCollection AddWalletConnector(
            this IServiceCollection services, 
            Action<NethereumWalletConfiguration> configureOptions)
        {
            var configuration = new NethereumWalletConfiguration();
            configureOptions(configuration);
            
            services.AddSingleton(configuration);
            services.AddSingleton<IComponentLocalizer<NethereumWalletViewModel>, NethereumWalletLocalizer>();
            
            return services;
        }
        public static IServiceCollection AddWalletConnectorWithLocalization(
            this IServiceCollection services,
            Action<NethereumWalletConfiguration> configureOptions)
        {
            var configuration = new NethereumWalletConfiguration();
            configureOptions(configuration);
            
            services.AddSingleton(configuration);
            services.AddSingleton<IComponentLocalizer<NethereumWalletViewModel>, NethereumWalletLocalizer>();
            
            return services;
        }
        public static NethereumWalletConfiguration CreateSimpleConfiguration(
            string? loginTitle = null,
            bool enableWalletReset = false,
            bool allowPasswordVisibilityToggle = true)
        {
            var config = new NethereumWalletConfiguration();
            config.Text.LoginTitle = loginTitle ?? "Welcome Back";
            config.Behavior.EnableWalletReset = enableWalletReset;
            config.AllowPasswordVisibilityToggle = allowPasswordVisibilityToggle;
            return config;
        }
        public static NethereumWalletConfiguration CreateAdvancedConfiguration(
            string? brandingTitle = null,
            bool enableProgressIndicators = true,
            bool enableWalletReset = true)
        {
            var config = new NethereumWalletConfiguration();
            config.FlowMode = Nethereum.Wallet.UI.Components.Core.Configuration.WalletFlowMode.Advanced;
            config.Text.ConnectWalletTitle = brandingTitle ?? "Connect Your Wallet";
            config.Behavior.EnableWalletReset = enableWalletReset;
            config.ShowProgressIndicators = enableProgressIndicators;
            return config;
        }
        public static NethereumWalletConfiguration CreateMinimalConfiguration()
        {
            var config = new NethereumWalletConfiguration();
            config.FlowMode = Nethereum.Wallet.UI.Components.Core.Configuration.WalletFlowMode.Simple;
            config.Behavior.EnableWalletReset = false;
            config.ShowProgressIndicators = false;
            return config;
        }
        public static NethereumWalletConfiguration CreateMobileConfiguration()
        {
            var config = new NethereumWalletConfiguration();
            config.FlowMode = Nethereum.Wallet.UI.Components.Core.Configuration.WalletFlowMode.Simple;
            config.Behavior.AutoFocusPasswordField = false;
            config.EnableKeyboardShortcuts = false;
            config.ShowProgressIndicators = true;
            return config;
        }
    }
}