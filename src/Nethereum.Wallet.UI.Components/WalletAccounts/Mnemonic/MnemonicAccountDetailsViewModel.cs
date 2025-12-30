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

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public partial class MnemonicAccountDetailsViewModel : ObservableObject, IAccountDetailsViewModel
    {
        private readonly IWalletVaultService _vaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IWalletDialogService _dialogService;
        private readonly IComponentLocalizer<MnemonicAccountDetailsViewModel> _localizer;

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

        public string AccountType => "mnemonic";

        public MnemonicAccountDetailsViewModel(
            IWalletVaultService vaultService,
            IWalletNotificationService notificationService,
            IWalletDialogService dialogService,
            IComponentLocalizer<MnemonicAccountDetailsViewModel> localizer)
        {
            _vaultService = vaultService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _localizer = localizer;
        }

        public bool CanHandle(IWalletAccount account)
        {
            return account is MnemonicWalletAccount;
        }

        public async Task InitializeAsync(IWalletAccount account)
        {
            if (!CanHandle(account))
                throw new ArgumentException($"Cannot handle account of type {account.GetType().Name}");

            try
            {
                IsLoading = true;
                ClearMessages();
                CloseRevealedPrivateKey();
                
                Account = account;
                EditingAccountName = account.Name;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to initialize: {ex.Message}";
                _notificationService.ShowError(ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }
        [RelayCommand]
        public async Task RemoveAccountAsync()
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
                ClearMessages();

                var vault = _vaultService.GetCurrentVault();
                if (vault?.Accounts != null)
                {
                    if (vault.Accounts.Count <= 1)
                    {
                        ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.CannotRemoveLastAccount);
                        return false;
                    }

                    // Show warning confirmation dialog
                    var accountName = Account.Name ?? $"Account {GetAccountIndex()}";
                    var confirmTitle = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.ConfirmRemoval);
                    var confirmMessage = string.Format(_localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.ConfirmRemovalMessage), accountName);
                    
                    var confirmed = await _dialogService.ShowWarningConfirmationAsync(
                        confirmTitle, 
                        confirmMessage, 
                        _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.RemoveAccount),
                        _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.Cancel));
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
                        
                        SuccessMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountRemoved);
                        
                        _notificationService.ShowSuccess(_localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountRemoved));
                        return true;
                    }
                    else
                    {
                        ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountNotFoundInVault);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.RemovalError), ex.Message);
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

        [RelayCommand]
        public async Task StartEditAccountNameAsync()
        {
            if (Account == null) return;

            EditingAccountName = Account.Name;
            IsEditingAccountName = true;
            ClearMessages();
        }
        [RelayCommand]
        public async Task SaveAccountNameAsync()
        {
            if (Account == null) return;

            try
            {
                IsLoading = true;
                ClearMessages();

                if (string.IsNullOrWhiteSpace(EditingAccountName))
                {
                    ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountNameRequired);
                    return;
                }

                Account.Label = EditingAccountName.Trim();
                await _vaultService.SaveAsync();

                SuccessMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountNameUpdated);
                _notificationService.ShowSuccess(SuccessMessage);

                IsEditingAccountName = false;
                EditingAccountName = "";
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.AccountNameUpdateError, ex.Message);
                _notificationService.ShowError(ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }
        [RelayCommand]
        public async Task CancelEditAccountNameAsync()
        {
            IsEditingAccountName = false;
            EditingAccountName = "";
            ClearMessages();
        }

        [RelayCommand]
        public async Task RevealPrivateKeyAsync(string password)
        {
            if (Account is not MnemonicWalletAccount mnemonicAccount) return;

            try
            {
                IsLoading = true;
                ClearMessages();

                var isValidPassword = await _vaultService.UnlockAsync(password);
                if (!isValidPassword)
                {
                    ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.InvalidPassword);
                    return;
                }

                var ethAccount = await mnemonicAccount.GetAccountAsync();
                if (ethAccount is Nethereum.Web3.Accounts.Account concreteAccount)
                {
                    RevealedPrivateKey = concreteAccount.PrivateKey;
                    ShowRevealedPrivateKey = true;
                }
                else
                {
                    ErrorMessage = _localizer.GetString(MnemonicAccountDetailsLocalizer.Keys.PrivateKeyRetrievalError);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error revealing private key: {ex.Message}";
                _notificationService.ShowError(ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }
        [RelayCommand]
        public void CloseRevealedPrivateKey()
        {
            ShowRevealedPrivateKey = false;
            RevealedPrivateKey = "";
        }

        public string GetDerivationPath()
        {
            if (Account is MnemonicWalletAccount mnemonicAccount)
            {
                return $"m/44'/60'/0'/0/{mnemonicAccount.Index}";
            }
            return "";
        }
        public int GetAccountIndex()
        {
            if (Account is MnemonicWalletAccount mnemonicAccount)
            {
                return mnemonicAccount.Index;
            }
            return 0;
        }
        public string GetMnemonicName()
        {
            if (Account is MnemonicWalletAccount mnemonicAccount)
            {
                var vault = _vaultService.GetCurrentVault();
                var mnemonic = vault?.FindMnemonicById(mnemonicAccount.MnemonicId);
                return mnemonic?.Label ?? "";
            }
            return "";
        }
        public string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address;

            return $"{address[..6]}...{address[^4..]}";
        }

    }
}