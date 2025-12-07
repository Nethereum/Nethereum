#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Trezor;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.AccountDetails;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Trezor.Localization;

namespace Nethereum.Wallet.UI.Components.Trezor.ViewModels;

public partial class TrezorAccountDetailsViewModel : ObservableObject, IAccountDetailsViewModel
{
    private readonly IWalletVaultService _vaultService;
    private readonly IWalletNotificationService _notificationService;
    private readonly IWalletDialogService _dialogService;
    private readonly IComponentLocalizer<TrezorAccountDetailsViewModel> _localizer;

    [ObservableProperty] private IWalletAccount? _account;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _isEditingAccountName;
    [ObservableProperty] private string _editingAccountName = string.Empty;

    public string AccountType => TrezorWalletAccount.TypeName;

    public TrezorAccountDetailsViewModel(
        IWalletVaultService vaultService,
        IWalletNotificationService notificationService,
        IWalletDialogService dialogService,
        IComponentLocalizer<TrezorAccountDetailsViewModel> localizer)
    {
        _vaultService = vaultService;
        _notificationService = notificationService;
        _dialogService = dialogService;
        _localizer = localizer;
    }

    public bool CanHandle(IWalletAccount account) => account is TrezorWalletAccount;

    public async Task InitializeAsync(IWalletAccount account)
    {
        if (!CanHandle(account))
        {
            throw new ArgumentException("Unsupported account type", nameof(account));
        }

        try
        {
            IsLoading = true;
            ClearMessages();

            Account = account;
            EditingAccountName = account.Name ?? account.Label ?? account.Address;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }

    [RelayCommand]
    public void StartEditAccountName()
    {
        if (Account == null)
        {
            return;
        }

        EditingAccountName = Account.Name ?? Account.Label ?? Account.Address;
        IsEditingAccountName = true;
        ClearMessages();
    }

    [RelayCommand]
    public async Task SaveAccountNameAsync()
    {
        if (Account == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditingAccountName))
        {
            ErrorMessage = _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.AccountNameRequired);
            return;
        }

        try
        {
            IsLoading = true;
            ClearMessages();

            Account.Label = EditingAccountName.Trim();
            await _vaultService.SaveAsync();
            SuccessMessage = _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.AccountNameUpdated);
            _notificationService.ShowSuccess(SuccessMessage);
            IsEditingAccountName = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizer.GetString(TrezorAccountDetailsLocalizer.Keys.AccountNameUpdateFailed), ex.Message);
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
        if (Account == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ClearMessages();

            var vault = _vaultService.GetCurrentVault();
            if (vault == null || vault.Accounts.Count <= 1)
            {
                ErrorMessage = _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.CannotRemoveLastAccount);
                return;
            }

            var confirmed = await _dialogService.ShowWarningConfirmationAsync(
                _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.ConfirmRemovalTitle),
                _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.ConfirmRemovalMessage),
                _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.RemoveAccountButton),
                _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.CancelButton));

            if (!confirmed)
            {
                return;
            }

            var accountToRemove = vault.Accounts.FirstOrDefault(a => a.Address == Account.Address);
            if (accountToRemove != null)
            {
                vault.Accounts.Remove(accountToRemove);
                await _vaultService.SaveAsync();
                SuccessMessage = _localizer.GetString(TrezorAccountDetailsLocalizer.Keys.AccountRemoved);
                _notificationService.ShowSuccess(SuccessMessage);
                Account = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizer.GetString(TrezorAccountDetailsLocalizer.Keys.RemoveAccountFailed), ex.Message);
            _notificationService.ShowError(ErrorMessage);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
