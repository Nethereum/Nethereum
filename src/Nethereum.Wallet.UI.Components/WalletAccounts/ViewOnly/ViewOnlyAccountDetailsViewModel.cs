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

namespace Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly
{
    public partial class ViewOnlyAccountDetailsViewModel : ObservableObject, IAccountDetailsViewModel
    {
        private readonly IWalletVaultService _vaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IWalletDialogService _dialogService;
        private readonly IComponentLocalizer<ViewOnlyAccountDetailsViewModel> _localizer;

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

        public string AccountType => "viewonly";
        public string DisplayName => "View-Only Account Details";

        public bool CanHandle(IWalletAccount account)
        {
            return account is ViewOnlyWalletAccount;
        }

        public ViewOnlyAccountDetailsViewModel(
            IWalletVaultService vaultService,
            IWalletNotificationService notificationService,
            IWalletDialogService dialogService,
            IComponentLocalizer<ViewOnlyAccountDetailsViewModel> localizer)
        {
            _vaultService = vaultService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _localizer = localizer;
        }

        public async Task InitializeAsync(IWalletAccount account)
        {
            if (!CanHandle(account))
                throw new ArgumentException($"Account type {account?.GetType().Name} is not supported by ViewOnlyAccountDetailsViewModel");

            Account = account;
            EditingAccountName = account?.Name ?? "";
            
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
                        SuccessMessage = _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.AccountNameUpdated);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.AccountNameUpdateError), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
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
                        ErrorMessage = _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.CannotRemoveLastAccount);
                        return false;
                    }

                    // Show warning confirmation dialog
                    var accountName = Account.Name ?? _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.ViewOnlyBadge);
                    var confirmTitle = _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.ConfirmRemoval);
                    var confirmMessage = string.Format(_localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.ConfirmRemovalMessage), accountName);
                    
                    var confirmed = await _dialogService.ShowWarningConfirmationAsync(
                        confirmTitle,
                        confirmMessage,
                        _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.RemoveAccount),
                        _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.Cancel));
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
                        
                        SuccessMessage = _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.AccountRemoved);

                        _notificationService.ShowSuccess(_localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.AccountRemoved));
                        return true;
                    }
                    else
                    {
                        ErrorMessage = _localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.AccountNotFoundInVault);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(ViewOnlyAccountDetailsLocalizer.Keys.RemovalError), ex.Message);
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