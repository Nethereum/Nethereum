using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicAccountMetadataViewModel : IAccountMetadataViewModel
    {
        private readonly IComponentLocalizer<MnemonicAccountCreationViewModel> _localizer;

        public MnemonicAccountMetadataViewModel(IComponentLocalizer<MnemonicAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string TypeName => MnemonicWalletAccount.TypeName;

        public string DisplayName => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.Description);

        public string Icon => "account_tree";
        public string ColorTheme => "primary";
        public int SortOrder => 1;
        public bool IsVisible => true;
    }
}