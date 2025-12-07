using System;

namespace Nethereum.Wallet.UI.Components.WalletAccounts;

/// <summary>
/// Enables external component libraries to register additional wallet UI pieces
/// (account creation editors, details views, etc.) once the host initializes the UI.
/// </summary>
public interface IWalletUIRegistryContributor
{
    void Configure(IServiceProvider serviceProvider);
}
