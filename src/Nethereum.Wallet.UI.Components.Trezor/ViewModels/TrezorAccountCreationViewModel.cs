#nullable enable

using System;
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

public partial class TrezorAccountCreationViewModel : AccountCreationViewModelBase
{
    [ObservableProperty] private string _deviceId = "trezor-default";
    [ObservableProperty] private uint _selectedIndex;
    [ObservableProperty] private ObservableCollection<TrezorDerivationPreview> _previews = new();
    [ObservableProperty] private string? _selectedAddress;
    [ObservableProperty] private string? _accountLabel;
    [ObservableProperty] private int _discoveryStartIndex;
    [ObservableProperty] private int _singleIndexInput;
    [ObservableProperty] private string _walletName = string.Empty;

    private readonly TrezorWalletAccountService _walletAccountService;
    private readonly ITrezorDeviceDiscoveryService _discoveryService;

    public TrezorAccountCreationViewModel(
        TrezorWalletAccountService walletAccountService,
        ITrezorDeviceDiscoveryService discoveryService,
        IWalletVaultService vaultService,
        NethereumWalletHostProvider walletHostProvider) : base(vaultService, walletHostProvider)
    {
        _walletAccountService = walletAccountService;
        _discoveryService = discoveryService;
    }

    public override string DisplayName => "Trezor Account";
    public override string Description => "Connect your Trezor device to add an account.";
    public override string Icon => "hardware";
    public override int SortOrder => 5;
    public override bool IsVisible => true;
    public override bool CanCreateAccount => !string.IsNullOrEmpty(SelectedAddress);

    public override IWalletAccount CreateAccount(WalletVault vault)
    {
        if (string.IsNullOrEmpty(SelectedAddress))
        {
            throw new InvalidOperationException("No address selected.");
        }

        var label = string.IsNullOrWhiteSpace(AccountLabel)
            ? $"Account {SelectedIndex}"
            : AccountLabel;

        return _walletAccountService.CreateFromKnownAddress(
            SelectedIndex,
            DeviceId,
            SelectedAddress,
            label,
            setAsSelected: true,
            addToVault: false,
            deviceLabel: WalletName);
    }

    public override void Reset()
    {
        SelectedAddress = null;
        AccountLabel = null;
        Previews.Clear();
        DiscoveryStartIndex = 0;
        SingleIndexInput = 0;
        WalletName = string.Empty;
        DeviceId = string.Empty;
    }

    public void PrepareForNewDevice()
    {
        if (string.IsNullOrEmpty(DeviceId))
        {
            DeviceId = $"trezor-{Guid.NewGuid():N}";
        }

        if (string.IsNullOrWhiteSpace(WalletName))
        {
            WalletName = _walletAccountService.GetDefaultHardwareDeviceLabel();
        }
    }

    [RelayCommand]
    public async Task DiscoverAsync(CancellationToken cancellationToken)
    {
        var startIndex = DiscoveryStartIndex < 0 ? 0u : (uint)DiscoveryStartIndex;
        var results = await _discoveryService.DiscoverAsync(DeviceId, startIndex, count: 5, cancellationToken);
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
        var targetIndex = SingleIndexInput < 0 ? 0u : (uint)SingleIndexInput;
        var results = await _discoveryService.DiscoverAsync(DeviceId, targetIndex, count: 1, cancellationToken);
        if (results.Count > 0)
        {
            var preview = results[0];
            Previews = new ObservableCollection<TrezorDerivationPreview>(results);
            SelectedIndex = preview.Index;
            SelectedAddress = preview.Address;
        }
    }
}
