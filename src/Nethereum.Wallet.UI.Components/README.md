# Nethereum.Wallet.UI.Components

Platform-agnostic wallet UI components, view models, and services for Nethereum applications. Provides MVVM-based ViewModels and business logic that can be consumed by any UI framework (Blazor, Avalonia, MAUI, etc.).

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components
```

## Target Framework

- net9.0

## Dependencies

### NuGet Packages
- CommunityToolkit.Mvvm 8.4.0 - MVVM framework
- Microsoft.AspNetCore.Components.WebAssembly 9.0.0 - Razor component support
- Microsoft.Extensions.Hosting.Abstractions 9.0.0 - Hosted service abstractions

### Nethereum Packages
- Nethereum.DataServices - Chain data and 4Byte directory services
- Nethereum.RPC - RPC client functionality
- Nethereum.Wallet - Core wallet types and services

Source: Nethereum.Wallet.UI.Components.csproj:16-26

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
    Type? GetComponentType<TViewModel>() where TViewModel : class;
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
}
```
Source: WalletAccounts/IAccountCreationRegistry.cs:6-14

Returns ViewModels filtered by `IsVisible` and ordered by `SortOrder`:
Source: WalletAccounts/AccountCreationRegistry.cs:32-34

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

Performs case-insensitive TypeName matching.
Source: WalletAccounts/AccountTypeMetadataRegistry.cs:24-25

#### 3. Dashboard Plugin System

**IDashboardPluginRegistry** - Manages dashboard plugins
```csharp
public interface IDashboardPluginRegistry
{
    IEnumerable<IDashboardPluginViewModel> GetAvailablePlugins();
    IDashboardPluginViewModel? GetPlugin(string pluginId);
    Type? GetComponentType(IDashboardPluginViewModel viewModel);
}
```
Source: Dashboard/IDashboardPluginRegistry.cs:6-12

Returns plugins filtered by `IsVisible && IsEnabled && IsAvailable()` and ordered by `SortOrder`.
Source: Dashboard/DashboardPluginRegistry.cs:26

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
Source: Dashboard/IDashboardPluginViewModel.cs:5-16

#### 4. Account Details Registry

**IAccountDetailsRegistry** - Maps account types to detail ViewModels
```csharp
public interface IAccountDetailsRegistry
{
    void Register<TViewModel, TComponent>()
        where TViewModel : class, IAccountDetailsViewModel
        where TComponent : class;
    Type? GetViewModelType(IWalletAccount account);
    Type? GetComponentType(Type viewModelType);
}
```
Source: AccountDetails/IAccountDetailsRegistry.cs:7-17

Iterates ViewModels calling `CanHandle(account)` to find appropriate ViewModel.
Source: AccountDetails/AccountDetailsRegistry.cs:53-60

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

Properties:
```csharp
string Mnemonic { get; set; }              // BIP-39 mnemonic phrase
string MnemonicLabel { get; set; }          // User-friendly name
string MnemonicPassphrase { get; set; }     // Optional BIP-39 passphrase
bool IsRevealed { get; set; }               // Show/hide mnemonic
bool IsBackedUp { get; set; }               // User confirmed backup
bool IsGenerateMode { get; set; }           // true = generate, false = import
int WordCount { get; set; }                 // 12 or 24 words
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:22-50

Commands:
- `GenerateMnemonicAsync()` - Generates 12-word mnemonic
- `GenerateMnemonic24Async()` - Generates 24-word mnemonic
- `ToggleRevealAsync()` - Show/hide mnemonic
- `ConfirmBackupAsync()` - Mark as backed up
- `SwitchToImportModeAsync()` / `SwitchToGenerateModeAsync()` - Toggle mode

Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:66-147

Mnemonic Validation:
```csharp
public (bool IsValid, string Message) ValidateMnemonic()
{
    if (string.IsNullOrWhiteSpace(Mnemonic))
        return (false, "Mnemonic phrase is required");

    var words = Mnemonic.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (words.Length != 12 && words.Length != 24)
        return (false, $"Mnemonic must be 12 or 24 words (found {words.Length})");

    try
    {
        var hdWallet = new MinimalHDWallet(Mnemonic, MnemonicPassphrase);
        var account = hdWallet.GetAccount(0);
        return (true, "Valid mnemonic phrase");
    }
    catch
    {
        return (false, "Invalid mnemonic phrase");
    }
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:180-210

Account Creation:
```csharp
public override IWalletAccount CreateAccount(WalletVault vault)
{
    var hdWallet = new MinimalHDWallet(Mnemonic, MnemonicPassphrase);
    var mnemonicAccount = new MnemonicWalletAccount
    {
        MnemonicId = Guid.NewGuid().ToString(),
        Name = MnemonicLabel,
        EncryptedMnemonic = vault.EncryptData(Mnemonic),
        EncryptedPassphrase = string.IsNullOrEmpty(MnemonicPassphrase)
            ? null
            : vault.EncryptData(MnemonicPassphrase),
        CreatedAt = DateTime.UtcNow
    };

    var account = hdWallet.GetAccount(0);
    var walletAccount = new MnemonicWalletAccountItem
    {
        Id = Guid.NewGuid().ToString(),
        Name = $"{MnemonicLabel} - Account 1",
        Address = account.Address,
        Index = 0,
        MnemonicId = mnemonicAccount.MnemonicId,
        // ...
    };

    return walletAccount;
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountCreationViewModel.cs:224-257

**MnemonicAccountDetailsViewModel** - View/manage HD wallet account details

Commands:
- `RemoveAccountAsync()` - Delete account with confirmation
- `SaveAccountNameAsync()` - Update account name
- `RevealPrivateKeyAsync(password)` - Show private key with password confirmation

Source: WalletAccounts/Mnemonic/MnemonicAccountDetailsViewModel.cs:88-254

Derivation Path:
```csharp
public string GetDerivationPath()
{
    var index = GetAccountIndex();
    return $"m/44'/60'/0'/0/{index}";  // BIP-44 Ethereum path
}
```
Source: WalletAccounts/Mnemonic/MnemonicAccountDetailsViewModel.cs:262-269

**VaultMnemonicAccountViewModel** - Create account from existing vault mnemonic

Form Steps:
```csharp
public enum FormStep
{
    SelectMnemonic,   // Choose from vault mnemonics
    Configure,        // Set account index
    Confirm           // Review and create
}
```
Source: WalletAccounts/Mnemonic/VaultMnemonicAccountViewModel.cs:28-33

Properties:
```csharp
string SelectedMnemonicId { get; set; }
int AccountIndex { get; set; }              // Derivation index
string DerivedAddress { get; set; }         // Preview address
List<MnemonicInfo> AvailableMnemonics { get; set; }
```
Source: WalletAccounts/Mnemonic/VaultMnemonicAccountViewModel.cs:35-40

**MnemonicListViewModel** - Manage vault mnemonics

Commands:
- `LoadMnemonicsAsync()` - Load all mnemonics from vault
- `DeleteMnemonicAsync(MnemonicItemViewModel)` - Delete mnemonic with validation

Source: WalletAccounts/Mnemonic/MnemonicListViewModel.cs:51-148

Validates that no accounts reference the mnemonic before deletion.
Source: WalletAccounts/Mnemonic/MnemonicListViewModel.cs:113-130

#### 2. Private Key

**PrivateKeyAccountCreationViewModel** - Import account from private key

Properties:
```csharp
string PrivateKey { get; set; }             // 64 hex characters
string Label { get; set; }                  // Account name
bool IsRevealed { get; set; }               // Show/hide private key
string DerivedAddress { get; set; }         // Calculated address
bool IsValidPrivateKey { get; set; }        // Validation result
string PrivateKeyFormat { get; set; }       // Format description
```
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:40-48

Validation:
```csharp
public (bool IsValid, string Message) ValidatePrivateKey()
{
    if (string.IsNullOrWhiteSpace(PrivateKey))
        return (false, "Private key is required");

    var cleanKey = CleanPrivateKey(PrivateKey);

    if (cleanKey.Length != 64)
        return (false, $"Private key must be 64 hexadecimal characters (found {cleanKey.Length})");

    if (!System.Text.RegularExpressions.Regex.IsMatch(cleanKey, "^[0-9a-fA-F]{64}$"))
        return (false, "Private key must contain only hexadecimal characters (0-9, a-f, A-F)");

    if (cleanKey.All(c => c == '0'))
        return (false, "Private key cannot be all zeros");

    try
    {
        var key = new EthECKey(cleanKey);
        DerivedAddress = key.GetPublicAddress();
        return (true, "Valid private key");
    }
    catch
    {
        return (false, "Invalid private key format");
    }
}
```
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:93-120

Cleans input by removing "0x" prefix.
Source: WalletAccounts/PrivateKey/PrivateKeyAccountCreationViewModel.cs:122-133

**PrivateKeyAccountDetailsViewModel** - View/manage private key account

Commands:
- `SaveAccountName()` - Update account name
- `RevealPrivateKey(password)` - Show private key with password protection
- `RemoveAccount()` - Delete account

Source: WalletAccounts/PrivateKey/PrivateKeyAccountDetailsViewModel.cs:82-231

#### 3. View-Only

**ViewOnlyAccountCreationViewModel** - Watch-only account

Properties:
```csharp
string ViewOnlyAddress { get; set; }        // Ethereum address to watch
string Label { get; set; }                  // Account name
```
Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:18-19

Validation:
- Address must start with "0x"
- Address must be 42 characters (20 bytes)

Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:32-34

Creates `ViewOnlyWalletAccount` with no private key operations.
Source: WalletAccounts/ViewOnly/ViewOnlyAccountCreationViewModel.cs:46

**ViewOnlyAccountDetailsViewModel** - Read-only account operations

No private key or signing operations available. Can only view balances and transaction history.
Source: WalletAccounts/ViewOnly/ViewOnlyAccountDetailsViewModel.cs:14-185

#### 4. Smart Contract (Account Abstraction)

**SmartContractAccountCreationViewModel** - ERC-4337 account abstraction

Properties:
```csharp
string Address { get; set; }                // Smart contract address
string Label { get; set; }                  // Account name
```
Source: WalletAccounts/SmartContract/SmartContractAccountCreationViewModel.cs:18-19

Supports ERC-4337, Safe, and Argent wallet contracts.
Source: WalletAccounts/SmartContract/SmartContractAccountMetadataViewModel.cs

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
Source: WalletAccounts/IAccountTypeMetadata.cs:3-13

**IAccountDetailsViewModel** interface:
```csharp
public interface IAccountDetailsViewModel
{
    string AccountType { get; }
    bool CanHandle(IWalletAccount account);
    Task InitializeAsync(IWalletAccount account);
}
```
Source: AccountDetails/IAccountDetailsViewModel.cs

Custom account types can be added by implementing these interfaces and registering via `IWalletUIRegistryContributor`.

## Network Management

### Add Custom Network

**AddCustomNetworkViewModel** - Configure custom EVM networks

Dependencies:
```csharp
private readonly IChainManagementService _chainManagement;
private readonly IRpcEndpointService _rpcEndpointService;
```
Source: Networks/AddCustomNetworkViewModel.cs:14-15

Properties:
```csharp
NetworkConfiguration Network { get; set; }
bool IsFormValid { get; }
```
Source: Networks/AddCustomNetworkViewModel.cs:17-25

Commands:
```csharp
SaveNetworkAsync()                           // Add/update network
TestRpcEndpointAsync(RpcEndpointInfo)       // Test RPC connectivity
AddRpcEndpoint()                             // Add RPC URL
RemoveRpcEndpoint(RpcEndpointInfo)          // Remove RPC URL
AddBlockExplorer()                           // Add explorer URL
RemoveBlockExplorer(BlockExplorerInfo)      // Remove explorer URL
```
Source: Networks/AddCustomNetworkViewModel.cs:38-141

**NetworkConfiguration** model:
```csharp
public class NetworkConfiguration
{
    public BigInteger ChainId { get; set; }
    public string ChainName { get; set; }
    public string CurrencySymbol { get; set; }
    public string CurrencyName { get; set; }
    public int CurrencyDecimals { get; set; }
    public List<RpcEndpointInfo> RpcEndpoints { get; set; }
    public List<BlockExplorerInfo> BlockExplorers { get; set; }
    public bool IsTestnet { get; set; }
    public string? IconUrl { get; set; }
}
```
Source: Networks/Models/NetworkConfiguration.cs

**RpcEndpointInfo** model:
```csharp
public class RpcEndpointInfo
{
    public string Url { get; set; }
    public bool IsWebSocket { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsHealthy { get; set; }
    public string? TestResult { get; set; }
    public bool IsTesting { get; set; }
    public bool IsCustom { get; set; }

    // Display properties
    public string TypeDisplayName => IsWebSocket ? "WebSocket" : "HTTP";
    public string StatusDisplayName => IsEnabled ? "Active" : "Inactive";
    public string HealthDisplayName => IsHealthy ? "Healthy" : "Unhealthy";
}
```
Source: Networks/Models/RpcEndpointInfo.cs:5-25

**NetworkManagementPluginViewModel** - Dashboard plugin for network management

Implements `IDashboardPluginViewModel`.
Source: Networks/NetworkManagementPluginViewModel.cs:12-36

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

**TokenNativeTransferModel** - Native cryptocurrency transfer model

Validation Attributes:
```csharp
[EthereumAddress]
public string RecipientAddress { get; set; }

[Required]
public string Amount { get; set; }

[Hex]
public string TransactionData { get; set; }
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:28-49

Properties:
```csharp
BigInteger AvailableBalance { get; set; }
string TokenSymbol { get; set; }            // e.g., "ETH"
int TokenDecimals { get; set; }             // e.g., 18
string Nonce { get; set; }
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:36-49

Methods:
```csharp
BigInteger GetTransferAmountInSmallestUnit()
{
    return UnitConversion.Convert.ToWei(decimal.Parse(Amount), TokenDecimals);
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:53-54

```csharp
public string FormattedAvailableBalance
{
    get
    {
        if (AvailableBalance == 0) return "0";

        var balance = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);

        // Format based on size
        if (balance >= 1000000m) return $"{balance / 1000000m:N2}M";
        if (balance >= 1000m) return $"{balance / 1000m:N2}K";
        if (balance >= 1m) return $"{balance:N4}";
        if (balance >= 0.0001m) return $"{balance:N6}";
        return $"{balance:N8}";
    }
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:56-74

```csharp
public (bool IsValid, string Message) ValidateAmountBalance()
{
    if (!decimal.TryParse(Amount, out var amount))
        return (false, "Invalid amount format");

    if (amount <= 0)
        return (false, "Amount must be greater than zero");

    var transferAmount = GetTransferAmountInSmallestUnit();
    if (transferAmount > AvailableBalance)
        return (false, $"Insufficient balance. Available: {FormattedAvailableBalance} {TokenSymbol}");

    return (true, string.Empty);
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:153-167

```csharp
public void SetMaxAmount()
{
    var maxBalance = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);
    Amount = maxBalance.ToString("F18").TrimEnd('0').TrimEnd('.');
}
```
Source: SendTransaction/Models/TokenNativeTransferModel.cs:181-188

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
}
```
Source: SendTransaction/Models/GasStrategyDisplay.cs:5-14

**GasMultiplierOption** - Pre-defined gas multipliers:
```csharp
public static GasMultiplierOption Economy = new()
{
    Multiplier = 0.8m,
    DisplayName = "Economy",
    Description = "Lower fees, slower confirmation",
    Icon = "savings"
};

public static GasMultiplierOption Standard = new()
{
    Multiplier = 1.0m,
    DisplayName = "Standard",
    Description = "Recommended for most transactions",
    Icon = "check_circle",
    IsRecommended = true
};

public static GasMultiplierOption Priority = new()
{
    Multiplier = 1.2m,
    DisplayName = "Priority",
    Description = "Higher fees, faster confirmation",
    Icon = "bolt"
};
```
Source: SendTransaction/Models/GasMultiplierOption.cs:11-34

### Transaction Monitoring

**TransactionMonitoringService** - Background transaction monitoring

Implements `IHostedService`:
```csharp
public class TransactionMonitoringService : IHostedService, IDisposable
{
    private readonly IPendingTransactionService _pendingTransactionService;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _pendingTransactionService.StartMonitoring();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pendingTransactionService.StopMonitoring();
    }
}
```
Source: Transactions/TransactionMonitoringService.cs:10-41

Registered via `AddTransactionServices()` extension.
Source: Transactions/TransactionServiceCollectionExtensions.cs:19

### Transaction Steps

**SendTransactionStep** enum:
```csharp
public enum SendTransactionStep
{
    Input,           // Enter recipient and amount
    Configure,       // Gas configuration
    Confirm,         // Review transaction
    Processing,      // Submitting transaction
    Complete         // Transaction submitted
}
```
Source: SendTransaction/Models/SendTransactionStep.cs

## Configuration System

### Base Configuration

**BaseWalletConfiguration** - Abstract base for component configuration

Properties:
```csharp
string ComponentId { get; set; }                         // Unique ID
WalletFlowMode FlowMode { get; set; }                   // Simple/Advanced/Custom
WalletTextConfiguration Text { get; set; }
WalletBehaviorConfiguration Behavior { get; set; }
WalletSecurityConfiguration Security { get; set; }
```
Source: Core/Configuration/BaseWalletConfiguration.cs:5-11

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
    public TBuilder UseSimpleFlow() { ... }
    public TBuilder UseAdvancedFlow() { ... }
    public TBuilder WithTitle(string title) { ... }
    public TBuilder EnableWalletReset(bool enable = true) { ... }
    public TBuilder WithMinPasswordLength(int length) { ... }
    public TBuilder ConfigureText(Action<WalletTextConfiguration> configure) { ... }
    public TBuilder ConfigureBehavior(Action<WalletBehaviorConfiguration> configure) { ... }
    public TBuilder ConfigureSecurity(Action<WalletSecurityConfiguration> configure) { ... }
    public TConfiguration Build() => _config;
}
```
Source: Core/Configuration/BaseWalletConfiguration.cs:97-168

### Component-Specific Configurations

**AccountListConfiguration**:
```csharp
public class AccountListConfiguration : BaseWalletConfiguration
{
    public bool ShowBalances { get; set; } = true;
    public bool AllowAccountDeletion { get; set; } = true;
    public bool AllowAccountEditing { get; set; } = true;
    public int AccountsPerPage { get; set; } = 10;
}
```
Source: AccountList/AccountListConfiguration.cs:5-13

**WalletOverviewConfiguration**:
```csharp
public class WalletOverviewConfiguration : BaseWalletConfiguration
{
    public bool ShowBalance { get; set; } = true;
    public bool ShowFiatBalance { get; set; } = false;
    public bool ShowQuickActions { get; set; } = true;
    public bool AutoRefreshBalance { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
}
```
Source: WalletOverview/WalletOverviewConfiguration.cs:5-14

**CreateAccountConfiguration**:
```csharp
public class CreateAccountConfiguration : BaseWalletConfiguration
{
    public bool ShowAdvancedOptions { get; set; } = false;
}
```
Source: CreateAccount/CreateAccountConfiguration.cs:5-11

**NethereumWalletConfiguration**:
```csharp
public class NethereumWalletConfiguration : BaseWalletConfiguration
{
    public bool ShowProgressIndicators { get; set; } = true;
    public bool EnableKeyboardShortcuts { get; set; } = true;
    public int PasswordMinimumStrength { get; set; } = 1;
    public bool AllowPasswordVisibilityToggle { get; set; } = true;
    public bool ShowPasswordStrengthIndicator { get; set; } = true;
}
```
Source: NethereumWallet/NethereumWalletConfiguration.cs:7-15

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

Translation Lookup Algorithm:
1. Try exact culture match (e.g., "es-ES")
2. Try language default mapping (e.g., "es" → "es-ES")
3. Fallback to default language
4. Return key if not found

Source: Core/Localization/WalletLocalizationService.cs:98-129

### Storage Providers

**ILocalizationStorageProvider** - Platform abstraction:
```csharp
public interface ILocalizationStorageProvider
{
    Task<string?> GetStoredLanguageAsync();
    Task SetStoredLanguageAsync(string languageCode);
    Task<string?> GetSystemLanguageAsync();
}
```
Source: Core/Localization/ILocalizationStorageProvider.cs:5-10

**BrowserLocalizationStorageProvider** - Browser localStorage:
```csharp
public class BrowserLocalizationStorageProvider : ILocalizationStorageProvider
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "wallet-ui-language";

    public async Task<string?> GetStoredLanguageAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
    }

    public async Task SetStoredLanguageAsync(string languageCode)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, languageCode);
    }

    public async Task<string?> GetSystemLanguageAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("eval", "navigator.language || navigator.userLanguage");
    }
}
```
Source: Core/Localization/BrowserLocalizationStorageProvider.cs:7-51

**SystemLocalizationStorageProvider** - File-based storage:
```csharp
public class SystemLocalizationStorageProvider : ILocalizationStorageProvider
{
    private string GetLanguageFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var walletPath = Path.Combine(appDataPath, "NethereumWallet");
        Directory.CreateDirectory(walletPath);
        return Path.Combine(walletPath, "language.txt");
    }

    public async Task<string?> GetSystemLanguageAsync()
    {
        return CultureInfo.CurrentUICulture.Name;
    }
}
```
Source: Core/Localization/SystemLocalizationStorageProvider.cs:8-53

### Component Localizer

**ComponentLocalizerBase<T>** - Type-safe localization:
```csharp
public abstract class ComponentLocalizerBase<T>
{
    protected readonly IWalletLocalizationService LocalizationService;

    protected ComponentLocalizerBase(IWalletLocalizationService localizationService)
    {
        LocalizationService = localizationService;
        RegisterTranslations();
    }

    public string GetString(string key)
    {
        return LocalizationService.GetTranslation(typeof(T).Name, LocalizationService.CurrentLanguage, key);
    }

    public string GetString(string key, params object[] args)
    {
        return LocalizationService.GetTranslation(typeof(T).Name, LocalizationService.CurrentLanguage, key, args);
    }

    protected abstract void RegisterTranslations();
}
```
Source: Core/Localization/ComponentLocalizerBase.cs:5-28

## Dashboard Navigation

**IDashboardNavigationService** - Plugin navigation
```csharp
public interface IDashboardNavigationService
{
    event NavigationRequestedHandler? NavigationRequested;
    string? CurrentPluginId { get; }
    object? CurrentPluginComponent { get; }

    Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null);
    Task NavigateCurrentPluginAsync(Dictionary<string, object> parameters);
    void RegisterActivePlugin(string pluginId, object? pluginComponent);
}
```
Source: Dashboard/Services/IDashboardNavigationService.cs:6-26

**DashboardNavigationService** implementation:
```csharp
public async Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null)
{
    CurrentPluginId = pluginId;
    NavigationRequested?.Invoke(pluginId, parameters ?? new Dictionary<string, object>());
}
```
Source: Dashboard/Services/DashboardNavigationService.cs:13-30

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

    Task ShowPromptAsync(PromptRequest request);
    Task ShowPromptByIdAsync(string promptId);
    Task ShowNextPromptAsync();
    Task ShowPreviousPromptAsync();
    void HideOverlay();
    void MinimizeOverlay();

    event EventHandler<OverlayStateChangedEventArgs>? OverlayStateChanged;
}
```
Source: Services/IPromptOverlayService.cs:6-27

**PromptOverlayService** implementation:

Dependencies: `IPromptQueueService`
Source: Services/PromptOverlayService.cs:10

Commands:
```csharp
public async Task ShowPromptAsync(PromptRequest request)
{
    CurrentPrompt = request;
    IsOverlayVisible = true;

    if (request.Status == PromptStatus.Pending)
    {
        request.Status = PromptStatus.InProgress;
    }

    OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs(true, CurrentPrompt));
}
```
Source: Services/PromptOverlayService.cs:23-40

```csharp
public void HideOverlay()
{
    IsOverlayVisible = false;
    var previousPrompt = CurrentPrompt;
    CurrentPrompt = null;
    CurrentIndex = 0;

    OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs(false, previousPrompt));
}
```
Source: Services/PromptOverlayService.cs:76-86

```csharp
public void MinimizeOverlay()
{
    IsOverlayVisible = false;
    // CurrentPrompt remains set

    OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs(false, CurrentPrompt));
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
    void ShowNotificationWithAction(string message, NotificationSeverity severity, Action action);
}

public enum NotificationSeverity
{
    Success,
    Info,
    Warning,
    Error
}
```
Source: Abstractions/GenericInterfaces.cs:8-33

**IWalletDialogService** - Modal dialogs
```csharp
public interface IWalletDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task<T?> ShowDialogAsync<T>(Dictionary<string, object> parameters);
    Task<bool> ShowWarningConfirmationAsync(string title, string message, string confirmText = "Confirm");
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
        if (string.IsNullOrEmpty(address) || address.Length < 4)
            return "??";
        return address.Substring(2, 2).ToUpper();
    }

    public static string GetNetworkIdenticonText(string networkName)
    {
        if (string.IsNullOrEmpty(networkName))
            return "??";

        var words = networkName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return $"{words[0][0]}{words[1][0]}".ToUpper();

        return networkName.Length >= 2
            ? networkName.Substring(0, 2).ToUpper()
            : networkName.ToUpper();
    }

    public static string GetIdenticonStyle(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "background: #cccccc;";

        var hash = address.GetHashCode();
        var hue = Math.Abs(hash % 360);
        var saturation = 65 + Math.Abs(hash % 20);
        var lightness = 45 + Math.Abs(hash % 20);

        return $"background: hsl({hue}, {saturation}%, {lightness}%);";
    }
}
```
Source: Utils/IdenticonGenerator.cs:5-47

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
    services.AddSingleton<PendingTransactionServiceLocalizer>();
    services.AddSingleton<PendingTransactionNotificationLocalizer>();

    services.AddHostedService<TransactionMonitoringService>();

    return services;
}
```
Source: Transactions/TransactionServiceCollectionExtensions.cs:10-24

### Token Transfer Services

**TokenTransferServiceCollectionExtensions**:
```csharp
public static IServiceCollection AddTokenTransferServices(this IServiceCollection services)
{
    services.AddSingleton<FourByteDirectoryService>();
    services.AddScoped<ITransactionDataDecodingService, TransactionDataDecodingService>();

    services.AddScoped<IGasPriceProvider, DefaultGasPriceProvider>();
    services.AddScoped<IGasConfigurationPersistenceService, InMemoryGasConfigurationPersistence>();

    services.AddTransient<TokenNativeTransferModel>();
    services.AddTransient<TokenERC20TransferModel>();
    services.AddTransient<TokenERC721TransferModel>();
    services.AddTransient<TokenERC1155TransferModel>();
    services.AddTransient<NativeTokenTransferViewModel>();

    // Localizers
    services.AddSingleton<SendNativeTokenLocalizer>();
    services.AddSingleton<SendERC20TokenLocalizer>();
    services.AddSingleton<SendERC721TokenLocalizer>();
    services.AddSingleton<SendERC1155TokenLocalizer>();
    services.AddSingleton<TokenTransferLocalizer>();
    services.AddSingleton<TransactionInputLocalizer>();
    services.AddSingleton<TransactionConfirmationLocalizer>();
    services.AddSingleton<TransactionStatusLocalizer>();

    return services;
}
```
Source: SendTransaction/TokenTransferServiceCollectionExtensions.cs:14-59

## Related Packages

### Platform-Specific UI Packages
- **Nethereum.Wallet.UI.Components.Blazor** - Blazor components (Razor, CSS)
- **Nethereum.Wallet.UI.Components.Maui** - MAUI platform integration
- **Nethereum.Wallet.UI.Components.Avalonia** - Avalonia desktop UI (skipped per user request)

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
