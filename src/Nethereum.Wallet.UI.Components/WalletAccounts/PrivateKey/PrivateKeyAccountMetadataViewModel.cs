using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey
{
    public class PrivateKeyAccountMetadataViewModel : IAccountMetadataViewModel
    {
        private readonly IComponentLocalizer<PrivateKeyAccountCreationViewModel> _localizer;

        public PrivateKeyAccountMetadataViewModel(IComponentLocalizer<PrivateKeyAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string TypeName => PrivateKeyWalletAccount.TypeName;

        public string DisplayName => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.Description);

        public string Icon => "vpn_key";
        public string ColorTheme => "secondary";
        public int SortOrder => 2;
        public bool IsVisible => true;
    }
}