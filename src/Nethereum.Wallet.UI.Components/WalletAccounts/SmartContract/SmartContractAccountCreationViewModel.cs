#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System;
using System.Threading.Tasks;
using static Nethereum.Wallet.UI.Components.WalletAccounts.SmartContract.SmartContractAccountEditorLocalizer;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.SmartContract
{
    public partial class SmartContractAccountCreationViewModel : ObservableObject, IAccountCreationViewModel
    {
        private readonly IComponentLocalizer<SmartContractAccountCreationViewModel> _localizer;
        
        public SmartContractAccountCreationViewModel(IComponentLocalizer<SmartContractAccountCreationViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string DisplayName => _localizer.GetString(Keys.DisplayName);
        public string Description => _localizer.GetString(Keys.Description);
        public string Icon => "smart_toy";
        public int SortOrder => 4;
        public bool IsVisible => true;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        
        public bool CanCreateAccount => !string.IsNullOrWhiteSpace(Address) && 
                                       Address.StartsWith("0x") && 
                                       Address.Length == 42;

        public IWalletAccount CreateAccount(WalletVault vault)
        {
            if (!CanCreateAccount)
            {
                ErrorMessage = _localizer.GetString(Keys.InvalidContractAddressError);
                return null;
            }

            try
            {
                return new SmartContractWalletAccount(Address, Label ?? Address);
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizer.GetString(Keys.CreateAccountError, ex.Message);
                return null;
            }
        }
        
        public void Reset()
        {
            Address = string.Empty;
            Label = string.Empty;
            ErrorMessage = string.Empty;
        }
    }
}
