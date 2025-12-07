#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Trezor;
using Nethereum.Wallet.UI.Components.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.Trezor.ViewModels;

public partial class TrezorVaultAccountCreationViewModel : AccountCreationViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDevices)), NotifyPropertyChangedFor(nameof(SelectedDeviceSummary))]
    private ObservableCollection<TrezorDeviceSummary> _devices = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedDeviceSummary))]
    private string? _selectedDeviceId;

    [ObservableProperty] private ObservableCollection<TrezorDerivationPreview> _previews = new();
    [ObservableProperty] private uint _selectedIndex;
    [ObservableProperty] private string? _selectedAddress;
    [ObservableProperty] private string? _accountLabel;
    [ObservableProperty] private int _discoveryStartIndex;
    [ObservableProperty] private int _singleIndexInput;
    [ObservableProperty] private bool _isLoadingDevices;
    [ObservableProperty] private string? _loadError;

    private readonly TrezorWalletAccountService _walletAccountService;
    private readonly ITrezorDeviceDiscoveryService _discoveryService;

    public TrezorVaultAccountCreationViewModel(
        TrezorWalletAccountService walletAccountService,
        ITrezorDeviceDiscoveryService discoveryService,
        IWalletVaultService vaultService,
        NethereumWalletHostProvider walletHostProvider) : base(vaultService, walletHostProvider)
    {
        _walletAccountService = walletAccountService;
        _discoveryService = discoveryService;
    }

    public bool HasDevices => Devices.Count > 0;
    public TrezorDeviceSummary? SelectedDeviceSummary => Devices.FirstOrDefault(d => string.Equals(d.DeviceId, SelectedDeviceId, StringComparison.OrdinalIgnoreCase));

    public override string DisplayName => "Add Existing Trezor Account";
    public override string Description => "Derive a new account from a Trezor device already stored in your vault.";
    public override string Icon => "hardware";
    public override int SortOrder => 6;
    public override bool IsVisible
    {
        get
        {
            var vault = _vaultService.GetCurrentVault();
            return vault?.Accounts?.OfType<TrezorWalletAccount>().Any() == true;
        }
    }

    public override bool CanCreateAccount => !string.IsNullOrEmpty(SelectedDeviceId) && !string.IsNullOrEmpty(SelectedAddress);

    public async Task LoadDevicesAsync()
    {
        try
        {
            IsLoadingDevices = true;
            LoadError = null;

            var vault = _vaultService.GetCurrentVault();
            var summaries = vault?.Accounts?
                .OfType<TrezorWalletAccount>()
                .GroupBy(a => a.DeviceId)
                .Select(g =>
                {
                    var nextIndex = g.Any() ? g.Max(x => x.Index) + 1 : 0;
                    var deviceLabel = vault?.FindHardwareDevice(g.Key)?.Label;
                    return new TrezorDeviceSummary(g.Key, deviceLabel ?? g.Key, g.Count(), nextIndex);
                })
                .OrderBy(s => s.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<TrezorDeviceSummary>();

            Devices = new ObservableCollection<TrezorDeviceSummary>(summaries);

            if (!string.IsNullOrEmpty(SelectedDeviceId) && Devices.All(d => !string.Equals(d.DeviceId, SelectedDeviceId, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedDeviceId = null;
            }

            if (string.IsNullOrEmpty(SelectedDeviceId) && Devices.Any())
            {
                SelectedDeviceId = Devices.First().DeviceId;
            }

            UpdateSuggestedIndices();
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
        finally
        {
            IsLoadingDevices = false;
        }
    }

    [RelayCommand]
    public async Task DiscoverAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SelectedDeviceId))
        {
            return;
        }

        var startIndex = DiscoveryStartIndex < 0 ? 0u : (uint)DiscoveryStartIndex;
        var results = await _discoveryService.DiscoverAsync(SelectedDeviceId, startIndex, count: 5, cancellationToken);
        Previews = new ObservableCollection<TrezorDerivationPreview>(results);
        var first = results.FirstOrDefault();
        if (first != null)
        {
            SelectedIndex = first.Index;
            SelectedAddress = first.Address;
        }
    }

    [RelayCommand]
    public async Task LoadSingleIndexAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SelectedDeviceId))
        {
            return;
        }

        var targetIndex = SingleIndexInput < 0 ? 0u : (uint)SingleIndexInput;
        var results = await _discoveryService.DiscoverAsync(SelectedDeviceId, targetIndex, count: 1, cancellationToken);
        if (results.Count > 0)
        {
            var preview = results[0];
            Previews = new ObservableCollection<TrezorDerivationPreview>(results);
            SelectedIndex = preview.Index;
            SelectedAddress = preview.Address;
        }
    }

    public string GetDeviceDisplayName(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return string.Empty;
        }

        var summary = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (summary == null)
        {
            return deviceId;
        }

        return summary.AccountCount > 0
            ? $"{summary.Label} ({summary.AccountCount} accounts)"
            : summary.Label;
    }

    public override IWalletAccount CreateAccount(WalletVault vault)
    {
        if (string.IsNullOrEmpty(SelectedDeviceId) || string.IsNullOrEmpty(SelectedAddress))
        {
            throw new InvalidOperationException("Selected device or address is missing.");
        }

        var label = string.IsNullOrWhiteSpace(AccountLabel)
            ? $"Account {SelectedIndex}"
            : AccountLabel;

        return _walletAccountService.CreateFromKnownAddress(
            SelectedIndex,
            SelectedDeviceId,
            SelectedAddress,
            label,
            setAsSelected: true,
            addToVault: false);
    }

    public override void Reset()
    {
        SelectedAddress = null;
        AccountLabel = null;
        Previews.Clear();
        DiscoveryStartIndex = 0;
        SingleIndexInput = 0;
    }

    partial void OnSelectedDeviceIdChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var suggested = (int)GetSuggestedStartIndex(value);
        DiscoveryStartIndex = suggested;
        SingleIndexInput = suggested;
        Previews.Clear();
        SelectedAddress = null;
    }

    private void UpdateSuggestedIndices()
    {
        if (!string.IsNullOrEmpty(SelectedDeviceId))
        {
            var suggested = (int)GetSuggestedStartIndex(SelectedDeviceId);
            DiscoveryStartIndex = suggested;
            if (SingleIndexInput == 0 || SingleIndexInput < suggested)
            {
                SingleIndexInput = suggested;
            }
        }
    }

    private uint GetSuggestedStartIndex(string deviceId)
    {
        var summary = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        return summary?.NextIndex ?? 0;
    }

    public sealed record TrezorDeviceSummary(string DeviceId, string Label, int AccountCount, uint NextIndex);
}
