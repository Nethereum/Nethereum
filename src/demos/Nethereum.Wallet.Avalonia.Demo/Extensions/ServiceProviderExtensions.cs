using Microsoft.Extensions.DependencyInjection;
using System;
using Nethereum.Wallet.UI.Components.CreateAccount;
using Nethereum.Wallet.UI.Components.AccountList;
using Nethereum.Wallet.UI.Components.Dashboard.Services;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey;
using Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly;
using Nethereum.Wallet.UI.Components.WalletOverview;
using Nethereum.Wallet.UI.Components.SendTransaction;
using Nethereum.Wallet.UI.Components.Prompts;

namespace Nethereum.Wallet.Avalonia.Demo.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static void InitializeAccountTypes(this IServiceProvider serviceProvider)
        {
            serviceProvider.ConfigureAccountCreationRegistry();
            serviceProvider.ConfigureAccountDetailsRegistry();
            serviceProvider.ConfigureGroupDetailsRegistry();
            serviceProvider.ConfigureDashboardPluginRegistry();
        }

        public static void ConfigureAccountCreationRegistry(this IServiceProvider serviceProvider)
        {
        }

        public static void ConfigureAccountDetailsRegistry(this IServiceProvider serviceProvider)
        {
        }

        public static void ConfigureGroupDetailsRegistry(this IServiceProvider serviceProvider)
        {
        }

        public static void ConfigureDashboardPluginRegistry(this IServiceProvider serviceProvider)
        {
        }
    }
}