#nullable enable

using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.Trezor;

public class TrezorAccountMetadataProvider : IAccountMetadataViewModel
{
    public string TypeName => "trezor";
    public string DisplayName => "Trezor Hardware Wallet";
    public string Description => "Create accounts backed by your Trezor device";
    public string Icon => "hardware";
    public string ColorTheme => "secondary";
    public int SortOrder => 5;
    public bool IsVisible => true;
}
