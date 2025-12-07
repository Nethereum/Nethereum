using System;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;
using Nethereum.Wallet.UI.Components.Blazor.Trezor.WalletAccounts;
using Nethereum.Wallet.UI.Components.Blazor.Trezor.WalletAccounts.Trezor;
using Nethereum.Wallet.UI.Components.AccountDetails;

namespace Nethereum.Wallet.UI.Components.Blazor.Trezor.Extensions;

public sealed class TrezorWalletUIRegistryContributor : IWalletUIRegistryContributor
{
    public void Configure(IServiceProvider serviceProvider)
    {
        var creationRegistry = serviceProvider.GetService<IAccountCreationRegistry>();
        creationRegistry?.Register<TrezorAccountCreationViewModel, TrezorAccountCreation>();
        creationRegistry?.Register<TrezorVaultAccountCreationViewModel, TrezorVaultAccountCreation>();

        var accountDetailsRegistry = serviceProvider.GetService<IAccountDetailsRegistry>();
        accountDetailsRegistry?.Register<TrezorAccountDetailsViewModel, TrezorAccountDetails>();

        var groupDetailsRegistry = serviceProvider.GetService<IGroupDetailsRegistry>();
        groupDetailsRegistry?.Register<TrezorGroupDetailsViewModel, TrezorGroupDetails>();
    }
}
