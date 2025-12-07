#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public partial class TrezorGroupDetailsViewModel : ObservableObject, IGroupDetailsViewModel
{
    private readonly IWalletVaultService _vaultService;
    private readonly TrezorWalletAccountService _accountService;
    private readonly IWalletNotificationService _notificationService;
    private readonly IComponentLocalizer<TrezorGroupDetailsViewModel> _localizer;

    [ObservableProperty] private string _deviceId = string.Empty;
    [ObservableProperty] private ObservableCollection<TrezorWalletAccount> _accounts = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAdding;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private uint _nextIndex;
    [ObservableProperty] private string _deviceLabel = string.Empty;
    [ObservableProperty] private string _editingDeviceLabel = string.Empty;
    [ObservableProperty] private bool _isEditingLabel;
    [ObservableProperty] private bool _isSavingLabel;

    public string GroupType => TrezorWalletAccount.TypeName;
    public string DisplayName =>
        string.IsNullOrWhiteSpace(DeviceLabel)
            ? _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DefaultDeviceLabel)
            : DeviceLabel;

    public TrezorGroupDetailsViewModel(
        IWalletVaultService vaultService,
        TrezorWalletAccountService accountService,
        IWalletNotificationService notificationService,
        IComponentLocalizer<TrezorGroupDetailsViewModel> localizer)
    {
        _vaultService = vaultService;
        _accountService = accountService;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    public bool CanHandle(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
    {
        return !string.IsNullOrWhiteSpace(groupId) &&
               groupAccounts.Any(a => a is TrezorWalletAccount);
    }

    public async Task InitializeAsync(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
    {
        DeviceId = groupId;
        if (groupAccounts?.Any() == true)
        {
            var trezorAccounts = groupAccounts
                .OfType<TrezorWalletAccount>()
                .OrderBy(a => a.Index)
                .ToList();
            Accounts = new ObservableCollection<TrezorWalletAccount>(trezorAccounts);
            NextIndex = trezorAccounts.Any() ? trezorAccounts.Max(a => a.Index) + 1 : 0;
        }
        else
        {
            await LoadAccountsAsync();
        }

        await LoadDeviceMetadataAsync();
    }

    [RelayCommand]
    public async Task AddNextAccountAsync()
    {
        if (string.IsNullOrEmpty(DeviceId))
        {
            return;
        }

        try
        {
            IsAdding = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            var label = $"{_localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DefaultAccountLabel)} {NextIndex}";
            await _accountService.CreateAsync(NextIndex, DeviceId, label, setAsSelected: false);
            await _vaultService.SaveAsync();

            SuccessMessage = _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.AccountAddedSuccess, NextIndex);
            _notificationService.ShowSuccess(SuccessMessage);
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizer.GetString(TrezorGroupDetailsLocalizer.Keys.AccountAddedFailed), ex.Message);
            _notificationService.ShowError(ErrorMessage);
        }
        finally
        {
            IsAdding = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAccountsAsync();
    }

    public void BeginEditDeviceLabel()
    {
        EditingDeviceLabel = DeviceLabel;
        IsEditingLabel = true;
    }

    public void CancelEditDeviceLabel()
    {
        EditingDeviceLabel = DeviceLabel;
        IsEditingLabel = false;
    }

    [RelayCommand]
    public async Task SaveDeviceLabelAsync()
    {
        if (string.IsNullOrEmpty(DeviceId))
        {
            return;
        }

        try
        {
            IsSavingLabel = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            var trimmed = string.IsNullOrWhiteSpace(EditingDeviceLabel)
                ? _accountService.GetDefaultHardwareDeviceLabel()
                : EditingDeviceLabel.Trim();

            _accountService.UpdateHardwareDeviceLabel(DeviceId, trimmed);
            await _vaultService.SaveAsync();

            DeviceLabel = trimmed;
            EditingDeviceLabel = trimmed;
            SuccessMessage = _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DeviceLabelSaved);
            _notificationService.ShowSuccess(SuccessMessage);
            IsEditingLabel = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(
                _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DeviceLabelSaveFailed),
                ex.Message);
            _notificationService.ShowError(ErrorMessage);
        }
        finally
        {
            IsSavingLabel = false;
        }
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            var vault = _vaultService.GetCurrentVault();
            if (vault == null)
            {
                return;
            }

            var trezorAccounts = vault.Accounts
                .OfType<TrezorWalletAccount>()
                .Where(a => string.Equals(a.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Index)
                .ToList();

            Accounts = new ObservableCollection<TrezorWalletAccount>(trezorAccounts);
            NextIndex = trezorAccounts.Any() ? trezorAccounts.Max(a => a.Index) + 1 : 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDeviceMetadataAsync()
    {
        var vault = _vaultService.GetCurrentVault();
        if (vault == null || string.IsNullOrEmpty(DeviceId))
        {
            DeviceLabel = _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DefaultDeviceLabel);
            EditingDeviceLabel = DeviceLabel;
            return;
        }

        var info = vault.FindHardwareDevice(DeviceId);
        if (info == null)
        {
            var fallback = _accountService.GetDefaultHardwareDeviceLabel();
            info = vault.AddOrUpdateHardwareDevice(DeviceId, TrezorWalletAccount.TypeName, fallback);
            await _vaultService.SaveAsync();
        }

        var label = string.IsNullOrWhiteSpace(info.Label)
            ? _accountService.GetDefaultHardwareDeviceLabel()
            : info.Label;

        DeviceLabel = label;
        EditingDeviceLabel = label;
    }
}
