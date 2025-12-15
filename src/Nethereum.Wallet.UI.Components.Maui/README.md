# Nethereum.Wallet.UI.Components.Maui

Cross-platform MAUI integration package providing platform-specific storage implementations and Blazor WebView hosting for Nethereum wallet components on mobile and desktop applications.

## Package Information

- **Target Frameworks**: net9.0-android, net9.0-ios, net9.0-maccatalyst, net9.0-windows10.0.19041.0
- **Platform**: .NET MAUI (Multi-platform App UI)
- **UI Technology**: Blazor WebView (hybrid web/native)
- **Package ID**: Nethereum.Wallet.UI.Components.Maui

## Supported Platforms

- **Android** - Android 5.0 (API 21) and higher
- **iOS** - iOS 11.0 and higher
- **macOS** - macOS 10.15 and higher (via Mac Catalyst)
- **Windows** - Windows 10 version 1903 (build 19041) and higher

## Dependencies

**NuGet Packages:**
- `Microsoft.AspNetCore.Components.WebView.Maui` - Blazor WebView hosting
- `Microsoft.Extensions.Logging.Debug` 9.0.0 - Debug logging
- `Microsoft.Maui.Controls` - MAUI framework

**Project References:**
- `Nethereum.Wallet.RpcRequests` - RPC request handlers
- `Nethereum.Wallet.UI.Components` - Platform-agnostic UI components
- `Nethereum.Wallet` - Core wallet services

**Note:** This package does NOT reference `Nethereum.Wallet.UI.Components.Blazor` directly. Your application must add both packages to use Blazor wallet components in MAUI.

## Installation

```bash
# Install MAUI storage services
dotnet add package Nethereum.Wallet.UI.Components.Maui

# Install Blazor UI components separately
dotnet add package Nethereum.Wallet.UI.Components.Blazor
```

## Quick Start

### 1. Configure MauiProgram.cs

```csharp
using Nethereum.Wallet.UI.Components.Maui.Extensions;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;

public static class MauiProgram
{
    public static MauiApp CreateMauiProgram()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register MAUI wallet services (storage, vault, initialization)
        builder.AddNethereumWalletMauiComponents(options =>
        {
            options.DefaultChainId = 1; // Ethereum Mainnet
            options.ConfigureUi = config =>
            {
                config.ApplicationName = "My Crypto Wallet";
                config.LogoPath = "/logo.png";
            };
        });

        // Register Blazor wallet UI components
        builder.Services.AddNethereumWalletUI();

        return builder.Build();
    }
}
```

### 2. Create Blazor WebView Page

**MainPage.xaml:**
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:blazor="clr-namespace:Microsoft.AspNetCore.Components.WebView.Maui;assembly=Microsoft.AspNetCore.Components.WebView.Maui"
             x:Class="MyApp.MainPage">

    <blazor:BlazorWebView HostPage="wwwroot/index.html">
        <blazor:BlazorWebView.RootComponents>
            <blazor:RootComponent Selector="#app" ComponentType="{x:Type local:Main}" />
        </blazor:BlazorWebView.RootComponents>
    </blazor:BlazorWebView>

</ContentPage>
```

### 3. Create Blazor Component

**Components/Main.razor:**
```razor
@using Nethereum.Wallet.UI.Components.Blazor.NethereumWallet

<NethereumWallet OnConnected="@HandleConnected"
                 Width="100%"
                 Height="100%" />

@code {
    private void HandleConnected()
    {
        Console.WriteLine("Wallet connected!");
    }
}
```

### 4. Create index.html

**wwwroot/index.html:**
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My Crypto Wallet</title>
    <base href="/" />

    <!-- MudBlazor CSS -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
</head>
<body>
    <div id="app">Loading...</div>

    <!-- Blazor framework script (provided by WebView) -->
    <script src="_framework/blazor.webview.js" autostart="false"></script>

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

## Architecture

### Platform Services

The package provides two core service implementations using MAUI APIs:

#### MauiSecureStorageWalletVaultService

Implements `IWalletVaultService` using MAUI SecureStorage for encrypted vault persistence.

**Location**: `Services/MauiSecureStorageWalletVaultService.cs:7-41`

**Platform-Specific Security:**
- **Android**: KeyStore-backed encryption (hardware-backed when available)
- **iOS/macOS**: Keychain services with access control
- **Windows**: Windows Credential Locker

**Storage Key**: `wallet.vault`

**Methods:**

```csharp
// Check if encrypted vault exists in secure storage
public override async Task<bool> VaultExistsAsync()
```
Lines 11-15

```csharp
// Retrieve encrypted vault data from secure storage
protected override Task<string?> GetEncryptedAsync()
```
Lines 17-20

```csharp
// Save encrypted vault data to secure storage
protected override Task SaveEncryptedAsync(string encrypted)
```
Lines 22-25

```csharp
// Remove vault from secure storage (with error handling)
protected override Task ResetStorageAsync()
```
Lines 27-39

Exception handling in `ResetStorageAsync` prevents platform-specific errors from crashing logout flows.

**Inherited Methods** (from `WalletVaultServiceBase`):
- `UnlockAsync(password)` - Decrypt and load vault into memory
- `CreateNewAsync(password)` - Create new encrypted vault
- `GetAccountsAsync()` - Get all accounts from vault
- `SaveAsync(password)` - Encrypt and save vault
- `LockAsync()` - Clear in-memory vault data
- `ResetAsync()` - Delete vault completely

#### MauiPreferencesWalletStorageService

Implements `IWalletStorageService` using MAUI Preferences for persistent storage of wallet configuration, networks, transactions, and DApp permissions.

**Location**: `Services/MauiPreferencesWalletStorageService.cs:18-441`

**Storage Keys:**

```csharp
private const string UserNetworksKey = "wallet.userNetworks";
private const string SelectedNetworkKey = "wallet.selectedNetwork";
private const string SelectedAccountKey = "wallet.selectedAccount";
private const string TransactionsPrefix = "wallet.transactions.";
private const string RpcSelectionPrefix = "wallet.rpcSelection.";
private const string ActiveRpcsPrefix = "wallet.activeRpcs.";
private const string CustomRpcsPrefix = "wallet.customRpcs.";
private const string RpcHealthPrefix = "wallet.rpcHealth.";
private const string DappPermissionsIndexKey = "wallet.dappPermissions.index";
private const string DappPermissionsPrefix = "wallet.dappPermissions.";
private const string NetworkPreferencePrefix = "wallet.networkPref.";
```
Lines 20-30

**JSON Serialization Configuration:**

```csharp
public MauiPreferencesWalletStorageService()
{
    _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    _jsonOptions.Converters.Add(new BigIntegerConverter());
    _jsonOptions.Converters.Add(new JsonStringEnumConverter());
}
```
Lines 34-43

**Key Methods:**

**Network Management** (Lines 45-84):
```csharp
Task<List<ChainFeature>> GetUserNetworksAsync()
Task SaveUserNetworksAsync(List<ChainFeature> networks)
Task SaveUserNetworkAsync(ChainFeature network)  // Upsert single network
Task DeleteUserNetworkAsync(BigInteger chainId)
Task<bool> UserNetworksExistAsync()
Task ClearUserNetworksAsync()
```

**RPC Management** (Lines 86-138):
```csharp
Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo)
Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl)
Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
Task RemoveRpcAsync(BigInteger chainId, string rpcUrl)
Task<List<string>> GetCustomRpcsAsync(BigInteger chainId)
Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl)
Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl)
```

RPC health cache key encoding (Lines 401-405):
```csharp
private static string GetRpcHealthKey(string rpcUrl)
{
    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(rpcUrl));
    return RpcHealthPrefix + encoded;
}
```

**Account Selection** (Lines 140-172):
```csharp
Task SetSelectedNetworkAsync(long chainId)
Task<long?> GetSelectedNetworkAsync()
Task SetSelectedAccountAsync(string accountAddress)
Task<string?> GetSelectedAccountAsync()
```

**RPC Configuration** (Lines 174-182):
```csharp
Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId)
Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config)
```

**Transaction Management** (Lines 184-244):
```csharp
Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction)
Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status)
Task<TransactionInfo?> GetTransactionByHashAsync(BigInteger chainId, string hash)
Task DeleteTransactionAsync(BigInteger chainId, string hash)
Task ClearTransactionsAsync(BigInteger chainId)
```

Transaction status updates automatically set `ConfirmedAt` timestamp (Lines 217-220):
```csharp
if (status == TransactionStatus.Confirmed && !existing.ConfirmedAt.HasValue)
{
    existing.ConfirmedAt = DateTime.UtcNow;
}
```

**DApp Permissions** (Lines 246-295):
```csharp
Task<List<DappPermission>> GetDappPermissionsAsync(string? accountAddress = null)
Task AddDappPermissionAsync(string accountAddress, string origin)
Task RemoveDappPermissionAsync(string accountAddress, string origin)
```

Permissions are normalized for consistent matching (Lines 395-399):
```csharp
private static string NormalizeAccount(string account)
    => account.Trim().ToLowerInvariant();

private static string NormalizeOrigin(string origin)
    => origin.Trim().ToLowerInvariant();
```

Permission indexing enables efficient "all permissions" queries (Lines 327-336):
```csharp
private void EnsureAccountIndexed(string accountAddress)
{
    var index = GetStringList(DappPermissionsIndexKey);
    var normalized = NormalizeAccount(accountAddress);
    if (!index.Contains(normalized, StringComparer.OrdinalIgnoreCase))
    {
        index.Add(normalized);
        SetStringList(DappPermissionsIndexKey, index);
    }
}
```

**Network Preferences** (Lines 297-312):
```csharp
Task SaveNetworkPreferenceAsync(string key, bool value)
Task<bool?> GetNetworkPreferenceAsync(string key)
```

**BigInteger JSON Converter** (Lines 407-439):
```csharp
private sealed class BigIntegerConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (BigInteger.TryParse(value, out var result))
            {
                return result;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var longValue))
            {
                return new BigInteger(longValue);
            }
        }
        throw new JsonException("Unable to convert value to BigInteger");
    }

    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
```

Handles BigInteger serialization as strings to prevent precision loss.

### Configuration

#### MauiWalletComponentOptions

Configuration options for MAUI wallet initialization.

**Location**: `Options/MauiWalletComponentOptions.cs:5-10`

```csharp
public class MauiWalletComponentOptions
{
    public long DefaultChainId { get; set; } = 1; // Ethereum Mainnet

    public Action<NethereumWalletUIConfiguration>? ConfigureUi { get; set; }
}
```

**Usage:**
```csharp
builder.AddNethereumWalletMauiComponents(options =>
{
    options.DefaultChainId = 137; // Polygon
    options.ConfigureUi = config =>
    {
        config.ApplicationName = "My Wallet";
        config.LogoPath = "/logo.png";
        config.WalletConfig.Security.MinPasswordLength = 12;
    };
});
```

### Service Registration

#### AddNethereumWalletMauiComponents Extension Method

Main registration method for MAUI wallet services.

**Location**: `Extensions/MauiAppBuilderExtensions.cs:50-151`

**Signature:**
```csharp
public static MauiAppBuilder AddNethereumWalletMauiComponents(
    this MauiAppBuilder builder,
    Action<MauiWalletComponentOptions>? configure = null)
```

**Registered Services:**

**Storage Services** (Lines 57-61):
```csharp
IEncryptionStrategy -> BouncyCastleAes256EncryptionStrategy (Singleton)
IWalletVaultService -> MauiSecureStorageWalletVaultService (Singleton)
IWalletStorageService -> MauiPreferencesWalletStorageService (Singleton)
IWalletConfigurationService -> InMemoryWalletConfigurationService (Singleton)
IDappPermissionService -> DefaultDappPermissionService (Singleton)
```

**Chain Management** (Lines 63-112):
```csharp
IChainFeaturesService -> ChainFeaturesService.Current (Singleton)
```

Configuration (Lines 65-112):
- `EnableExternalChainList: true` - Fetch chains from external sources
- `Strategy: PreconfiguredEnrich` - Merge external with preconfigured chains
- `PostProcessPreconfigured` - Adds localhost (1337) and enriches with default RPCs

**RPC Services** (Lines 114-117):
```csharp
IRpcEndpointService -> RpcEndpointService (Singleton)
IRpcClientFactory -> RpcClientFactory (Scoped)
```

**Wallet Services** (Lines 117-127):
```csharp
services.AddWalletPromptServices(ServiceLifetime.Singleton)
services.AddNethereumWalletServicesSingleton(options.DefaultChainId)

ICoreWalletAccountService -> Factory-created from vault (Singleton)
```

**UI Services** (Lines 128-148):
```csharp
INethereumWalletUIConfiguration -> Configured singleton
IDashboardNavigationService -> DashboardNavigationService (Scoped)
```

**MAUI Integration** (Lines 147-148):
```csharp
services.AddMauiBlazorWebView()
IMauiInitializeService -> WalletRegistryInitializer (Singleton)
```

**Default UI Configuration** (Lines 128-143):
```csharp
ApplicationName = "Nethereum Wallet"
ShowApplicationName = true
ShowLogo = true
LogoPath = "/nethereum-logo.png"
WelcomeLogoPath = "/nethereum-logo-large.png"
DrawerBehavior = DrawerBehavior.Responsive
ResponsiveBreakpoint = 1024
SidebarWidth = 200
WalletConfig.Security.MinPasswordLength = 8
WalletConfig.Behavior.EnableWalletReset = true
WalletConfig.AllowPasswordVisibilityToggle = true
```

User-provided configuration via `options.ConfigureUi` overrides defaults.

### Default RPC Seeds

The package includes fallback RPC endpoints for 16 networks.

**Location**: `Extensions/MauiAppBuilderExtensions.cs:28-46`

```csharp
private static readonly Dictionary<long, string[]> DefaultRpcSeed = new()
{
    { 1,        new[]{ "https://rpc.mevblocker.io", "https://eth.llamarpc.com" } },
    { 10,       new[]{ "https://mainnet.optimism.io" } },
    { 56,       new[]{ "https://bsc-dataseed.binance.org" } },
    { 100,      new[]{ "https://rpc.gnosischain.com" } },
    { 137,      new[]{ "https://polygon-rpc.com" } },
    { 324,      new[]{ "https://mainnet.era.zksync.io" } },
    { 42161,    new[]{ "https://arb1.arbitrum.io/rpc" } },
    { 42220,    new[]{ "https://forno.celo.org" } },
    { 43114,    new[]{ "https://api.avax.network/ext/bc/C/rpc" } },
    { 59144,    new[]{ "https://linea-mainnet.infura.io/v3/" } },
    { 8453,     new[]{ "https://mainnet.base.org" } },
    { 84532,    new[]{ "https://sepolia.base.org" } },
    { 11155111, new[]{ "https://rpc.sepolia.org" } },
    { 11155420, new[]{ "https://optimism-sepolia.blockpi.network/v1/rpc/public" } },
    { 1337,     new[]{ "http://localhost:8545" } },
    { 421614,   new[]{ "https://sepolia-rollup.arbitrum.io/rpc" } }
};
```

**Supported Networks:**
- Ethereum Mainnet (1)
- Optimism (10)
- BSC (56)
- Gnosis (100)
- Polygon (137)
- zkSync Era (324)
- Arbitrum One (42161)
- Celo (42220)
- Avalanche C-Chain (43114)
- Linea (59144)
- Base (8453)
- Base Sepolia (84532)
- Sepolia (11155111)
- Optimism Sepolia (11155420)
- Localhost (1337)
- Arbitrum Sepolia (421614)

These RPCs are automatically added to networks during initialization if no RPCs are configured.

### Automatic Initialization

#### WalletRegistryInitializer

Internal class implementing `IMauiInitializeService` to initialize wallet on app startup.

**Location**: `Extensions/MauiAppBuilderExtensions.cs:156-308`

**Initialization Steps:**

**1. RPC Handler Registration** (Lines 160-164):
```csharp
var registry = services.GetService<RpcHandlerRegistry>();
if (registry != null)
{
    WalletRpcHandlerRegistration.RegisterAll(registry);
}
```

Registers all wallet RPC request handlers for EIP-1193 compatibility.

**2. Default Network Preloading** (Lines 179-195):
```csharp
var defaultChainIds = new long[] { 1, 10, 100, 137, 56, 42161, 42220, 43114, 59144, 8453, 1337 };

foreach (var chainId in defaultChainIds)
{
    try
    {
        var feature = await chainManagement.GetChainAsync(new BigInteger(chainId));
        if (feature != null)
        {
            await configuration.AddOrUpdateChainAsync(feature);
        }
    }
    catch
    {
        // Ignore failures when preloading optional networks
    }
}
```

Preloads 11 popular networks into `IWalletConfigurationService` for immediate availability.

**3. User Network Storage Initialization** (Lines 197-258):

If no user networks exist, seeds storage with default networks (Lines 206-236):
```csharp
if (networks.Count == 0)
{
    foreach (var chainId in defaultChainIds)
    {
        try
        {
            var seedFeature = await chainManagement.GetChainAsync(new BigInteger(chainId));
            if (seedFeature == null) continue;

            // Enrich with default RPCs if missing
            if (seedFeature.HttpRpcs == null || seedFeature.HttpRpcs.Count == 0)
            {
                if (MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue(chainId, out var seedUrls))
                {
                    seedFeature.HttpRpcs = seedUrls.ToList();
                }
            }

            await storage.SaveUserNetworkAsync(seedFeature);
            updated = true;
        }
        catch { /* Ignore seeding failures */ }
    }
}
```

Enriches existing networks with default RPCs if missing (Lines 237-247):
```csharp
foreach (var network in networks)
{
    var chainId = (long)network.ChainId;
    if ((network.HttpRpcs == null || network.HttpRpcs.Count == 0) &&
        MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue(chainId, out var urls))
    {
        network.HttpRpcs = urls.ToList();
        await storage.SaveUserNetworkAsync(network);
        updated = true;
    }
}
```

**4. RPC Configuration Seeding** (Lines 260-295):

Creates default `RpcSelectionConfiguration` for networks without RPC configuration:
```csharp
foreach (var network in networks)
{
    try
    {
        var chainId = network.ChainId;
        var config = await rpcEndpointService.GetConfigurationAsync(chainId);
        if (config == null || config.SelectedRpcUrls == null || config.SelectedRpcUrls.Count == 0)
        {
            // Get candidate URLs from network or seed
            var candidateUrls = (network.HttpRpcs ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            if (candidateUrls.Count == 0 &&
                MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue((long)chainId, out var seedUrls))
            {
                candidateUrls = seedUrls.ToList();
            }

            if (candidateUrls.Count > 0)
            {
                var rpcConfig = new RpcSelectionConfiguration
                {
                    ChainId = chainId,
                    Mode = RpcSelectionMode.Single,
                    SelectedRpcUrls = new List<string> { candidateUrls[0] },
                    LastModified = DateTime.UtcNow
                };

                await rpcEndpointService.SaveConfigurationAsync(rpcConfig);
            }
        }
    }
    catch { /* Ignore configuration seeding failures */ }
}
```

**5. Active Chain Selection** (Lines 297-305):
```csharp
var activeChainId = options?.DefaultChainId ?? 1;
try
{
    await configuration.SetActiveChainAsync(new BigInteger(activeChainId));
}
catch { /* Ignore failures */ }
```

**Error Handling:**

All initialization steps are wrapped in try-catch blocks to prevent app crashes. Failures are logged but don't stop initialization. The wallet UI can prompt users to configure missing settings.

**Background Execution:**

Initialization runs in `Task.Run` to avoid blocking app startup (Line 177):
```csharp
_ = Task.Run(async () =>
{
    // Initialization code
});
```

## Platform-Specific Considerations

### Android

**SecureStorage:**
- Uses Android KeyStore for encryption key storage
- Hardware-backed encryption on supported devices
- Falls back to software KeyStore on older devices

**Preferences:**
- Stored in `SharedPreferences`
- Automatically backed up if app backup is enabled

**Permissions:**
- No special permissions required for storage

### iOS/macOS

**SecureStorage:**
- Uses iOS/macOS Keychain
- `kSecAttrAccessibleWhenUnlocked` access control
- Syncs across devices if iCloud Keychain is enabled

**Preferences:**
- Stored in `NSUserDefaults`
- Automatically backed up via iCloud

**Privacy:**
- No App Tracking Transparency (ATT) permission required

### Windows

**SecureStorage:**
- Uses Windows Credential Locker (PasswordVault)
- User-specific credential storage
- Encrypted at rest

**Preferences:**
- Stored in application local storage
- Roaming settings not used (privacy consideration)

## Security

### Vault Encryption

**Encryption Algorithm**: AES-256 (BouncyCastle implementation)
**Key Derivation**: PBKDF2 from user password
**Storage**: Platform secure storage (KeyStore/Keychain/Credential Locker)

**Security Flow:**
1. User provides password
2. PBKDF2 derives encryption key
3. Vault JSON serialized and encrypted with AES-256
4. Encrypted blob stored in platform secure storage
5. Secure storage manages key protection

### DApp Permissions

DApp permissions are stored in **unencrypted** Preferences (not sensitive data):
- Origin normalization prevents case-sensitivity issues
- Timestamp tracking for permission grant time
- Per-account permission isolation

**Recommendation:** Regularly review and revoke unused DApp permissions.

### Network Preferences

Network and RPC configurations are stored in **unencrypted** Preferences (not sensitive data).

## Complete Example: MAUI Crypto Wallet

### Project Structure

```
MyMauiWallet/
├── MauiProgram.cs
├── App.xaml
├── App.xaml.cs
├── MainPage.xaml
├── MainPage.xaml.cs
├── Components/
│   ├── Main.razor
│   └── _Imports.razor
└── wwwroot/
    ├── index.html
    ├── logo.png
    └── css/
        └── app.css
```

### MauiProgram.cs

```csharp
using Microsoft.Extensions.Logging;
using Nethereum.Wallet.UI.Components.Maui.Extensions;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using MudBlazor.Services;

namespace MyMauiWallet;

public static class MauiProgram
{
    public static MauiApp CreateMauiProgram()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure MAUI wallet services
        builder.AddNethereumWalletMauiComponents(options =>
        {
            options.DefaultChainId = 1; // Ethereum Mainnet
            options.ConfigureUi = config =>
            {
                config.ApplicationName = "My Crypto Wallet";
                config.LogoPath = "/logo.png";
                config.WelcomeLogoPath = "/logo.png";
                config.ShowLogo = true;
                config.ShowApplicationName = true;
                config.DrawerBehavior = DrawerBehavior.Responsive;
                config.ResponsiveBreakpoint = 768;
                config.WalletConfig.Security.MinPasswordLength = 10;
                config.WalletConfig.Behavior.EnableWalletReset = true;
            };
        });

        // Register Blazor wallet UI components
        builder.Services.AddNethereumWalletUI();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

### MainPage.xaml

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:blazor="clr-namespace:Microsoft.AspNetCore.Components.WebView.Maui;assembly=Microsoft.AspNetCore.Components.WebView.Maui"
             xmlns:local="clr-namespace:MyMauiWallet"
             x:Class="MyMauiWallet.MainPage"
             Shell.NavBarIsVisible="False">

    <blazor:BlazorWebView HostPage="wwwroot/index.html">
        <blazor:BlazorWebView.RootComponents>
            <blazor:RootComponent Selector="#app" ComponentType="{x:Type local:Components.Main}" />
        </blazor:BlazorWebView.RootComponents>
    </blazor:BlazorWebView>

</ContentPage>
```

### Components/Main.razor

```razor
@using Nethereum.Wallet.UI.Components.Blazor.NethereumWallet

<div style="height: 100vh; width: 100vw;">
    <NethereumWallet OnConnected="@HandleWalletConnected"
                     Width="100%"
                     Height="100%" />
</div>

@code {
    private void HandleWalletConnected()
    {
        // Wallet is unlocked and ready
        Console.WriteLine("Wallet connected successfully!");
    }
}
```

### Components/_Imports.razor

```razor
@using Microsoft.AspNetCore.Components.Web
@using MudBlazor
@using Nethereum.Wallet.UI.Components.Blazor
```

### wwwroot/index.html

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
    <title>My Crypto Wallet</title>
    <base href="/" />

    <!-- MudBlazor fonts and icons -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="https://use.fontawesome.com/releases/v6.3.0/css/all.css" rel="stylesheet" />

    <!-- MudBlazor CSS -->
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

    <!-- App CSS -->
    <link href="css/app.css" rel="stylesheet" />
</head>
<body>
    <div id="app">
        <div style="display: flex; align-items: center; justify-content: center; height: 100vh;">
            <div style="text-align: center;">
                <h2>Loading Wallet...</h2>
                <p>Please wait</p>
            </div>
        </div>
    </div>

    <!-- Blazor WebView framework script -->
    <script src="_framework/blazor.webview.js" autostart="false"></script>

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

### wwwroot/css/app.css

```css
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

html, body {
    height: 100%;
    width: 100%;
    overflow: hidden;
    font-family: 'Roboto', sans-serif;
}

#app {
    height: 100%;
    width: 100%;
}
```

## Testing on Platforms

### Android

```bash
# Build and deploy to Android emulator
dotnet build -f net9.0-android
dotnet run -f net9.0-android
```

### iOS

```bash
# Build and deploy to iOS simulator (Mac only)
dotnet build -f net9.0-ios
dotnet run -f net9.0-ios
```

### Windows

```bash
# Build and run on Windows
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0
```

## Troubleshooting

### SecureStorage Errors

**Problem**: `PlatformNotSupportedException` on Windows when using SecureStorage

**Solution**: Ensure targeting Windows 10 SDK 10.0.19041.0 or higher:
```xml
<TargetFrameworks>net9.0-windows10.0.19041.0</TargetFrameworks>
```

### Blazor WebView Not Loading

**Problem**: Blank screen or "Loading..." never completes

**Solutions:**
1. Verify `index.html` is in `wwwroot/` directory
2. Check `<base href="/" />` is present in HTML
3. Ensure `blazor.webview.js` script is included
4. Check browser console for errors (Developer Tools)

### MudBlazor Styles Not Loading

**Problem**: Wallet UI appears unstyled

**Solution**: Ensure MudBlazor CSS and JS are referenced in `index.html`:
```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

### Storage Permissions

**Problem**: `UnauthorizedAccessException` when accessing storage

**Solution**: Add permissions to platform-specific manifests:

**Android** (`Platforms/Android/AndroidManifest.xml`):
```xml
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
```

**iOS** (`Platforms/iOS/Info.plist`):
No additional permissions required for SecureStorage/Preferences.

## Performance Considerations

### Startup Time

- Initialization runs in background (`Task.Run`)
- Network preloading happens asynchronously
- First wallet unlock may take 1-2 seconds (decryption)

### Storage Limits

- **Android SharedPreferences**: Recommended max 1MB per key
- **iOS NSUserDefaults**: Recommended max 100KB total
- **Windows LocalStorage**: No hard limit, but keep under 10MB

**Optimization:** The package keeps storage lean:
- Recent transactions limited to last 50 per chain
- Pending transactions moved to recent when confirmed
- RPC health cache expires automatically

### Memory Usage

- Vault stored in memory while unlocked
- Transaction lists loaded on-demand per chain
- Network list cached in memory after first load

## Related Packages

- **Nethereum.Wallet.UI.Components** - Platform-agnostic ViewModels and abstractions
- **Nethereum.Wallet.UI.Components.Blazor** - Blazor UI components (required for UI)
- **Nethereum.Wallet** - Core wallet services
- **Nethereum.Wallet.RpcRequests** - RPC request handlers
- **Nethereum.Web3** - Ethereum RPC client

## Source Files

**Services:**
- `Services/MauiSecureStorageWalletVaultService.cs:7-41` - Secure vault storage
- `Services/MauiPreferencesWalletStorageService.cs:18-441` - Settings and data persistence

**Configuration:**
- `Options/MauiWalletComponentOptions.cs:5-10` - Configuration options

**Extensions:**
- `Extensions/MauiAppBuilderExtensions.cs:50-151` - Service registration
- `Extensions/MauiAppBuilderExtensions.cs:156-308` - WalletRegistryInitializer (automatic initialization)
- `Extensions/MauiAppBuilderExtensions.cs:28-46` - Default RPC seeds

## License

MIT License - Part of the Nethereum project.
