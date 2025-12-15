# Nethereum.Wallet.UI.Components.Trezor

Platform-agnostic ViewModels and business logic for integrating Trezor hardware wallets into Nethereum wallet UIs. Provides MVVM-based components that can be consumed by any UI framework (Blazor, Avalonia, MAUI, etc.).

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components.Trezor
```

## Target Framework

- net9.0

## Dependencies

### Nethereum Packages
- Nethereum.Wallet.Trezor - Trezor device communication and account management
- Nethereum.Wallet.UI.Components - Base UI components and MVVM framework

Source: Nethereum.Wallet.UI.Components.Trezor.csproj:15-18

## Overview

This package implements Trezor hardware wallet support using the registry pattern from `Nethereum.Wallet.UI.Components`. It provides ViewModels for:

1. **New Device Setup** - Connect and add accounts from a new Trezor device
2. **Vault Account Creation** - Derive additional accounts from existing Trezor devices
3. **Account Management** - View and edit Trezor account details
4. **Device Management** - Manage multiple accounts from the same Trezor device

## Account Type Metadata

**TrezorAccountMetadataProvider** - Registers Trezor in the account type system
```csharp
public class TrezorAccountMetadataProvider : IAccountMetadataViewModel
{
    public string TypeName => "trezor";
    public string DisplayName => "Trezor Hardware Wallet";
    public string Description => "Create accounts backed by your Trezor device";
    public string Icon => "hardware";
    public string ColorTheme => "secondary";
    public int SortOrder => 5;
    public bool IsVisible => true;
}
```
Source: TrezorAccountMetadataProvider.cs:8-17

## ViewModels

### 1. TrezorAccountCreationViewModel

Create accounts from a new Trezor device.

**Properties:**
```csharp
[ObservableProperty] private string _deviceId = "trezor-default";
[ObservableProperty] private uint _selectedIndex;
[ObservableProperty] private ObservableCollection<TrezorDerivationPreview> _previews = new();
[ObservableProperty] private string? _selectedAddress;
[ObservableProperty] private string? _accountLabel;
[ObservableProperty] private int _discoveryStartIndex;
[ObservableProperty] private int _singleIndexInput;
[ObservableProperty] private string _walletName = string.Empty;
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:21-28

**Dependencies:**
```csharp
private readonly TrezorWalletAccountService _walletAccountService;
private readonly ITrezorDeviceDiscoveryService _discoveryService;
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:30-31

**Account Creation Display:**
```csharp
public override string DisplayName => "Trezor Account";
public override string Description => "Connect your Trezor device to add an account.";
public override string Icon => "hardware";
public override int SortOrder => 5;
public override bool IsVisible => true;
public override bool CanCreateAccount => !string.IsNullOrEmpty(SelectedAddress);
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:43-48

**Commands:**

**DiscoverAsync** - Scan multiple addresses starting from an index
```csharp
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
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:95-107

Discovers 5 addresses starting from `DiscoveryStartIndex`.
Source: ViewModels/TrezorAccountCreationViewModel.cs:99

**LoadSingleIndexAsync** - Load a specific derivation index
```csharp
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
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:109-122

**Account Creation:**
```csharp
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
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:50-69

**Device Preparation:**
```csharp
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
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:82-93

Generates unique device ID and default wallet name.
Source: ViewModels/TrezorAccountCreationViewModel.cs:86,91

**Reset:**
```csharp
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
```
Source: ViewModels/TrezorAccountCreationViewModel.cs:71-80

### 2. TrezorVaultAccountCreationViewModel

Derive additional accounts from Trezor devices already stored in the vault.

**Properties:**
```csharp
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
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:21-34

**Device Summary Record:**
```csharp
public sealed record TrezorDeviceSummary(string DeviceId, string Label, int AccountCount, uint NextIndex);
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:229

**Computed Properties:**
```csharp
public bool HasDevices => Devices.Count > 0;
public TrezorDeviceSummary? SelectedDeviceSummary =>
    Devices.FirstOrDefault(d => string.Equals(d.DeviceId, SelectedDeviceId, StringComparison.OrdinalIgnoreCase));
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:49-50

**Visibility:**
```csharp
public override bool IsVisible
{
    get
    {
        var vault = _vaultService.GetCurrentVault();
        return vault?.Accounts?.OfType<TrezorWalletAccount>().Any() == true;
    }
}
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:56-63

Only visible if vault contains at least one Trezor account.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:61

**Load Devices from Vault:**
```csharp
public async Task LoadDevicesAsync()
{
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

    // Auto-select first device if none selected
    if (string.IsNullOrEmpty(SelectedDeviceId) && Devices.Any())
    {
        SelectedDeviceId = Devices.First().DeviceId;
    }

    UpdateSuggestedIndices();
}
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:67-109

Groups accounts by DeviceId and calculates next available index for each device.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:80

**Suggested Index Calculation:**
```csharp
private uint GetSuggestedStartIndex(string deviceId)
{
    var summary = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    return summary?.NextIndex ?? 0;
}
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:223-227

**Device Selection Handler:**
```csharp
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
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:196-208

Automatically updates suggested indices when device is selected.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:203-205

**Device Display Name:**
```csharp
public string GetDeviceDisplayName(string? deviceId)
{
    var summary = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (summary == null)
    {
        return deviceId;
    }

    return summary.AccountCount > 0
        ? $"{summary.Label} ({summary.AccountCount} accounts)"
        : summary.Label;
}
```
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:149-165

**DiscoverAsync and LoadSingleIndexAsync** - Same as TrezorAccountCreationViewModel but requires `SelectedDeviceId` to be set.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:111-147

### 3. TrezorAccountDetailsViewModel

View and manage individual Trezor account details.

**Implements:** `IAccountDetailsViewModel`

**Properties:**
```csharp
[ObservableProperty] private IWalletAccount? _account;
[ObservableProperty] private bool _isLoading;
[ObservableProperty] private string _errorMessage = string.Empty;
[ObservableProperty] private string _successMessage = string.Empty;
[ObservableProperty] private bool _isEditingAccountName;
[ObservableProperty] private string _editingAccountName = string.Empty;
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:25-30

**Account Type:**
```csharp
public string AccountType => TrezorWalletAccount.TypeName;
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:32

**Account Handling:**
```csharp
public bool CanHandle(IWalletAccount account) => account is TrezorWalletAccount;
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:46

**Initialization:**
```csharp
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
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:48-67

**Commands:**

**StartEditAccountName** - Begin editing account name
```csharp
[RelayCommand]
public void StartEditAccountName()
{
    EditingAccountName = Account.Name ?? Account.Label ?? Account.Address;
    IsEditingAccountName = true;
    ClearMessages();
}
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:75-86

**SaveAccountNameAsync** - Save account name changes
```csharp
[RelayCommand]
public async Task SaveAccountNameAsync()
{
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
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:88-122

**RemoveAccountAsync** - Delete account with validation
```csharp
[RelayCommand]
public async Task RemoveAccountAsync()
{
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
```
Source: ViewModels/TrezorAccountDetailsViewModel.cs:124-174

Prevents deletion of last account in vault.
Source: ViewModels/TrezorAccountDetailsViewModel.cs:138-141

### 4. TrezorGroupDetailsViewModel

Manage all accounts from a single Trezor device.

**Implements:** `IGroupDetailsViewModel`

**Properties:**
```csharp
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
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:26-36

**Group Type:**
```csharp
public string GroupType => TrezorWalletAccount.TypeName;
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:38

**Display Name:**
```csharp
public string DisplayName =>
    string.IsNullOrWhiteSpace(DeviceLabel)
        ? _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DefaultDeviceLabel)
        : DeviceLabel;
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:39-42

**Group Handling:**
```csharp
public bool CanHandle(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
{
    return !string.IsNullOrWhiteSpace(groupId) &&
           groupAccounts.Any(a => a is TrezorWalletAccount);
}
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:56-60

**Initialization:**
```csharp
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
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:62-80

Calculates next available derivation index.
Source: ViewModels/TrezorGroupDetailsViewModel.cs:72

**Commands:**

**AddNextAccountAsync** - Create next account on device
```csharp
[RelayCommand]
public async Task AddNextAccountAsync()
{
    var label = $"{_localizer.GetString(TrezorGroupDetailsLocalizer.Keys.DefaultAccountLabel)} {NextIndex}";
    await _accountService.CreateAsync(NextIndex, DeviceId, label, setAsSelected: false);
    await _vaultService.SaveAsync();

    SuccessMessage = _localizer.GetString(TrezorGroupDetailsLocalizer.Keys.AccountAddedSuccess, NextIndex);
    _notificationService.ShowSuccess(SuccessMessage);
    await LoadAccountsAsync();
}
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:82-113

Creates account with default label "Account {Index}".
Source: ViewModels/TrezorGroupDetailsViewModel.cs:96

**SaveDeviceLabelAsync** - Update device label
```csharp
[RelayCommand]
public async Task SaveDeviceLabelAsync()
{
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
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:133-171

Falls back to default label if empty.
Source: ViewModels/TrezorGroupDetailsViewModel.cs:147-149

**Load Accounts:**
```csharp
private async Task LoadAccountsAsync()
{
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
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:173-200

Filters accounts by case-insensitive DeviceId comparison.
Source: ViewModels/TrezorGroupDetailsViewModel.cs:189

**Load Device Metadata:**
```csharp
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
```
Source: ViewModels/TrezorGroupDetailsViewModel.cs:202-227

Creates default device metadata if not found.
Source: ViewModels/TrezorGroupDetailsViewModel.cs:214-217

## Localization

All ViewModels support full localization for English (en-US) and Spanish (es-ES).

**Localizer Classes:**
- TrezorAccountCreationLocalizer - 40+ localization keys
- TrezorVaultAccountCreationLocalizer - 38+ localization keys
- TrezorAccountDetailsLocalizer - 30+ localization keys
- TrezorGroupDetailsLocalizer - 34+ localization keys

### Example Localization Keys

**TrezorAccountCreationLocalizer:**
```csharp
public static class Keys
{
    public const string DisplayName = "DisplayName";
    public const string Description = "Description";
    public const string WalletNameLabel = "WalletNameLabel";
    public const string ConnectInstruction = "ConnectInstruction";
    public const string ScanButtonText = "ScanButtonText";
    public const string StepConnectLabel = "StepConnectLabel";
    public const string StepSelectLabel = "StepSelectLabel";
    public const string StepConfirmLabel = "StepConfirmLabel";
    // ... additional keys
}
```
Source: Localization/TrezorAccountCreationLocalizer.cs:9-41

**English Translations:**
```csharp
_globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
{
    [Keys.DisplayName] = "Trezor Hardware Wallet",
    [Keys.Description] = "Connect your Trezor device and select the account you want to use.",
    [Keys.WalletNameLabel] = "Wallet name",
    [Keys.ConnectInstruction] = "Plug in your Trezor, unlock it, and scan for addresses.",
    [Keys.ScanButtonText] = "Scan Addresses",
    [Keys.StepConnectLabel] = "Connect",
    [Keys.StepSelectLabel] = "Select",
    [Keys.StepConfirmLabel] = "Confirm",
    // ...
});
```
Source: Localization/TrezorAccountCreationLocalizer.cs:49-81

**Spanish Translations:**
```csharp
_globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
{
    [Keys.DisplayName] = "Cartera Trezor",
    [Keys.Description] = "Conecta tu dispositivo Trezor y selecciona la cuenta que deseas usar.",
    [Keys.WalletNameLabel] = "Nombre de la cartera",
    [Keys.ConnectInstruction] = "Conecta tu Trezor, desbloquéalo y pulsa Escanear para ver las direcciones.",
    [Keys.ScanButtonText] = "Escanear direcciones",
    [Keys.StepConnectLabel] = "Conectar",
    [Keys.StepSelectLabel] = "Seleccionar",
    [Keys.StepConfirmLabel] = "Confirmar",
    // ...
});
```
Source: Localization/TrezorAccountCreationLocalizer.cs:83-115

## Integration with Wallet UI

### Registry Contributor Pattern

To integrate Trezor support, platform-specific packages (Blazor, Avalonia, MAUI) implement `IWalletUIRegistryContributor`:

```csharp
public void Configure(IServiceProvider serviceProvider)
{
    var accountCreationRegistry = serviceProvider.GetRequiredService<IAccountCreationRegistry>();
    var accountDetailsRegistry = serviceProvider.GetRequiredService<IAccountDetailsRegistry>();
    var groupDetailsRegistry = serviceProvider.GetRequiredService<IGroupDetailsRegistry>();
    var metadataRegistry = serviceProvider.GetRequiredService<IAccountTypeMetadataRegistry>();

    // Register ViewModels and their UI components
    accountCreationRegistry.Register<TrezorAccountCreationViewModel, TrezorAccountCreationComponent>();
    accountCreationRegistry.Register<TrezorVaultAccountCreationViewModel, TrezorVaultAccountCreationComponent>();
    accountDetailsRegistry.Register<TrezorAccountDetailsViewModel, TrezorAccountDetailsComponent>();
    groupDetailsRegistry.Register<TrezorGroupDetailsViewModel, TrezorGroupDetailsComponent>();
}
```

This pattern allows platform-specific UI packages to provide their own UI components while reusing these ViewModels.

### Service Registration

Platform-specific packages should register:

**ViewModels:**
```csharp
services.AddTransient<TrezorAccountCreationViewModel>();
services.AddTransient<TrezorVaultAccountCreationViewModel>();
services.AddTransient<TrezorAccountDetailsViewModel>();
services.AddTransient<TrezorGroupDetailsViewModel>();
```

**Metadata:**
```csharp
services.AddSingleton<IAccountMetadataViewModel, TrezorAccountMetadataProvider>();
```

**Localizers:**
```csharp
services.AddSingleton<IComponentLocalizer<TrezorAccountCreationViewModel>, TrezorAccountCreationLocalizer>();
services.AddSingleton<IComponentLocalizer<TrezorVaultAccountCreationViewModel>, TrezorVaultAccountCreationLocalizer>();
services.AddSingleton<IComponentLocalizer<TrezorAccountDetailsViewModel>, TrezorAccountDetailsLocalizer>();
services.AddSingleton<IComponentLocalizer<TrezorGroupDetailsViewModel>, TrezorGroupDetailsLocalizer>();
```

**Dependencies:**
```csharp
services.AddScoped<TrezorWalletAccountService>();
services.AddScoped<ITrezorDeviceDiscoveryService, TrezorDeviceDiscoveryService>();
```

## Workflow Examples

### New Trezor Device Workflow

1. User selects "Trezor Hardware Wallet" from account creation options
2. `TrezorAccountCreationViewModel` is instantiated
3. User enters wallet name and connects device
4. Calls `PrepareForNewDevice()` to generate unique device ID
5. Calls `DiscoverAsync()` to scan addresses (default: 5 addresses from index 0)
6. User selects an address from previews
7. Optionally provides account label
8. Calls `CreateAccount()` which uses `TrezorWalletAccountService.CreateFromKnownAddress()`
9. Account is added to vault

### Existing Device Workflow

1. User navigates to existing Trezor device group
2. `TrezorGroupDetailsViewModel` displays all accounts for that device
3. User clicks "Add next address"
4. Calls `AddNextAccountAsync()` which derives account at `NextIndex`
5. New account is added and list refreshes

### Vault Account Creation Workflow

1. User selects "Add Account from Trezor" (only visible if Trezor accounts exist)
2. `TrezorVaultAccountCreationViewModel` loads available devices via `LoadDevicesAsync()`
3. Displays devices with account counts: "My Trezor (3 accounts)"
4. User selects device
5. System suggests next available index automatically
6. User can scan multiple addresses or load specific index
7. Selects address and creates account

## Related Packages

### Platform-Specific UI Packages
- **Nethereum.Wallet.UI.Components.Blazor.Trezor** - Blazor Razor components and prompts
- Additional platform packages (Avalonia, MAUI) can implement UI for these ViewModels

### Core Dependencies
- **Nethereum.Wallet.Trezor** - Trezor device communication and account types
- **Nethereum.Wallet.UI.Components** - Base MVVM framework and registry system
- **Nethereum.Wallet** - Vault and account management

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
- [Trezor Documentation](https://trezor.io)
