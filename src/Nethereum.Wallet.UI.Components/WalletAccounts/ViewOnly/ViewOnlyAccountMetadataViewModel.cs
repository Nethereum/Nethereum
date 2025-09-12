using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly
{
    public class ViewOnlyAccountMetadataViewModel : IAccountMetadataViewModel
    {
        private readonly IComponentLocalizer<ViewOnlyAccountCreationViewModel> _localizer;

        public ViewOnlyAccountMetadataViewModel(IComponentLocalizer<ViewOnlyAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string TypeName => ViewOnlyWalletAccount.TypeName;

        public string DisplayName => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.Description);

        public string Icon => "visibility";
        public string ColorTheme => "info";
        public int SortOrder => 3;
        public bool IsVisible => true;
    }
}