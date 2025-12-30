using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.AccountDetails;
using Nethereum.Wallet.UI.Components.Abstractions;
using static Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic.MnemonicDetailsLocalizer;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public partial class MnemonicDetailsViewModel : ObservableObject, IGroupDetailsViewModel
    {
        private readonly IComponentLocalizer<MnemonicDetailsViewModel> _localizer;
        private readonly IWalletVaultService _walletVaultService;
        private readonly IWalletDialogService _dialogService;

        public MnemonicDetailsViewModel(
            IComponentLocalizer<MnemonicDetailsViewModel> localizer,
            IWalletVaultService walletVaultService,
            IWalletDialogService dialogService)
        {
            _localizer = localizer;
            _walletVaultService = walletVaultService;
            _dialogService = dialogService;
        }

        [ObservableProperty] private MnemonicInfo? _mnemonicInfo;
        [ObservableProperty] private List<IWalletAccount> _associatedAccounts = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _successMessage = "";
        [ObservableProperty] private string _editingMnemonicLabel = "";

        [ObservableProperty] private bool _showRevealedMnemonic = false;
        [ObservableProperty] private string _revealedMnemonic = "";

        public int AccountCount => AssociatedAccounts?.Count ?? 0;
        public bool HasAccounts => AccountCount > 0;
        public string DisplayLabel => MnemonicInfo?.Label ?? "Unnamed Mnemonic";
        public bool HasPassphrase => !string.IsNullOrEmpty(MnemonicInfo?.Passphrase);

        public async Task InitializeAsync(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
        {
            IsLoading = true;
            ErrorMessage = "";
            
            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault == null)
                {
                    ErrorMessage = _localizer.GetString(Keys.NoVaultAvailable);
                    return;
                }

                MnemonicInfo = vault.Mnemonics?.FirstOrDefault(m => m.Id == groupId);
                if (MnemonicInfo == null)
                {
                    ErrorMessage = _localizer.GetString(Keys.MnemonicNotFound);
                    return;
                }

                await LoadAssociatedAccountsAsync();
                
                EditingMnemonicLabel = MnemonicInfo.Label;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading mnemonic details: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadAssociatedAccountsAsync()
        {
            if (MnemonicInfo == null) return;

            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault?.Accounts != null)
                {
                    AssociatedAccounts = vault.Accounts
                        .Where(account => account is MnemonicWalletAccount mnemonicAccount && 
                                        mnemonicAccount.MnemonicId == MnemonicInfo.Id)
                        .ToList();
                }
                else
                {
                    AssociatedAccounts.Clear();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading associated accounts: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SaveMnemonicLabelAsync()
        {
            if (MnemonicInfo == null || string.IsNullOrWhiteSpace(EditingMnemonicLabel))
                return;

            IsLoading = true;
            ErrorMessage = "";
            SuccessMessage = "";

            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault == null)
                {
                    ErrorMessage = _localizer.GetString(Keys.NoVaultAvailable);
                    return;
                }

                MnemonicInfo.Label = EditingMnemonicLabel.Trim();

                await _walletVaultService.SaveAsync();

                SuccessMessage = _localizer.GetString(Keys.LabelUpdated);
                OnPropertyChanged(nameof(DisplayLabel));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating mnemonic label: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RevealMnemonicAsync(string password)
        {
            if (MnemonicInfo == null) return;

            IsLoading = true;
            ErrorMessage = "";

            try
            {
                // Validate password (this should be implemented based on your security requirements)
                var isValid = await ValidatePasswordAsync(password);
                if (!isValid)
                {
                    ErrorMessage = _localizer.GetString(Keys.InvalidPassword);
                    return;
                }

                RevealedMnemonic = MnemonicInfo.Mnemonic;
                ShowRevealedMnemonic = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error revealing mnemonic: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void HideMnemonic()
        {
            ShowRevealedMnemonic = false;
            RevealedMnemonic = "";
        }
        public async Task<bool> DeleteMnemonicInternalAsync()
        {
            if (MnemonicInfo == null) return false;

           
            var confirmationTitle = _localizer.GetString(Keys.DeleteWarning);
            var confirmationMessage = HasAccounts 
                ? string.Format(_localizer.GetString(Keys.DeleteWarningWithAccountsMessage), AccountCount)
                : _localizer.GetString(Keys.DeleteWarningMessage);

            var confirmed = await _dialogService.ShowWarningConfirmationAsync(
                confirmationTitle, 
                confirmationMessage, 
                _localizer.GetString(Keys.Delete),
                _localizer.GetString(Keys.Cancel));
            if (!confirmed) return false;

            return await DeleteMnemonicAsync();
        }

        [RelayCommand]
        public async Task<bool> DeleteMnemonicAsync()
        {
            if (MnemonicInfo == null) return false;

            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault == null)
                {
                    ErrorMessage = _localizer.GetString(Keys.NoVaultAvailable);
                    return false;
                }

                if (HasAccounts && vault.Accounts != null)
                {
                    var accountsToRemove = vault.Accounts.Where(account => 
                        account is MnemonicWalletAccount mnemonicAccount && 
                        mnemonicAccount.MnemonicId == MnemonicInfo.Id).ToList();
                    
                    foreach (var account in accountsToRemove)
                    {
                        vault.Accounts.Remove(account);
                    }
                }

                if (vault.Mnemonics != null)
                {
                    vault.Mnemonics.Remove(MnemonicInfo);
                    await _walletVaultService.SaveAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting mnemonic: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public string GetAccountDerivationPath(IWalletAccount account)
        {
            if (account is MnemonicWalletAccount mnemonicAccount)
            {
                return $"m/44'/60'/0'/0/{mnemonicAccount.Index}";
            }
            return "Unknown";
        }

        public string GetAccountTypeDescription()
        {
            return "HD Wallet (BIP-44)";
        }

        private async Task<bool> ValidatePasswordAsync(string password)
        { 
            return !string.IsNullOrEmpty(password);
        }

        public void Reset()
        {
            MnemonicInfo = null;
            AssociatedAccounts.Clear();
            ErrorMessage = "";
            SuccessMessage = "";
            EditingMnemonicLabel = "";
            ShowRevealedMnemonic = false;
            RevealedMnemonic = "";
            IsLoading = false;
        }

        public string GroupType => MnemonicWalletAccount.TypeName;
        public string DisplayName => "Seed Phrase";

        public bool CanHandle(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
        {
            return groupAccounts?.All(account => 
                account is MnemonicWalletAccount mnemonicAccount && 
                mnemonicAccount.MnemonicId == groupId) == true;
        }
    }
}
