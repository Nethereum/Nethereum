# Nethereum.Wallet.UI.Components

Platform-agnostic wallet UI components, view models, and services for Nethereum applications. Provides MVVM-based ViewModels and business logic that can be consumed by any UI framework (Blazor, Avalonia, MAUI, etc.).

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components
```

## Target Framework

- net10.0

## Dependencies

### NuGet Packages
- CommunityToolkit.Mvvm 8.4.0 - MVVM framework
- Microsoft.AspNetCore.Components.WebAssembly 10.0.0 - Razor component support
- Microsoft.Extensions.Hosting.Abstractions 10.0.0 - Hosted service abstractions

### Nethereum Packages
- Nethereum.DataServices - Chain data and 4Byte directory services
- Nethereum.RPC - RPC client functionality
- Nethereum.Wallet - Core wallet types and services

Source: Nethereum.Wallet.UI.Components.csproj:17-26

## Architecture

This package implements a **platform-agnostic MVVM architecture** using registry patterns for extensibility. UI framework-specific packages (Blazor, Avalonia, MAUI) consume these ViewModels and provide platform-specific UI components.

### Core Design Patterns

#### 1. Registry Pattern for Extensibility

**IComponentRegistry** - Maps ViewModels to UI components
```csharp
public interface IComponentRegistry
{
    void Register<TViewModel, TComponent>()
        where TViewModel : class
        where TComponent : class;
    void Register(Type viewModelType, Type componentType);
    Type? GetComponentType<TViewModel>() where TViewModel : class;
    Type? GetComponentType(Type viewModelType);
    IEnumerable<Type> GetRegisteredViewModelTypes();
}
```
Source: Core/Registry/IComponentRegistry.cs:6-15

Implementation uses `ConcurrentDictionary<Type, Type>` for thread-safe registration.
Source: Core/Registry/ComponentRegistry.cs:9

#### 2. Account Type Registry System

**IAccountCreationRegistry** - Manages account creation ViewModels
```csharp
public interface IAccountCreationRegistry
{
    void Register<TViewModel, TComponent>()
        where TViewModel : class, IAccountCreationViewModel
        where TComponent : class;
    IEnumerable<IAccountCreationViewModel> GetAvailableAccountTypes();
    Type? GetComponentType(IAccountCreationViewModel viewModel);
    Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountCreationViewModel;
}
```
Source: WalletAccounts/IAccountCreationRegistry.cs:6-14

Returns ViewModels filtered by `IsVisible` and ordered by `SortOrder`:
Source: WalletAccounts/AccountCreationRegistry.cs:33-34

**IAccountTypeMetadataRegistry** - Provides account type metadata
```csharp
public interface IAccountTypeMetadataRegistry
{
    IAccountTypeMetadata? GetMetadata(string accountType);
    IEnumerable<IAccountTypeMetadata> GetAllMetadata();
    IEnumerable<IAccountTypeMetadata> GetVisibleMetadata();
    bool HasMetadata(string accountType);
}
```
Source: WalletAccounts/IAccountTypeMetadataRegistry.cs:5-11

#### 3. Dashboard Plugin System

**IDashboardPluginRegistry** - Manages dashboard plugins
```csharp
public interface IDashboardPluginRegistry
{
    IEnumerable<IDashboardPluginViewModel> GetAvailablePlugins();
    IDashboardPluginViewModel? GetPlugin(string pluginId);
    Type? GetComponentType(IDashboardPluginViewModel viewModel);
    Type? GetComponentType<TViewModel>() where TViewModel : class, IDashboardPluginViewModel;
}
```
Source: Dashboard/IDashboardPluginRegistry.cs:6-12

**IDashboardPluginViewModel** interface:
```csharp
public interface IDashboardPluginViewModel
{
    string PluginId { get; }
    string DisplayName { get; }
    string Description { get; }
    string Icon { get; }
    int SortOrder { get; }
    bool IsVisible { get; }
    bool IsEnabled { get; }
    bool IsAvailable();
}
```
Source: Dashboard/IDashboardPluginViewModel.cs:5-15

#### 4. Account Details Registry

**IAccountDetailsRegistry** - Maps account types to detail ViewModels
```csharp
public interface IAccountDetailsRegistry
{
    void Register<TViewModel, TComponent>()
        where TViewModel : class, IAccountDetailsViewModel
        where TComponent : class;
    IEnumerable<IAccountDetailsViewModel> GetAvailableAccountDetailTypes();
    Type? GetComponentType(IAccountDetailsViewModel viewModel);
    Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountDetailsViewModel;
    Type? GetViewModelType(IWalletAccount account);
    Type? GetComponentType(Type viewModelType);
}
```
Source: AccountDetails/IAccountDetailsRegistry.cs:7-17

#### 5. Group Details Registry

**GroupDetailsRegistry** - Manages group detail views (e.g., Trezor device groups)
```csharp
public Type? GetViewModelType(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
{
    var availableViewModels = GetAvailableGroupDetailTypes();
    foreach (var viewModel in availableViewModels)
    {
        if (viewModel.CanHandle(groupId, groupAccounts))
        {
            return viewModel.GetType();
        }
    }
    return null;
}
```
Source: AccountDetails/GroupDetailsRegistry.cs:48-61

#### 6. Registry Contributor Pattern

**IWalletUIRegistryContributor** - Allows external packages to register UI components
```csharp
public interface IWalletUIRegistryContributor
{
    void Configure(IServiceProvider serviceProvider);
}
```
Source: WalletAccounts/IWalletUIRegistryContributor.cs:9-12

Enables packages like Nethereum.Wallet.UI.Components.Trezor to register their ViewModels and components into the registry during startup.

## Account Type System

### Built-in Account Types

#### 1. Mnemonic (HD Wallet)

**MnemonicAccountCreationViewModel** - Create/import HD wallet accounts

Observable Properties (CommunityToolkit.Mvvm source-generated):
```csharp
string Mnemonic { get; set; }               // BIP-39 mnemonic phrase
string MnemonicLabel { get; set; }          // User-friendly name
string MnemonicPassphrase { get; set; }     // Optional BIP-39 passphrase
bool IsRevealed { get; set; }               // Show/hide mnemonic
bool IsBackedUp { get; set; }               // User confirmed backup
bool IsGenerateMode { get; set; }           // true = generate, false = import
string ErrorMessage { get; set; }
string ValidationMessage { get; set; }
string DerivedAddress { get; set; }         // Address derived at index 0
string FinalAccountName { get; set; }
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:22-31

Computed Properties (read-only):
```csharp
bool IsValidMnemonic { get; }
int WordCount { get; }                       // 12 or 24 words
bool HasValidWordCount { get; }
string MnemonicStrength { get; }
bool CanCreateAccount { get; }               // Valid + (backed up when generating)
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:49-53

Commands:
- `GenerateMnemonicAsync()` - Generates 12-word mnemonic
- `GenerateMnemonic24Async()` - Generates 24-word mnemonic
- `ToggleRevealAsync()` - Show/hide mnemonic
- `ConfirmBackupAsync()` - Mark as backed up
- `SwitchToImportModeAsync()` / `SwitchToGenerateModeAsync()` - Toggle mode
- `CopyMnemonicToClipboardAsync()` / `CopyAddressToClipboardAsync()` - Clipboard helpers

Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:65-147, 280-295

Mnemonic Validation (private helper backing `IsValidMnemonic`):
```csharp
private (bool IsValid, string Message) ValidateMnemonic()
{
    if (string.IsNullOrWhiteSpace(Mnemonic))
        return (false, "Mnemonic is required");

    var words = Mnemonic.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (words.Length != 12 && words.Length != 24)
        return (false, $"Mnemonic must be 12 or 24 words, got {words.Length}");

    var invalidWords = new List<string>();
    foreach (var word in words)
    {
        if (!Bip39.WordList.Contains(word.ToLowerInvariant()))
            invalidWords.Add(word);
    }

    if (invalidWords.Any())
        return (false, $"Invalid words: {string.Join(", ", invalidWords)}");

    try
    {
        var hdWallet = new MinimalHDWallet(Mnemonic, null);
        var address = hdWallet.GetEthereumAddress(0);
        return (true, $"Valid {words.Length}-word mnemonic");
    }
    catch
    {
        return (false, "Invalid mnemonic checksum");
    }
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:180-210

Account Creation:
```csharp
public override IWalletAccount CreateAccount(WalletVault vault)
{
    if (!CanCreateAccount)
        throw new InvalidOperationException("Cannot create account: validation failed");

    if (string.IsNullOrWhiteSpace(MnemonicLabel))
    {
        var existingAccounts = vault.Accounts.Count;
        MnemonicLabel = existingAccounts == 0 ? "Main Wallet" : $"Account {existingAccounts + 1}";
    }

    var mnemonicInfo = new MnemonicInfo(MnemonicLabel, Mnemonic, MnemonicPassphrase);
    vault.AddMnemonic(mnemonicInfo);

    var hdWallet = new MinimalHDWallet(Mnemonic, MnemonicPassphrase);
    var address = hdWallet.GetEthereumAddress(0);

    var accountName = !string.IsNullOrWhiteSpace(FinalAccountName)
        ? FinalAccountName
        : MnemonicLabel;

    // MnemonicWalletAccount exposes only a positional constructor:
    // (string address, string label, int index, string mnemonicId, MinimalHDWallet wallet)
    return new MnemonicWalletAccount(address, accountName, 0, mnemonicInfo.Id, hdWallet);
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:224-257

**MnemonicAccountDetailsViewModel** - View/manage HD wallet account details

Commands:
- `RemoveAccountAsync()` - Delete account with confirmation
- `StartEditAccountNameAsync()` / `SaveAccountNameAsync()` / `CancelEditAccountNameAsync()` - Edit account name
- `RevealPrivateKeyAsync(string password)` - Show private key with password confirmation
- `CloseRevealedPrivateKey()` - Hide the revealed private key

Source: WalletAccounts/Mnemonic/MnemonicAccountDetailsViewModel.cs:88-260

Derivation Path:
```csharp
public string GetDerivationPath()
{
    if (Account is MnemonicWalletAccount mnemonicAccount)
        return $"m/44'/60'/0'/0/{mnemonicAccount.Index}";  // BIP-44 Ethereum path
    return "";
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountDetailsViewModel.cs:262-269

**VaultMnemonicAccountViewModel** - Create account from an existing vault mnemonic

Form Steps:
```csharp
public enum FormStep
{
    SelectMnemonic = 0,   // Choose from vault mnemonics
    Configure = 1,        // Set account index
    Confirm = 2           // Review and create
}
```
Source: WalletAccounts/Mnemonic/VaultMnemonicAccountViewModel.cs:28-33

Observable Properties:
```csharp
string SelectedMnemonicId { get; set; }
int AccountIndex { get; set; }                       // Derivation index (default 1)
string AccountName { get; set; }
FormStep CurrentStep { get; set; }                   // Wizard step
List<MnemonicInfo> AvailableMnemonics { get; set; }
string DerivedAddress { get; set; }                  // Preview address
bool HasDuplicateAccount { get; set; }
bool IsLoading { get; set; }
```
Source: WalletAccounts/Mnemonic/VaultMnemonicAccountViewModel.cs:35-44

**MnemonicListViewModel** - Manage vault mnemonics

Commands:
- `LoadMnemonicsAsync()` - Load all mnemonics from vault
- `RefreshAsync()` - Reload the list
- `DeleteMnemonicAsync(MnemonicItemViewModel)` - Delete mnemonic with validation

Source: WalletAccounts/Mnemonic/MnemonicListViewModel.cs:44-103

#### 2. Private Key

**PrivateKeyAccountCreationViewModel** - Import account from private key

Observable Properties:
```csharp
string PrivateKey { get; set; }             // 64 hex characters
string Label { get; set; }                  // Account name
bool IsRevealed { get; set; }               // Show/hide private key
string ErrorMessage { get; set; }
string ValidationMessage { get; set; }
string DerivedAddress { get; set; }         // Calculated address
```
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:40-45

Computed Properties (read-only):
```csharp
bool IsValidPrivateKey { get; }
string PrivateKeyFormat { get; }            // Format description
bool CanCreateAccount { get; }
```
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:47-49

Validation (private helper backing `IsValidPrivateKey`):
```csharp
private (bool IsValid, string Message) ValidatePrivateKey()
{
    if (string.IsNullOrWhiteSpace(PrivateKey))
        return (false, _localizer.GetString(Keys.PrivateKeyRequiredError));

    var cleanKey = CleanPrivateKey(PrivateKey);

    if (!Regex.IsMatch(cleanKey, "^[0-9a-fA-F]+$"))
        return (false, _localizer.GetString(Keys.InvalidHexStringError));

    // Check length (32 bytes = 64 hex characters)
    if (cleanKey.Length != 64)
        return (false, _localizer.GetString(Keys.InvalidLengthError, cleanKey.Length));

    if (cleanKey.All(c => c == '0'))
        return (false, _localizer.GetString(Keys.PrivateKeyCannotBeZeroError));

    try
    {
        var address = new EthECKey(cleanKey).GetPublicAddress();
        return (true, _localizer.GetString(Keys.ValidPrivateKeySuccess, address));
    }
    catch (Exception ex)
    {
        return (false, _localizer.GetString(Keys.InvalidPrivateKeyError, ex.Message));
    }
}
```
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:93-120

Cleans input by removing the "0x" prefix.
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:122-133

**PrivateKeyAccountDetailsViewModel** - View/manage private key account

Provides commands to edit the account name, reveal the private key with password protection, and remove the account.
Source: WalletAccounts/PrivateKey/PrivateKeyAccountDetailsViewModel.cs:81-233

#### 3. View-Only

**ViewOnlyAccountCreationViewModel** - Watch-only account

Observable Properties:
```csharp
string ViewOnlyAddress { get; set; }        // Ethereum address to watch
string Label { get; set; }                  // Account name
string ErrorMessage { get; set; }
```
Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:28-30

Validation (`CanCreateAccount`):
- Address must be non-empty
- Address must start with "0x"
- Address must be 42 characters

Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:32-34

Creates `ViewOnlyWalletAccount` with no private key operations.
Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:46

**ViewOnlyAccountDetailsViewModel** - Read-only account operations

No private key or signing operations available. Can only view balances and transaction history.
Source: WalletAccounts/ViewOnly/ViewOnlyAccountDetailsViewModel.cs:59-179

#### 4. Smart Contract (Account Abstraction)

**SmartContractAccountCreationViewModel** - Smart contract wallet account

Observable Properties:
```csharp
string Address { get; set; }                // Smart contract address
string Label { get; set; }                  // Account name
string ErrorMessage { get; set; }
```
Source: WalletAccounts/SmartContract/SmartContractAccountCreationViewModel.cs:28-30

Creates a `SmartContractWalletAccount` from the supplied contract address.
Source: WalletAccounts/SmartContract/SmartContractAccountCreationViewModel.cs:46

**SmartContractAccountMetadataViewModel** provides the display metadata (`TypeName` from `SmartContractWalletAccount.TypeName`, icon `smart_toy`, color theme `warning`).
Source: WalletAccounts/SmartContract/SmartContractAccountMetadataViewModel.cs:15-23

### Account Type Extensibility

**IAccountCreationViewModel** interface:
```csharp
public interface IAccountCreationViewModel
{
    string DisplayName { get; }
    string Description { get; }
    string Icon { get; }
    int SortOrder { get; }
    bool IsVisible { get; }
    bool CanCreateAccount { get; }
    IWalletAccount CreateAccount(WalletVault vault);
    void Reset();
}
```
Source: WalletAccounts/IAccountCreationViewModel.cs:5-15

**IAccountTypeMetadata** interface:
```csharp
public interface IAccountTypeMetadata
{
    string TypeName { get; }
    string DisplayName { get; }
    string Description { get; }
    string Icon { get; }
    string ColorTheme { get; }
    int SortOrder { get; }
    bool IsVisible { get; }
}
```
Source: WalletAccounts/IAccountTypeMetadata.cs:3-12

**IAccountDetailsViewModel** interface:
```csharp
public interface IAccountDetailsViewModel
{
    string AccountType { get; }
    bool CanHandle(IWalletAccount account);
    Task InitializeAsync(IWalletAccount account);
    IWalletAccount? Account { get; }
    bool IsLoading { get; }
    string ErrorMessage { get; }
    string SuccessMessage { get; }
    void ClearMessages();
}
```
Source: AccountDetails/IAccountDetailsViewModel.cs:6-16

Custom account types can be added by implementing these interfaces and registering via `IWalletUIRegistryContributor`.

## Network Management

### Add Custom Network

**AddCustomNetworkViewModel** - Configure custom EVM networks

Dependencies:
```csharp
private readonly IChainManagementService _chainManagementService;
private readonly IRpcEndpointService _rpcEndpointService;
```
Source: Networks/AddCustomNetworkViewModel.cs:14-15

Properties:
```csharp
NetworkConfiguration Network { get; set; }
bool IsLoading { get; set; }
string? ErrorMessage { get; set; }
string? SuccessMessage { get; set; }
bool IsFormValid { get; }
Action<BigInteger>? OnNetworkAdded { get; set; }
Action? OnCancel { get; set; }
```
Source: Networks/AddCustomNetworkViewModel.cs:17-25

Commands:
```csharp
SaveNetworkAsync()                            // Validate and add the custom chain
Cancel()
Reset()
AddRpcEndpoint()
RemoveRpcEndpoint(RpcEndpointInfo)
AddBlockExplorer()
RemoveBlockExplorer(string explorer)
TestRpcEndpointAsync(RpcEndpointInfo)         // Test RPC connectivity
```
Source: Networks/AddCustomNetworkViewModel.cs:37-141

**NetworkConfiguration** model - a validation-aware, observable model (not a plain DTO):
```csharp
public partial class NetworkConfiguration : LocalizedValidationModel
{
    public NetworkConfiguration(IComponentLocalizer localizer) : base(localizer)
    {
        RpcEndpoints = new ObservableCollection<RpcEndpointInfo>();
        BlockExplorers = new ObservableCollection<string>();
    }

    // Observable properties (CommunityToolkit.Mvvm source-generated)
    string ChainId { get; set; }             // stored as string; parse via ChainIdValue
    string NetworkName { get; set; }
    string CurrencySymbol { get; set; }
    string CurrencyName { get; set; }
    int CurrencyDecimals { get; set; }       // default 18
    bool IsTestnet { get; set; }
    bool SupportEip155 { get; set; }         // default true
    bool SupportEip1559 { get; set; }        // default true
    ObservableCollection<RpcEndpointInfo> RpcEndpoints { get; set; }
    ObservableCollection<string> BlockExplorers { get; set; }
    string NewRpcUrl { get; set; }
    string NewExplorerUrl { get; set; }

    // Computed
    public bool IsValid => !HasErrors && RpcEndpoints.Any();
    public BigInteger ChainIdValue => BigInteger.TryParse(ChainId, out var id) ? id : 0;
}
```
Source: Networks/Models/NetworkConfiguration.cs:14-39

**RpcEndpointInfo** model:
```csharp
public partial class RpcEndpointInfo : ObservableObject
{
    public RpcEndpointInfo(string url, bool isWebSocket = false)
    {
        Url = url;
        IsWebSocket = isWebSocket;
    }

    string Url { get; set; }
    bool IsWebSocket { get; set; }
    bool IsEnabled { get; set; }             // default true
    bool IsHealthy { get; set; }             // default true
    string? TestResult { get; set; }
    bool IsTesting { get; set; }
    bool IsCustom { get; set; }

    // Display properties
    public string TypeDisplayName => IsWebSocket ? "WebSocket" : "HTTP";
    public string StatusDisplayName => IsEnabled ? "Active" : "Inactive";
    public string HealthDisplayName => IsHealthy ? "Healthy" : (TestResult ?? "Unknown");
}
```
Source: Networks/Models/RpcEndpointInfo.cs:5-24

**NetworkManagementPluginViewModel** - Dashboard plugin for network management

Implements `IDashboardPluginViewModel`.
Source: Networks/NetworkManagementPluginViewModel.cs

### Network Provider Service

**INetworkProviderService** - Default network configuration
```csharp
public interface INetworkProviderService
{
    Task<List<ChainFeature>> GetDefaultNetworksAsync();
}
```
Source: Abstractions/INetworkProviderService.cs:7-10

## Transaction Management

### Native Token Transfer

**TokenNativeTransferModel** - Native/ERC-20 token transfer model, a validation-aware observable model.

```csharp
public partial class TokenNativeTransferModel : LocalizedValidationModel
{
    public TokenNativeTransferModel(IComponentLocalizer localizer) : base(localizer) { ... }
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:26-33

Validation Attributes (declared on the source-generated observable fields):
```csharp
[ObservableProperty]
[NotifyDataErrorInfo]
[Required(ErrorMessage = "The field is required")]
[EthereumAddress(ErrorMessage = "Invalid Ethereum address")]
[NotifyPropertyChangedFor(nameof(IsValid))]
private string _recipientAddress = "";

[ObservableProperty]
[NotifyDataErrorInfo]
[Required(ErrorMessage = "The field is required")]
[NotifyPropertyChangedFor(nameof(IsValid))]
private string _amount = "";

[ObservableProperty]
[NotifyDataErrorInfo]
[Hex(ErrorMessage = "Invalid hex value")]
[NotifyPropertyChangedFor(nameof(IsValid))]
private string _transactionData = "";
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:37-67

Other Properties:
```csharp
string FromAddress { get; set; }
BigInteger AvailableBalance { get; set; }
string TokenSymbol { get; set; }            // e.g., "ETH"
int TokenDecimals { get; set; }             // e.g., 18
bool ShowAdvancedOptions { get; set; }
bool IsNativeToken { get; set; }
string? ContractAddress { get; set; }
string? TokenLogoUri { get; set; }
long ChainId { get; set; }
string Nonce { get; set; }
decimal AmountValue { get; }                // parsed from Amount
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:35-73

Methods:
```csharp
public BigInteger GetTransferAmountInSmallestUnit() =>
    UnitConversion.Convert.ToWei(AmountValue, TokenDecimals);
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:75-76

```csharp
public string FormattedAvailableBalance
{
    get
    {
        if (AvailableBalance == BigInteger.Zero)
            return "0";

        var tokenValue = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);

        if (tokenValue >= 1000)
            return tokenValue.ToString("N2");
        else if (tokenValue >= 1)
            return tokenValue.ToString("F4").TrimEnd('0').TrimEnd('.');
        else if (tokenValue >= 0.001m)
            return tokenValue.ToString("F6").TrimEnd('0').TrimEnd('.');
        else
            return tokenValue.ToString("F8").TrimEnd('0').TrimEnd('.');
    }
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:78-96

```csharp
public bool ValidateAmountBalance()
{
    if (string.IsNullOrWhiteSpace(Amount)) return false;
    if (!decimal.TryParse(Amount, out var amt) || amt <= 0) return false;

    if (GetTransferAmountInSmallestUnit() > AvailableBalance)
    {
        ClearCustomErrors(nameof(Amount));
        AddCustomError(Keys.InsufficientBalance, nameof(Amount));
        return false;
    }

    ClearCustomErrors(nameof(Amount));
    return true;
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:175-189

```csharp
public void SetMaxAmount()
{
    if (AvailableBalance > BigInteger.Zero)
    {
        var tokenValue = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);
        Amount = tokenValue.ToString();
    }
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:203-210

### Gas Strategy

**GasStrategy** enum:
```csharp
public enum GasStrategy
{
    Slow,
    Normal,
    Fast,
    Custom
}
```
Source: SendTransaction/Models/GasStrategy.cs:3-9

**GasStrategyDisplay** - Gas estimation data:
```csharp
public class GasStrategyDisplay
{
    public GasStrategy Strategy { get; set; }
    public string? EstimatedTime { get; set; }
    public string? EstimatedCost { get; set; }
    public BigInteger? MaxFee { get; set; }           // EIP-1559
    public BigInteger? PriorityFee { get; set; }      // EIP-1559
    public BigInteger? GasPrice { get; set; }         // Legacy
    public bool IsAvailable { get; set; } = true;
}
```
Source: SendTransaction/Models/GasStrategyDisplay.cs:5-14

**GasMultiplierOption** - Pre-defined gas multipliers:
```csharp
public class GasMultiplierOption
{
    public decimal Multiplier { get; set; }
    public string DisplayText { get; set; } = "";
    public string LocalizationKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public bool IsRecommended { get; set; }

    public static readonly GasMultiplierOption Economy = new()
    {
        Multiplier = 0.8m,
        DisplayText = "0.8x",
        LocalizationKey = "Multiplier08",
        DescriptionKey = "Multiplier08Description"
    };

    public static readonly GasMultiplierOption Standard = new()
    {
        Multiplier = 1.0m,
        DisplayText = "1.0x",
        LocalizationKey = "Multiplier10",
        DescriptionKey = "Multiplier10Description",
        IsRecommended = true
    };

    public static readonly GasMultiplierOption Priority = new()
    {
        Multiplier = 1.2m,
        DisplayText = "1.2x",
        LocalizationKey = "Multiplier12",
        DescriptionKey = "Multiplier12Description"
    };

    public static readonly GasMultiplierOption[] All = { Economy, Standard, Priority };
}
```
Source: SendTransaction/Models/GasMultiplierOption.cs:3-37

### Transaction Monitoring

**TransactionMonitoringService** - Background transaction monitoring

Implements `IHostedService`:
```csharp
public class TransactionMonitoringService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private IServiceScope? _scope;
    private IPendingTransactionService? _pendingTransactionService;

    public TransactionMonitoringService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scope = _serviceProvider.CreateScope();
        _pendingTransactionService = _scope.ServiceProvider.GetRequiredService<IPendingTransactionService>();
        _pendingTransactionService.StartMonitoring();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _pendingTransactionService?.StopMonitoring();
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _scope?.Dispose();
}
```
Source: Transactions/TransactionMonitoringService.cs:10-40

Registered via `AddTransactionServices()` extension.
Source: Transactions/TransactionServiceCollectionExtensions.cs:19

### Transaction Steps

**TokenTransferStep** enum:
```csharp
public enum TokenTransferStep
{
    TransactionInput = 0,        // Enter recipient and amount
    TransactionConfirmation = 1, // Review transaction
    TransactionStatus = 2        // Track submission status
}
```
Source: SendTransaction/Models/SendTransactionStep.cs:3-8

## Configuration System

### Base Configuration

**BaseWalletConfiguration** - Abstract base for component configuration

Properties:
```csharp
string ComponentId { get; set; }                         // default: Guid
WalletFlowMode FlowMode { get; set; }                    // Simple/Advanced/Custom
WalletTextConfiguration Text { get; set; }
WalletBehaviorConfiguration Behavior { get; set; }
WalletSecurityConfiguration Security { get; set; }
```
Source: Core/Configuration/BaseWalletConfiguration.cs:7-11

**WalletFlowMode** enum:
```csharp
public enum WalletFlowMode
{
    Simple,      // Simplified UI for basic users
    Advanced,    // Full feature set
    Custom       // Fully customizable
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:35-40

**WalletTextConfiguration** - UI text customization:
```csharp
public class WalletTextConfiguration
{
    public string LoginTitle { get; set; } = "Welcome Back";
    public string LoginSubtitle { get; set; } = "Enter your password to unlock your wallet";
    public string LoginButtonText { get; set; } = "Unlock Wallet";
    public string CreateTitle { get; set; } = "Create New Wallet";
    public string CreateSubtitle { get; set; } = "Set up a new wallet vault to securely store your accounts";
    public string CreateButtonText { get; set; } = "Create Wallet";
    public string PasswordLabel { get; set; } = "Password";
    public string CreatePasswordLabel { get; set; } = "Create Password";
    public string ConfirmPasswordLabel { get; set; } = "Confirm Password";
    public string PasswordHelperText { get; set; } = "Choose a strong password to protect your wallet";
    // ... additional properties
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:41-67

**WalletBehaviorConfiguration** - Feature toggles:
```csharp
public class WalletBehaviorConfiguration
{
    public bool EnableWalletReset { get; set; } = false;
    public bool AutoFocusPasswordField { get; set; } = true;
    public bool ShowPasswordStrengthIndicator { get; set; } = true;
    public bool EnablePasswordVisibilityToggle { get; set; } = true;
    public bool AutoSaveProgress { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    public bool ValidatePasswordStrength { get; set; } = true;
    public bool EnableFormValidation { get; set; } = true;
    public bool ShowLoadingIndicators { get; set; } = true;
    public int OperationTimeoutSeconds { get; set; } = 60;
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:68-80

**WalletSecurityConfiguration** - Security settings:
```csharp
public class WalletSecurityConfiguration
{
    public int MinPasswordLength { get; set; } = 8;
    public int MaxPasswordLength { get; set; } = 128;
    public bool RequireUppercasePassword { get; set; } = true;
    public bool RequireLowercasePassword { get; set; } = true;
    public bool RequireNumericPassword { get; set; } = true;
    public bool RequireSpecialCharacterPassword { get; set; } = true;
    public bool EnableRateLimiting { get; set; } = false;
    public int MaxLoginAttempts { get; set; } = 5;
    public int RateLimitWindowMinutes { get; set; } = 15;
    public bool EnableSessionTimeout { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public bool RequirePasswordConfirmation { get; set; } = true;
    public bool EnableSecurityLogging { get; set; } = true;
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:81-96

### Configuration Builder Pattern

**BaseWalletConfigurationBuilder** - Fluent API for configuration:
```csharp
public abstract class BaseWalletConfigurationBuilder<TConfiguration, TBuilder>
    where TConfiguration : BaseWalletConfiguration, new()
    where TBuilder : BaseWalletConfigurationBuilder<TConfiguration, TBuilder>
{
    public TBuilder UseSimpleFlow();
    public TBuilder UseAdvancedFlow();
    public TBuilder UseCustomFlow();
    public TBuilder WithTitle(string title);
    public TBuilder WithSubtitle(string subtitle);
    public TBuilder EnableWalletReset(bool enable = true);
    public TBuilder WithMinPasswordLength(int length);
    public TBuilder ConfigureText(Action<WalletTextConfiguration> configure);
    public TBuilder ConfigureBehavior(Action<WalletBehaviorConfiguration> configure);
    public TBuilder ConfigureSecurity(Action<WalletSecurityConfiguration> configure);
    public TConfiguration Build();
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:97-168

### Component-Specific Configurations

**AccountListConfiguration**:
```csharp
public class AccountListConfiguration : BaseWalletConfiguration, IComponentConfiguration
{
    public new string ComponentId { get; set; } = "AccountList";
    public bool ShowBalances { get; set; } = true;
    public bool AllowAccountDeletion { get; set; } = true;
    public bool AllowAccountEditing { get; set; } = true;
    public int AccountsPerPage { get; set; } = 10;
}
```
Source: AccountList/AccountListConfiguration.cs:5-12

**WalletOverviewConfiguration**:
```csharp
public class WalletOverviewConfiguration : BaseWalletConfiguration, IComponentConfiguration
{
    public new string ComponentId { get; set; } = "WalletOverview";
    public bool ShowBalance { get; set; } = true;
    public bool ShowFiatBalance { get; set; } = false;
    public bool ShowQuickActions { get; set; } = true;
    public bool AutoRefreshBalance { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
}
```
Source: WalletOverview/WalletOverviewConfiguration.cs:5-13

**CreateAccountConfiguration**:
```csharp
public class CreateAccountConfiguration : BaseWalletConfiguration, IComponentConfiguration
{
    public new string ComponentId { get; set; } = "CreateAccount";
    public bool ShowAccountTypeDescriptions { get; set; } = true;
    public bool AutoSelectNewAccount { get; set; } = true;
}
```
Source: CreateAccount/CreateAccountConfiguration.cs:5-10

**NethereumWalletConfiguration**:
```csharp
public class NethereumWalletConfiguration : BaseWalletConfiguration, IComponentConfiguration
{
    public new string ComponentId { get; set; } = "NethereumWallet";
    public bool ShowProgressIndicators { get; set; } = true;
    public bool EnableKeyboardShortcuts { get; set; } = true;
    public int PasswordMinimumStrength { get; set; } = 1;
    public bool AllowPasswordVisibilityToggle { get; set; } = true;
    public bool ShowPasswordStrengthIndicator { get; set; } = true;
}
```
Source: NethereumWallet/NethereumWalletConfiguration.cs:7-16

## Localization System

### Localization Service

**IWalletLocalizationService** - Multi-language support
```csharp
public interface IWalletLocalizationService
{
    string CurrentLanguage { get; }
    CultureInfo CurrentCulture { get; }
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
    event Action<string> LanguageChanged;

    Task SetLanguageAsync(string languageCode);
    Task<string> DetectAndSetLanguageAsync();

    void RegisterTranslations(string componentName, string language, Dictionary<string, string> translations);
    void OverrideTranslation(string componentName, string language, string key, string value);

    string GetTranslation(string componentName, string language, string key);
    string GetTranslation(string componentName, string language, string key, params object[] args);

    IComponentLocalizer<T> GetLocalizer<T>();
    void RegisterLocalizer<T>(IComponentLocalizer<T> localizer);

    Task LoadTranslationsAsync(string componentName, string language, string json);
    bool IsLanguageSupported(string languageCode);
    void AddLanguageSupport(string languageCode, string defaultCulture);
    void SetDefaultLanguage(string defaultCulture);
}
```
Source: Core/Localization/IWalletLocalizationService.cs:8-26

**LanguageInfo** model:
```csharp
public class LanguageInfo
{
    public string Code { get; set; }           // e.g., "en"
    public string Name { get; set; }           // e.g., "English"
    public string NativeName { get; set; }     // e.g., "English"
    public bool IsRTL { get; set; }            // Right-to-left support
    public string Culture { get; set; }        // e.g., "en-US"
}
```
Source: Core/Localization/IWalletLocalizationService.cs:27-34

**WalletLocalizationService** implementation:

Supported Languages (default):
```csharp
public IReadOnlyList<LanguageInfo> AvailableLanguages { get; } = new List<LanguageInfo>
{
    new LanguageInfo { Code = "en", Name = "English", NativeName = "English", Culture = "en-US" },
    new LanguageInfo { Code = "es", Name = "Spanish", NativeName = "Español", Culture = "es-ES" }
};
```
Source: Core/Localization/WalletLocalizationService.cs:20-24

### Storage Providers

**ILocalizationStorageProvider** - Platform abstraction:
```csharp
public interface ILocalizationStorageProvider
{
    Task<string> GetStoredLanguageAsync();
    Task SetStoredLanguageAsync(string languageCode);
    Task<string> GetSystemLanguageAsync();
}
```
Source: Core/Localization/ILocalizationStorageProvider.cs:5-10

**BrowserLocalizationStorageProvider** - Browser localStorage:
```csharp
public class BrowserLocalizationStorageProvider : ILocalizationStorageProvider
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "wallet-ui-language";

    public BrowserLocalizationStorageProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async Task<string> GetStoredLanguageAsync()
    {
        try { return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey); }
        catch { return null; }
    }

    public async Task SetStoredLanguageAsync(string languageCode)
    {
        try { await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, languageCode); }
        catch { }
    }

    public async Task<string> GetSystemLanguageAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("eval",
                "navigator.language || navigator.userLanguage || 'en-US'");
        }
        catch { return "en-US"; }
    }
}
```
Source: Core/Localization/BrowserLocalizationStorageProvider.cs:7-52

**SystemLocalizationStorageProvider** - File-based storage:
```csharp
public class SystemLocalizationStorageProvider : ILocalizationStorageProvider
{
    private readonly string _settingsFilePath;

    public SystemLocalizationStorageProvider(string settingsDirectory = null)
    {
        var dir = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NethereumWallet");

        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "language.txt");
    }

    public async Task<string> GetStoredLanguageAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
                return await File.ReadAllTextAsync(_settingsFilePath);
        }
        catch { }
        return null;
    }

    public async Task SetStoredLanguageAsync(string languageCode)
    {
        try { await File.WriteAllTextAsync(_settingsFilePath, languageCode); }
        catch { }
    }

    public Task<string> GetSystemLanguageAsync() =>
        Task.FromResult(CultureInfo.CurrentUICulture.Name);
}
```
Source: Core/Localization/SystemLocalizationStorageProvider.cs:8-54

### Component Localizer

**ComponentLocalizerBase<T>** - Type-safe localization:
```csharp
public abstract class ComponentLocalizerBase<T> : IComponentLocalizer<T>
{
    protected readonly IWalletLocalizationService _globalService;
    protected readonly string _componentName;

    protected ComponentLocalizerBase(IWalletLocalizationService globalService)
    {
        _globalService = globalService;
        _componentName = typeof(T).FullName ?? typeof(T).Name;
        RegisterTranslations();
    }

    public string GetString(string key)
    {
        return _globalService.GetTranslation(_componentName, _globalService.CurrentLanguage, key);
    }

    public string GetString(string key, params object[] args)
    {
        return _globalService.GetTranslation(_componentName, _globalService.CurrentLanguage, key, args);
    }

    protected abstract void RegisterTranslations();
}
```
Source: Core/Localization/ComponentLocalizerBase.cs:5-27

## Dashboard Navigation

**IDashboardNavigationService** - Plugin navigation
```csharp
public interface IDashboardNavigationService
{
    Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null);
    Task NavigateCurrentPluginAsync(Dictionary<string, object> parameters);
    void RegisterActivePlugin(string pluginId, object? pluginComponent);
    event NavigationRequestedHandler? NavigationRequested;
}

public delegate Task NavigationRequestedHandler(object sender, DashboardNavigationEventArgs e);
```
Source: Dashboard/Services/IDashboardNavigationService.cs:6-25

**DashboardNavigationService** implementation:
```csharp
public async Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null)
{
    // If navigating to the same plugin and it implements INavigatablePlugin, call directly
    if (_activePluginId == pluginId &&
        _activePluginComponent is INavigatablePlugin navigatablePlugin)
    {
        await navigatablePlugin.NavigateWithParametersAsync(parameters ?? new Dictionary<string, object>());
        return;
    }

    if (NavigationRequested != null)
    {
        var args = new DashboardNavigationEventArgs(pluginId, parameters);
        await NavigationRequested.Invoke(this, args);
    }
}
```
Source: Dashboard/Services/DashboardNavigationService.cs:13-28

**INavigatablePlugin** - Plugin parameter handling:
```csharp
public interface INavigatablePlugin
{
    Task NavigateWithParametersAsync(Dictionary<string, object> parameters);
}
```
Source: Dashboard/INavigatablePlugin.cs:6-9

## Services

### Prompt Overlay Service

**IPromptOverlayService** - DApp interaction prompts
```csharp
public interface IPromptOverlayService
{
    bool IsOverlayVisible { get; }
    PromptRequest? CurrentPrompt { get; }
    int CurrentIndex { get; }

    Task ShowPromptAsync(PromptRequest prompt);
    Task ShowPromptByIdAsync(string promptId);
    Task ShowNextPromptAsync();
    Task ShowPreviousPromptAsync();
    void HideOverlay();
    void MinimizeOverlay();

    event EventHandler<OverlayStateChangedEventArgs>? OverlayStateChanged;
}

public class OverlayStateChangedEventArgs : EventArgs
{
    public bool IsVisible { get; set; }
    public PromptRequest? CurrentPrompt { get; set; }
}
```
Source: Services/IPromptOverlayService.cs:6-26

**PromptOverlayService** implementation:

Dependencies: `IPromptQueueService`
Source: Services/PromptOverlayService.cs:10

```csharp
public async Task ShowPromptAsync(PromptRequest prompt)
{
    CurrentPrompt = prompt;
    var prompts = _queueService.PendingPrompts;
    CurrentIndex = prompts.ToList().IndexOf(prompt);

    if (CurrentPrompt != null)
    {
        CurrentPrompt.Status = PromptStatus.InProgress;
        IsOverlayVisible = true;

        OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
        {
            IsVisible = true,
            CurrentPrompt = CurrentPrompt
        });
    }
}
```
Source: Services/PromptOverlayService.cs:23-40

```csharp
public void HideOverlay()
{
    IsOverlayVisible = false;
    CurrentPrompt = null;

    OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
    {
        IsVisible = false,
        CurrentPrompt = null
    });
}
```
Source: Services/PromptOverlayService.cs:76-86

```csharp
public void MinimizeOverlay()
{
    IsOverlayVisible = false;
    // CurrentPrompt remains set

    OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
    {
        IsVisible = false,
        CurrentPrompt = CurrentPrompt
    });
}
```
Source: Services/PromptOverlayService.cs:88-97

### Platform Abstraction Interfaces

**IWalletNotificationService** - Toast notifications
```csharp
public interface IWalletNotificationService
{
    void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info);
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);

    void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action);
    void ShowSuccessWithAction(string message, NotificationAction action);
    void ShowInfoWithAction(string message, NotificationAction action);
}

public enum NotificationSeverity
{
    Success,
    Info,
    Warning,
    Error
}

public class NotificationAction
{
    public string Label { get; set; } = "View";
    public Action? OnClick { get; set; }
}
```
Source: Abstractions/GenericInterfaces.cs:8-33

**IWalletDialogService** - Modal dialogs
```csharp
public interface IWalletDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task<T?> ShowDialogAsync<T>(object? parameters = null) where T : class;
    Task<bool> ShowWarningConfirmationAsync(string title, string message,
        string confirmText = "Remove", string cancelText = "Cancel");
    Task ShowErrorAsync(string title, string message);
    Task ShowSuccessAsync(string title, string message);
}
```
Source: Abstractions/GenericInterfaces.cs:35-43

**IWalletNavigationService** - Route navigation
```csharp
public interface IWalletNavigationService
{
    Task GoToAsync(string route);
}
```
Source: Abstractions/GenericInterfaces.cs:45-48

**IWalletLoadingService** - Loading indicators
```csharp
public interface IWalletLoadingService
{
    bool IsLoading { get; }
    string? LoadingMessage { get; }
    double Progress { get; }

    void SetLoading(bool isLoading, string? message = null);
    void ShowProgress(double percentage, string? message = null);
}
```
Source: Abstractions/GenericInterfaces.cs:50-58

## Utilities

### Identicon Generator

**IdenticonGenerator** - Address visualization
```csharp
public static class IdenticonGenerator
{
    public static string GetIdenticonText(string address)
    {
        if (string.IsNullOrEmpty(address)) return "?";
        return address.Length >= 4 ? address.Substring(2, 2).ToUpper() : "??";
    }

    public static string GetNetworkIdenticonText(string networkName)
    {
        if (string.IsNullOrEmpty(networkName)) return "?";

        if (networkName.Length >= 3)
            return networkName.Substring(0, 3).ToUpper();
        else if (networkName.Length >= 2)
            return networkName.Substring(0, 2).ToUpper();
        else
            return networkName.ToUpper().PadRight(2, '?');
    }

    public static string GetTokenIdenticonText(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return "?";

        if (symbol.Length >= 3)
            return symbol.Substring(0, 3).ToUpper();
        else if (symbol.Length >= 2)
            return symbol.Substring(0, 2).ToUpper();
        else
            return symbol.ToUpper();
    }

    public static string GetIdenticonStyle(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "background: #ccc;";

        var addressBytes = address.ToLowerInvariant().Replace("0x", "");
        var hash1 = 0;
        var hash2 = 0;

        for (int i = 0; i < Math.Min(addressBytes.Length, 8); i++)
        {
            hash1 = hash1 * 31 + addressBytes[i];
            if (i + 8 < addressBytes.Length)
                hash2 = hash2 * 31 + addressBytes[i + 8];
        }

        var hue = Math.Abs(hash1) % 360;
        var saturation = 65 + (Math.Abs(hash2) % 20);
        var lightness = 45 + (Math.Abs(hash1 >> 8) % 20);

        return $"background: hsl({hue}, {saturation}%, {lightness}%); color: white; font-weight: 600; font-size: 1.1rem;";
    }
}
```
Source: Utils/IdenticonGenerator.cs:5-60

### Network Icon Repository

**INetworkIconProvider** - Network icon abstraction
```csharp
public interface INetworkIconProvider
{
    string? GetNetworkIcon(BigInteger chainId);
    bool HasNetworkIcon(BigInteger chainId);
}
```
Source: Utils/NetworkIconRepository.cs:5-9

**DefaultNetworkIconProvider** - Placeholder implementation
```csharp
public class DefaultNetworkIconProvider : INetworkIconProvider
{
    public string? GetNetworkIcon(BigInteger chainId) => null;
    public bool HasNetworkIcon(BigInteger chainId) => false;
}
```
Source: Utils/NetworkIconRepository.cs:10-14

## Service Registration

### Transaction Services

**TransactionServiceCollectionExtensions**:
```csharp
public static IServiceCollection AddTransactionServices(this IServiceCollection services)
{
    services.AddScoped<IPendingTransactionService, PendingTransactionService>();

    services.AddTransient<TransactionHistoryViewModel>();

    services.AddSingleton<TransactionHistoryLocalizer>();
    services.AddTransient<IComponentLocalizer<TransactionHistoryViewModel>>(provider =>
        provider.GetRequiredService<TransactionHistoryLocalizer>());

    services.AddHostedService<TransactionMonitoringService>();

    return services;
}
```
Source: Transactions/TransactionServiceCollectionExtensions.cs:9-22

### Token Transfer Services

**TokenTransferServiceCollectionExtensions**:
```csharp
public static IServiceCollection AddTokenTransferServices(this IServiceCollection services)
{
    services.AddTransient<FourByteDirectoryService>();
    services.AddTransient<FourByteDataDecodingService>();

    services.AddTransient<IGasPriceProvider, NethereumGasPriceProvider>();
    services.TryAddSingleton<IGasConfigurationPersistenceService, InMemoryGasConfigurationPersistenceService>();

    services.TryAddSingleton<IABIInfoStorage>(sp => ABIInfoStorageFactory.CreateWithSourcifyOnly());

    services.AddTransient<ITransactionDataDecodingService>(sp =>
    {
        var abiStorage = sp.GetRequiredService<IABIInfoStorage>();
        var fourByteFallback = sp.GetRequiredService<FourByteDataDecodingService>();
        return new SourcifyDataDecodingService(abiStorage, fourByteFallback);
    });

    services.AddTransient<IStateChangesPreviewService, StateChangesPreviewService>();

    services.AddTransient<TokenNativeTransferModel>();
    services.AddTransient<TransactionModel>();

    services.AddTransient<TransactionViewModel>();
    services.AddTransient<SendNativeTokenViewModel>();
    services.AddTransient<TransactionStatusViewModel>();

    services.AddSingleton<SharedValidationLocalizer>();
    services.AddTransient<IComponentLocalizer>(provider =>
        provider.GetRequiredService<SharedValidationLocalizer>());

    services.AddSingleton<TransactionLocalizer>();
    services.AddSingleton<SendNativeTokenLocalizer>();
    services.AddSingleton<TransactionStatusLocalizer>();

    services.AddTransient<IComponentLocalizer<TransactionViewModel>>(provider =>
        provider.GetRequiredService<TransactionLocalizer>());
    services.AddTransient<IComponentLocalizer<SendNativeTokenViewModel>>(provider =>
        provider.GetRequiredService<SendNativeTokenLocalizer>());
    services.AddTransient<IComponentLocalizer<TransactionStatusViewModel>>(provider =>
        provider.GetRequiredService<TransactionStatusLocalizer>());

    services.AddScoped<TokenTransferPluginViewModel>();
    services.AddScoped<IDashboardPluginViewModel, TokenTransferPluginViewModel>();

    return services;
}
```
Source: SendTransaction/TokenTransferServiceCollectionExtensions.cs:17-74

## Related Packages

### Platform-Specific UI Packages
- **Nethereum.Wallet.UI.Components.Blazor** - Blazor components (Razor, CSS)
- **Nethereum.Wallet.UI.Components.Maui** - MAUI platform integration
- **Nethereum.Wallet.UI.Components.Avalonia** - Avalonia desktop UI

### Hardware Wallet Support
- **Nethereum.Wallet.UI.Components.Trezor** - Trezor ViewModels (platform-agnostic)
- **Nethereum.Wallet.UI.Components.Blazor.Trezor** - Trezor Blazor components

### Core Dependencies
- **Nethereum.Wallet** - Wallet accounts, vault, services
- **Nethereum.RPC** - RPC client functionality
- **Nethereum.DataServices** - Chain data services
- **Nethereum.UI** - Base UI abstractions

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
