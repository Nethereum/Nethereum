
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System;
using System.Threading.Tasks;
using static Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly.ViewOnlyAccountEditorLocalizer;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly
{
    public partial class ViewOnlyAccountCreationViewModel : ObservableObject, IAccountCreationViewModel
    {
        private readonly IComponentLocalizer<ViewOnlyAccountCreationViewModel> _localizer;
        
        public ViewOnlyAccountCreationViewModel(IComponentLocalizer<ViewOnlyAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string DisplayName => _localizer.GetString(Keys.DisplayName);
        public string Description => _localizer.GetString(Keys.Description);
        public string Icon => "visibility";
        public int SortOrder => 3;
        public bool IsVisible => true;

        [ObservableProperty] private string _viewOnlyAddress = string.Empty;
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        public bool CanCreateAccount => !string.IsNullOrWhiteSpace(ViewOnlyAddress) && 
                                       ViewOnlyAddress.StartsWith("0x") && 
                                       ViewOnlyAddress.Length == 42;

        public IWalletAccount CreateAccount(WalletVault vault)
        {
            if (!CanCreateAccount)
            {
                ErrorMessage = _localizer.GetString(Keys.InvalidEthereumAddressError);
                return null;
            }

            try
            {
                return new ViewOnlyWalletAccount(ViewOnlyAddress, Label ?? ViewOnlyAddress);
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizer.GetString(Keys.CreateAccountError, ex.Message);
                return null;
            }
        }

        public void Reset()
        {
            ViewOnlyAddress = string.Empty;
            Label = string.Empty;
            ErrorMessage = string.Empty;
        }
    }
}
