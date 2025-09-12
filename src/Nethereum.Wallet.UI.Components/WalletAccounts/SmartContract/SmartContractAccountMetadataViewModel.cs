using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.SmartContract
{
    public class SmartContractAccountMetadataViewModel : IAccountMetadataViewModel
    {
        private readonly IComponentLocalizer<SmartContractAccountCreationViewModel> _localizer;

        public SmartContractAccountMetadataViewModel(IComponentLocalizer<SmartContractAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string TypeName => SmartContractWalletAccount.TypeName;

        public string DisplayName => _localizer.GetString(SmartContractAccountEditorLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(SmartContractAccountEditorLocalizer.Keys.Description);

        public string Icon => "smart_toy";
        public string ColorTheme => "warning";
        public int SortOrder => 4;
        public bool IsVisible => true;
    }
}