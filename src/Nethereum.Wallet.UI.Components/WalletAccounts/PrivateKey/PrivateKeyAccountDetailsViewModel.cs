using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.AccountDetails;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey
{
    public partial class PrivateKeyAccountDetailsViewModel : ObservableObject, IAccountDetailsViewModel
    {
        private readonly IWalletVaultService _vaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IWalletDialogService _dialogService;
        private readonly IComponentLocalizer<PrivateKeyAccountDetailsViewModel> _localizer;

        [ObservableProperty]
        private IWalletAccount? _account;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private string _successMessage = "";

        [ObservableProperty]
        private bool _isEditingAccountName = false;

        [ObservableProperty]
        private string _editingAccountName = "";

        [ObservableProperty]
        private bool _showRevealedPrivateKey = false;

        [ObservableProperty]
        private string _revealedPrivateKey = "";

        public string AccountType => "private-key";
        public string DisplayName => "Private Key Account Details";

        public bool CanHandle(IWalletAccount account)
        {
            return account is PrivateKeyWalletAccount;
        }

        public PrivateKeyAccountDetailsViewModel(
            IWalletVaultService vaultService,
            IWalletNotificationService notificationService,
            IWalletDialogService dialogService,
            IComponentLocalizer<PrivateKeyAccountDetailsViewModel> localizer)
        {
            _vaultService = vaultService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _localizer = localizer;
        }

        public async Task InitializeAsync(IWalletAccount account)
        {
            if (!CanHandle(account))
                throw new ArgumentException($"Account type {account?.GetType().Name} is not supported by PrivateKeyAccountDetailsViewModel");

            Account = account;
            EditingAccountName = account?.Name ?? "";
            
            ShowRevealedPrivateKey = false;
            RevealedPrivateKey = "";
            ErrorMessage = "";
            SuccessMessage = "";
            
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAccountName()
        {
            if (Account == null || string.IsNullOrWhiteSpace(EditingAccountName))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = "";
                
                var vault = _vaultService.GetCurrentVault();
                if (vault?.Accounts != null)
                {
                    var existingAccount = vault.Accounts.FirstOrDefault(a => a.Address == Account.Address);
                    if (existingAccount != null)
                    {
                        existingAccount.Label = EditingAccountName.Trim();
                        await _vaultService.SaveAsync();
                        
                        IsEditingAccountName = false;
                        SuccessMessage = _localizer.GetString("AccountNameUpdated");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to update account name: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RevealPrivateKey(string password)
        {
            if (Account == null || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = "";

                var isValidPassword = await _vaultService.UnlockAsync(password);
                if (isValidPassword)
                {
                    if (Account is PrivateKeyWalletAccount privateKeyAccount)
                    {
                        RevealedPrivateKey = privateKeyAccount.PrivateKey;
                        ShowRevealedPrivateKey = true;
                    }
                    else
                    {
                        ErrorMessage = "Unable to retrieve private key for this account type.";
                    }
                }
                else
                {
                    ErrorMessage = "Invalid password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to reveal private key: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void CloseRevealedPrivateKey()
        {
            ShowRevealedPrivateKey = false;
            RevealedPrivateKey = "";
        }

        [RelayCommand]
        private async Task RemoveAccount()
        {
            await RemoveAccountInternalAsync();
        }
        public async Task<bool> RemoveAccountInternalAsync()
        {
            if (Account == null)
                return false;

            try
            {
                IsLoading = true;
                ErrorMessage = "";

                var vault = _vaultService.GetCurrentVault();
                if (vault?.Accounts != null)
                {
                    if (vault.Accounts.Count <= 1)
                    {
                        ErrorMessage = _localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.CannotRemoveLastAccount);
                        return false;
                    }

                    // Show warning confirmation dialog
                    var accountName = Account.Name ?? "Private Key Account";
                    var confirmTitle = _localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.ConfirmRemoval);
                    var confirmMessage = string.Format(_localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.ConfirmRemovalMessage), accountName);
                    
                    var confirmed = await _dialogService.ShowWarningConfirmationAsync(
                        confirmTitle, 
                        confirmMessage, 
                        _localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.RemoveAccount),
                        "Cancel");
                    if (!confirmed)
                    {
                        IsLoading = false;
                        return false;
                    }

                    var accountToRemove = vault.Accounts.FirstOrDefault(a => a.Address == Account.Address);
                    if (accountToRemove != null)
                    {
                        vault.Accounts.Remove(accountToRemove);
                        await _vaultService.SaveAsync();
                        
                        SuccessMessage = _localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.AccountRemoved);
                        
                        _notificationService.ShowSuccess(_localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.AccountRemoved));
                        return true;
                    }
                    else
                    {
                        ErrorMessage = "Account not found in vault.";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(PrivateKeyAccountDetailsLocalizer.Keys.RemovalError), ex.Message);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
            
            return false;
        }

        public void ClearMessages()
        {
            ErrorMessage = "";
            SuccessMessage = "";
        }
    }
}