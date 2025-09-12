using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.Bip32;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public partial class VaultMnemonicAccountViewModel : ObservableObject, IAccountCreationViewModel
    {
        private readonly IComponentLocalizer<VaultMnemonicAccountViewModel> _localizer;
        private readonly IWalletVaultService _walletVaultService;

        public VaultMnemonicAccountViewModel(
            IComponentLocalizer<VaultMnemonicAccountViewModel> localizer,
            IWalletVaultService walletVaultService)
        {
            _localizer = localizer;
            _walletVaultService = walletVaultService;
        }

        public enum FormStep
        {
            SelectMnemonic = 0,
            Configure = 1,
            Confirm = 2
        }

        [ObservableProperty] private string _selectedMnemonicId = "";
        [ObservableProperty] private int _accountIndex = 1;
        [ObservableProperty] private string _accountName = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private FormStep _currentStep = FormStep.SelectMnemonic;
        [ObservableProperty] private List<MnemonicInfo> _availableMnemonics = new();
        [ObservableProperty] private string _derivedAddress = "";
        [ObservableProperty] private bool _hasDuplicateAccount = false;
        [ObservableProperty] private string _duplicateAccountMessage = "";
        [ObservableProperty] private bool _isLoading = false;

        public string DisplayName => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.Description);
        public string Icon => "account_tree";
        public int SortOrder => 0;

        public bool IsVisible
        {
            get
            {
                var vault = _walletVaultService.GetCurrentVault();
                return vault?.Mnemonics?.Any() == true;
            }
        }

        public bool CanCreateAccount => !string.IsNullOrEmpty(SelectedMnemonicId) && 
                                       AccountIndex >= 0 && 
                                       !string.IsNullOrEmpty(DerivedAddress) &&
                                       !HasDuplicateAccount;

        public bool CanContinue => CurrentStep switch
        {
            FormStep.SelectMnemonic => !string.IsNullOrEmpty(SelectedMnemonicId),
            FormStep.Configure => !string.IsNullOrEmpty(SelectedMnemonicId) && 
                                  AccountIndex >= 0 && 
                                  !string.IsNullOrEmpty(DerivedAddress),
            _ => false
        };

        partial void OnSelectedMnemonicIdChanged(string value)
        {
            // Use async void to avoid blocking and ensure UI thread execution
            _ = UpdateDerivedDataAsync();
        }

        partial void OnAccountIndexChanged(int value)
        {
            // Use async void to avoid blocking and ensure UI thread execution
            _ = UpdateDerivedDataAsync();
        }

        private async Task UpdateDerivedDataAsync()
        {
            await UpdateDerivedAddressAsync();
            await CheckForDuplicateAccountAsync();
            OnPropertyChanged(nameof(CanCreateAccount));
            OnPropertyChanged(nameof(CanContinue));
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            Reset();
            await LoadAvailableMnemonicsAsync();
        }

        [RelayCommand]
        public async Task LoadAvailableMnemonicsAsync()
        {
            IsLoading = true;
            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                AvailableMnemonics = vault?.Mnemonics?.ToList() ?? new List<MnemonicInfo>();
                
                if (AvailableMnemonics.Any())
                {
                    SelectedMnemonicId = AvailableMnemonics.First().Id;
                    await UpdateDerivedAddressAsync();
                    await CheckForDuplicateAccountAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ErrorLoadingMnemonics, ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task UpdateDerivedAddressAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedMnemonicId))
                {
                    DerivedAddress = "";
                    return;
                }

                var selectedMnemonic = AvailableMnemonics.FirstOrDefault(m => m.Id == SelectedMnemonicId);
                if (selectedMnemonic == null)
                {
                    DerivedAddress = "";
                    return;
                }

                var hdWallet = new MinimalHDWallet(selectedMnemonic.Mnemonic, selectedMnemonic.Passphrase);
                DerivedAddress = hdWallet.GetEthereumAddress(AccountIndex);
            }
            catch (Exception ex)
            {
                DerivedAddress = "";
                ErrorMessage = _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ErrorDerivingAddress, ex.Message);
            }
        }

        [RelayCommand]
        public async Task CheckForDuplicateAccountAsync()
        {
            HasDuplicateAccount = false;
            DuplicateAccountMessage = "";
            
            if (string.IsNullOrEmpty(DerivedAddress))
                return;
                
            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault?.Accounts != null)
                {
                    var existingAccount = vault.Accounts.FirstOrDefault(a => 
                        string.Equals(a.Address, DerivedAddress, StringComparison.OrdinalIgnoreCase));
                        
                    if (existingAccount != null)
                    {
                        HasDuplicateAccount = true;
                        DuplicateAccountMessage = $"An account with this address already exists: {existingAccount.Label ?? "Unnamed Account"}";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error checking for duplicate accounts: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task ContinueToNextStepAsync()
        {
            if (CanContinue && CurrentStep < FormStep.Confirm)
            {
                CurrentStep++;
                
                if (CurrentStep == FormStep.Configure || CurrentStep == FormStep.Confirm)
                {
                    await UpdateDerivedAddressAsync();
                    await CheckForDuplicateAccountAsync();
                }
                
                OnPropertyChanged(nameof(CanContinue));
                OnPropertyChanged(nameof(CanCreateAccount));
            }
        }

        [RelayCommand]
        public void GoToPreviousStep()
        {
            if (CurrentStep > FormStep.SelectMnemonic)
            {
                CurrentStep--;
                OnPropertyChanged(nameof(CanContinue));
                OnPropertyChanged(nameof(CanCreateAccount));
            }
        }

        public string GetSelectedMnemonicName()
        {
            var selectedMnemonic = AvailableMnemonics.FirstOrDefault(m => m.Id == SelectedMnemonicId);
            return selectedMnemonic?.Label ?? "";
        }

        public string GetMnemonicDisplayName(string mnemonicId)
        {
            var selectedMnemonic = AvailableMnemonics.FirstOrDefault(m => m.Id == mnemonicId);
            return selectedMnemonic?.Label ?? "";
        }

        public string GetStepTitle()
        {
            return CurrentStep switch
            {
                FormStep.SelectMnemonic => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.SelectMnemonicTitle),
                FormStep.Configure => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ConfigureAccountTitle),
                FormStep.Confirm => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ConfirmDetailsTitle),
                _ => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.AddAccount)
            };
        }

        public string GetStepSubtitle()
        {
            return CurrentStep switch
            {
                FormStep.SelectMnemonic => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.SelectMnemonicSubtitle),
                FormStep.Configure => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ConfigureAccountSubtitle),
                FormStep.Confirm => _localizer.GetString(VaultMnemonicAccountEditorLocalizer.Keys.ConfirmDetailsSubtitle),
                _ => ""
            };
        }

        public IWalletAccount CreateAccount(WalletVault vault)
        {
            if (!CanCreateAccount || vault == null)
                throw new InvalidOperationException("Cannot create account with current parameters");

            try
            {
                var selectedMnemonic = vault.Mnemonics?.FirstOrDefault(m => m.Id == SelectedMnemonicId);
                if (selectedMnemonic == null)
                    throw new InvalidOperationException("Selected mnemonic not found in vault");

                var accountName = !string.IsNullOrWhiteSpace(AccountName) 
                    ? AccountName 
                    : $"Account {AccountIndex + 1}";

                var hdWallet = new MinimalHDWallet(selectedMnemonic.Mnemonic, selectedMnemonic.Passphrase);
                var address = hdWallet.GetEthereumAddress(AccountIndex);

                return new MnemonicWalletAccount(
                    address,
                    accountName,
                    AccountIndex,
                    selectedMnemonic.Id,
                    hdWallet
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create account: {ex.Message}", ex);
            }
        }

        public void Reset()
        {
            SelectedMnemonicId = "";
            AccountIndex = 1;
            AccountName = "";
            ErrorMessage = "";
            CurrentStep = FormStep.SelectMnemonic;
            AvailableMnemonics.Clear();
            DerivedAddress = "";
            HasDuplicateAccount = false;
            DuplicateAccountMessage = "";
        }
    }
}