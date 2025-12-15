# Nethereum.Wallet.UI.Components.Blazor

Production-ready Blazor component library providing complete Ethereum wallet UI for building decentralized applications. Implements platform-specific services for browser environments using MudBlazor Material Design components and browser localStorage for persistence.

## Package Information

- **Target Framework**: .NET 9.0
- **UI Framework**: Blazor (WebAssembly and Server compatible)
- **Component Library**: MudBlazor 8.x
- **Architecture**: MVVM (CommunityToolkit.Mvvm)
- **Package ID**: Nethereum.Wallet.UI.Components.Blazor

## Dependencies

**NuGet Packages:**
- `CommunityToolkit.Mvvm` 8.4.0 - MVVM infrastructure
- `Microsoft.AspNetCore.Components` 9.0.1 - Blazor core
- `Microsoft.AspNetCore.Components.Web` 9.0.1 - Blazor web components
- `MudBlazor` 8.* - Material Design UI components

**Project References:**
- `Nethereum.Blazor` - EIP-6963 integration
- `Nethereum.Wallet.UI.Components` - Platform-agnostic ViewModels
- `Nethereum.Wallet` - Wallet core services
- `Nethereum.Web3` - Ethereum interaction

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components.Blazor
```

## Quick Start

### 1. Service Registration

Register wallet services in your `Program.cs`:

**Blazor WebAssembly:**
```csharp
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register scoped wallet services (default for WebAssembly)
builder.Services.AddNethereumWalletUI();

// Register storage services
builder.Services.AddSingleton<IWalletVaultService, LocalStorageWalletVaultService>();
builder.Services.AddSingleton<IWalletStorageService, LocalStorageWalletStorageService>();
builder.Services.AddSingleton<IEncryptionStrategy, AesEncryptionStrategy>();

await builder.Build().RunAsync();
```

**Blazor Server:**
```csharp
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Register scoped wallet services
builder.Services.AddNethereumWalletUI();

// Register storage services
builder.Services.AddScoped<IWalletVaultService, LocalStorageWalletVaultService>();
builder.Services.AddScoped<IWalletStorageService, LocalStorageWalletStorageService>();
builder.Services.AddSingleton<IEncryptionStrategy, AesEncryptionStrategy>();

var app = builder.Build();
app.Run();
```

### 2. Add Wallet Component

Add the wallet component to your page:

```razor
@page "/"
@using Nethereum.Wallet.UI.Components.Blazor.NethereumWallet

<NethereumWallet OnConnected="@HandleWalletConnected"
                 Width="100%"
                 Height="100vh" />

@code {
    private async Task HandleWalletConnected()
    {
        // Wallet is unlocked and ready
        Console.WriteLine("Wallet connected!");
    }
}
```

### 3. Initialize Registries

Initialize component registries in your app startup (after DI container is built):

```csharp
var app = builder.Build();

// Initialize account type registries
var scope = app.Services.CreateScope();
scope.ServiceProvider.InitializeAccountTypes();

await app.RunAsync();
```

## Architecture

### Service Registration

The package provides three registration methods in `ServiceCollectionExtensions.cs`:

#### AddNethereumWalletUI() / AddNethereumWalletUIScoped()

Registers all wallet services with **scoped** lifetime (recommended for most scenarios).

**Location**: `Extensions/ServiceCollectionExtensions.cs:46-142`

**Key Registrations:**
```csharp
// MudBlazor configuration
services.AddMudServices(config => {
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    // ... additional snackbar configuration
});

// Platform services
services.AddScoped<IWalletNotificationService, BlazorWalletNotificationService>();
services.AddScoped<IWalletDialogService, BlazorWalletDialogService>();
services.AddScoped<IWalletLoadingService, MudLoadingService>();

// Component registries
services.AddScoped<IAccountCreationRegistry, AccountCreationRegistry>();
services.AddScoped<IAccountDetailsRegistry, AccountDetailsRegistry>();
services.AddScoped<IGroupDetailsRegistry, GroupDetailsRegistry>();

// ViewModels
services.AddScoped<NethereumWalletViewModel>();
services.AddScoped<WalletDashboardViewModel>();
services.AddScoped<CreateAccountViewModel>();

// Account type ViewModels
services.AddScoped<IAccountCreationViewModel, MnemonicAccountCreationViewModel>();
services.AddScoped<IAccountCreationViewModel, PrivateKeyAccountCreationViewModel>();
services.AddScoped<IAccountCreationViewModel, ViewOnlyAccountCreationViewModel>();

// Additional services
services.AddNetworkManagement();
services.AddTokenTransferServices();
services.AddTransactionServices();
services.AddPromptsServices();
```

#### AddNethereumWalletUISingleton()

Registers wallet services with **singleton** lifetime for application-wide state scenarios.

**Location**: `Extensions/ServiceCollectionExtensions.cs:147-232`

Uses `Transient` for ViewModels and `Singleton` for registries and infrastructure.

### Component Registry System

Component registries map ViewModels to Razor components dynamically using the `IWalletUIRegistryContributor` pattern.

#### Registry Initialization

**Location**: `Extensions/ServiceCollectionExtensions.cs:276-318`

```csharp
public static void InitializeAccountTypes(this IServiceProvider serviceProvider)
{
    serviceProvider.ConfigureAccountCreationRegistry();
    serviceProvider.ConfigureAccountDetailsRegistry();
    serviceProvider.ConfigureGroupDetailsRegistry();
    serviceProvider.ConfigureDashboardPluginRegistry();
}
```

**Account Creation Registry:**
```csharp
// Maps account creation ViewModels to Razor components
registry.Register<MnemonicAccountCreationViewModel, MnemonicAccountCreation>();
registry.Register<PrivateKeyAccountCreationViewModel, PrivateKeyAccountCreation>();
registry.Register<ViewOnlyAccountCreationViewModel, ViewOnlyAccountCreation>();
registry.Register<VaultMnemonicAccountViewModel, VaultMnemonicAccountEditor>();
```

**Dashboard Plugin Registry:**
```csharp
// Maps dashboard plugins to components
componentRegistry.Register<AccountListPluginViewModel, AccountList>();
componentRegistry.Register<CreateAccountPluginViewModel, CreateAccount>();
componentRegistry.Register<WalletOverviewPluginViewModel, WalletOverview>();
componentRegistry.Register<NetworkManagementPluginViewModel, NetworkManagement>();
componentRegistry.Register<SendNativeTokenViewModel, TokenTransfer>();
componentRegistry.Register<PromptsPluginViewModel, PromptsPlugin>();
```

#### WalletUIBootstrapper

Ensures registries are initialized before component use.

**Location**: `Services/WalletUIBootstrapper.cs:8-38`

```csharp
public sealed class WalletUIBootstrapper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IWalletUIRegistryContributor> _registryContributors;
    private bool _initialized;

    public void EnsureInitialized()
    {
        if (_initialized) return;

        // Initialize built-in account types
        _serviceProvider.InitializeAccountTypes();

        // Apply custom registry contributors (e.g., Trezor hardware wallet)
        foreach (var contributor in _registryContributors)
        {
            contributor.Configure(_serviceProvider);
        }

        _initialized = true;
    }
}
```

## Platform Services

### BlazorWalletDialogService

Implements `IWalletDialogService` using MudBlazor dialogs.

**Location**: `Services/BlazorWalletDialogService.cs:10-149`

**Methods:**

```csharp
// Confirmation dialog with Yes/No options
Task<bool> ShowConfirmationAsync(string title, string message)
```
- Uses `WalletPromptDialog` component
- Icon: `Icons.Material.Filled.HelpOutline`
- Color: Warning
- Returns `true` if user confirms

```csharp
// Information message dialog
Task ShowMessageAsync(string title, string message)
```
- Icon: `Icons.Material.Filled.Info`
- Color: Info
- Single OK button

```csharp
// Warning confirmation dialog (destructive actions)
Task<bool> ShowWarningConfirmationAsync(string title, string message,
    string confirmText = "Remove", string cancelText = "Cancel")
```
- Icon: `Icons.Material.Filled.Warning`
- Color: Error for confirm button
- Used for account deletion, wallet reset, etc.

```csharp
// Error message dialog
Task ShowErrorAsync(string title, string message)
```
- Icon: `Icons.Material.Filled.Error`
- Color: Error

```csharp
// Success message dialog
Task ShowSuccessAsync(string title, string message)
```
- Icon: `Icons.Material.Filled.CheckCircle`
- Color: Success

**Dialog Options:**
- `BackdropClick`: false (prevents accidental dismissal)
- `MaxWidth`: MaxWidth.Small
- `CloseButton`: false (requires explicit user action)

### BlazorWalletNotificationService

Implements `IWalletNotificationService` using MudBlazor Snackbar.

**Location**: `Services/BlazorWalletNotificationService.cs:7-138`

**Configuration:**
- Position: Bottom-left
- Variant: Filled
- Auto-hide with severity-based durations

**Methods:**

```csharp
// Show notification with custom severity
void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
```

```csharp
// Success notification (3 second duration)
void ShowSuccess(string message)
```
- Action icon: ✓
- Duration: 3000ms

```csharp
// Error notification (5 second duration, requires interaction)
void ShowError(string message)
```
- Action icon: ×
- Duration: 5000ms
- `RequireInteraction`: true

```csharp
// Warning notification (4 second duration)
void ShowWarning(string message)
```
- Action icon: ⚠
- Duration: 4000ms

```csharp
// Info notification (3 second duration)
void ShowInfo(string message)
```
- Action icon: ℹ
- Duration: 3000ms

```csharp
// Notification with custom action button
void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action)
```

### LocalStorageWalletVaultService

Implements `IWalletVaultService` using browser localStorage for encrypted wallet storage.

**Location**: `Services/LocalStorageWalletVaultService.cs:10-45`

**Storage Key**: `Nethereum.Wallet.Vault`

**Methods:**

```csharp
// Check if vault exists in localStorage
async Task<bool> VaultExistsAsync()
```

```csharp
// Get encrypted vault data
async Task<string?> GetEncryptedAsync()
```

```csharp
// Save encrypted vault data
async Task SaveEncryptedAsync(string encrypted)
```

```csharp
// Delete vault from localStorage
async Task ResetStorageAsync()
```

**Encryption:**
Uses `IEncryptionStrategy` (typically AES-256) provided during service registration. Vault data is encrypted before storage and decrypted on retrieval.

### LocalStorageWalletStorageService

Implements `IWalletStorageService` for persisting wallet configuration, networks, transactions, and DApp permissions.

**Location**: `Services/LocalStorageWalletStorageService.cs:18-560`

**Storage Keys:**
- Settings: `Nethereum.Wallet.Settings.{key}`
- User Networks: `Nethereum.Wallet.UserNetworks`
- Custom RPCs: `Nethereum.Wallet.CustomRpcs.{chainId}`
- Active RPCs: `Nethereum.Wallet.ActiveRpcs.{chainId}`
- RPC Health: `Nethereum.Wallet.RpcHealth.{base64(rpcUrl)}`
- Transactions (Pending): `Nethereum.Wallet.Transactions.Pending.{chainId}`
- Transactions (Recent): `Nethereum.Wallet.Transactions.Recent.{chainId}` (max 50)
- DApp Permissions: `Nethereum.Wallet.DAppPermissions.{origin}`
- Selected Network: `Nethereum.Wallet.Settings.SelectedNetwork`
- Selected Account: `Nethereum.Wallet.Settings.SelectedAccount`

**Key Methods:**

```csharp
// Generic settings storage
async Task<T?> GetSettingAsync<T>(string key)
async Task SetSettingAsync<T>(string key, T value)
async Task RemoveSettingAsync(string key)
```

```csharp
// Network management
async Task<List<ChainFeature>> GetUserNetworksAsync()
async Task SaveUserNetworkAsync(ChainFeature network)
async Task DeleteUserNetworkAsync(BigInteger chainId)
```

```csharp
// RPC endpoint management
async Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
async Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
async Task RemoveRpcAsync(BigInteger chainId, string rpcUrl)
```

```csharp
// Transaction history
async Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
async Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
async Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction)
async Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status)
```

```csharp
// DApp permission management
async Task<string?> GetDAppPermissionsAsync(string dappOrigin)
async Task SaveDAppPermissionsAsync(string dappOrigin, string permissionsJson)
async Task RemoveDAppPermissionsAsync(string dappOrigin)
```

### WalletDialogAccessor

Provides access to MudBlazor `IDialogService` for components that need dialog functionality outside the normal DI scope (e.g., hardware wallet prompt handlers).

**Location**: `Services/WalletDialogAccessor.cs:5-14`

```csharp
public interface IWalletDialogAccessor
{
    IDialogService? DialogService { get; set; }
}
```

Set by `NethereumWallet.razor:223` on initialization.

## Main Components

### NethereumWallet

Root wallet component managing authentication, vault creation, and dashboard routing.

**Location**: `NethereumWallet/NethereumWallet.razor:1-390`

**Features:**
- **Vault Creation**: Password-protected wallet creation with strength indicator
- **Authentication**: Login form with password visibility toggle
- **Auto-focus**: Password field auto-focus on mount (configurable)
- **Keyboard Shortcuts**: Enter key to submit login form
- **Wallet Reset**: Optional destructive wallet reset (configurable)
- **Account Creation**: Embedded account creation flow for first-time users
- **Dashboard Routing**: Transitions to dashboard when wallet has accounts

**Parameters:**
```csharp
[Parameter] public EventCallback OnConnected { get; set; }
[Parameter] public string? Width { get; set; }          // Default: 100%
[Parameter] public string? Height { get; set; }         // Default: 100%
[Parameter] public DrawerBehavior? DrawerBehavior { get; set; }
[Parameter] public int? ResponsiveBreakpoint { get; set; }
[Parameter] public int? SidebarWidth { get; set; }
[Parameter] public bool? ShowLogo { get; set; }
[Parameter] public bool? ShowApplicationName { get; set; }
[Parameter] public bool? ShowNetworkInHeader { get; set; }
[Parameter] public bool? ShowAccountDetailsInHeader { get; set; }
```

**States:**

1. **Loading** (Lines 32-43):
   - Shows progress indicator while initializing
   - Configurable via `Config.ShowProgressIndicators`

2. **Login** (Lines 44-109):
   - Password input with visibility toggle
   - "Create New Wallet" link (if vault exists)
   - Optional "Reset Wallet" button (configurable)
   - Auto-focus password field
   - Enter key support

3. **Wallet Creation** (Lines 126-196):
   - New password with strength indicator
   - Confirm password with mismatch validation
   - Logo and application name display
   - Cancel button (when creating from login screen)

4. **Account Creation** (Lines 111-117):
   - Embedded `CreateAccount` component
   - Shown when wallet is unlocked but has no accounts
   - Transitions to dashboard after account creation

5. **Dashboard** (Lines 118-123):
   - `WalletDashboard` component
   - Shown when wallet is unlocked and has accounts
   - Handles logout

**Dispatcher Registration** (Lines 221-223):
```csharp
WalletBlazorDispatcher.Register(RunOnUiThreadAsync);
DialogAccessor.DialogService = DialogService;
```

Enables background threads (e.g., transaction monitoring) to update UI safely.

### WalletDashboard

Plugin-based dashboard with responsive sidebar navigation and dynamic content area.

**Location**: `Dashboard/WalletDashboard.razor:1-568`

**Features:**
- **Plugin System**: Dynamic plugin registration and rendering
- **Responsive Layout**: Desktop sidebar / mobile overlay
- **Network Switching**: Header displays current network with click navigation
- **Account Switching**: Header displays current account with click navigation
- **Navigation Service**: Programmatic plugin navigation with parameters
- **Size Detection**: Automatic responsive breakpoint detection
- **Notification Badge**: Displays pending DApp prompts

**Parameters:**
```csharp
[Parameter] public EventCallback OnLogout { get; set; }
[Parameter] public string? SelectedAccount { get; set; }
[Parameter] public string Title { get; set; } = "Wallet Dashboard"
[Parameter] public string Subtitle { get; set; } = "Manage your accounts, security, and wallet settings"
[Parameter] public string MobileTitle { get; set; } = "Wallet"
```

**Layout Modes:**

**Desktop Layout** (Lines 27-67):
- Persistent sidebar (width configurable via `GlobalConfig.SidebarWidth`, default 280px)
- Logo and application name in sidebar header
- Navigation menu with icons
- Logout button at bottom

**Mobile Layout** (Lines 118-153):
- Overlay menu triggered by hamburger button
- Same navigation structure as desktop
- Auto-closes after navigation

**Layout Behavior** (Lines 526-546):
```csharp
private bool ShouldShowSidebar()
{
    return GlobalConfig.DrawerBehavior switch
    {
        DrawerBehavior.AlwaysShow => true,      // Always show sidebar
        DrawerBehavior.AlwaysHidden => false,   // Always use overlay
        DrawerBehavior.Responsive => !isCompact, // Responsive (default)
        _ => !isCompact
    };
}
```

**Header** (Lines 72-95):
Uses `WalletHeader` component displaying:
- Application name and logo
- Current network (with logo and chain ID)
- Current account (name and address)
- Notification badge
- Menu button (mobile)

**Plugin Rendering** (Lines 100-114):
```csharp
<DynamicComponent Type="@GetPluginComponentType(SelectedPlugin)"
                  Parameters="@GetPluginParameters(SelectedPlugin)"
                  @key="@GetPluginKey(SelectedPlugin)"
                  @ref="activePluginComponentRef" />
```

**Plugin Parameters** (Lines 340-373):
- `SelectedAccount`: Current account address
- `ComponentWidth`: Current component width (for responsive components)
- `IsCompact`: Boolean indicating compact mode
- `OnAccountAdded`: Callback for account creation workflow
- `OnReady`: Callback for plugin initialization

**Navigation System** (Lines 430-474):
```csharp
private async Task OnNavigationRequestedAsync(object sender, DashboardNavigationEventArgs e)
{
    pendingNavigationParameters = e.Parameters;
    await SwitchToPlugin(e.PluginId);
}

private async void OnPluginReady(object pluginInstance)
{
    if (pendingNavigationParameters != null && pluginInstance is INavigatablePlugin nav)
    {
        await nav.NavigateWithParametersAsync(pendingNavigationParameters);
        pendingNavigationParameters = null;
    }
}
```

Solves Blazor parameter caching issues by delivering navigation parameters when plugin is ready.

**Built-in Plugins:**

1. **AccountList** (`account-list`) - Account management
2. **CreateAccount** (`create-account`) - Account creation wizard
3. **WalletOverview** (plugin ID varies) - Portfolio overview
4. **NetworkManagement** (`network_management`) - Network management
5. **TokenTransfer** (plugin ID varies) - Send tokens
6. **PromptsPlugin** (`Prompts`) - DApp interaction prompts

**Auto-navigation to Prompts** (Lines 436-462):
```csharp
private async void OnPromptQueueChanged(object? sender, PromptQueueChangedEventArgs e)
{
    if (e.ChangeType == PromptQueueChangeType.Added)
    {
        await DashboardNavService.NavigateToPluginAsync("Prompts");
    }
    else if (!PromptQueueService.HasPendingPrompts && SelectedPlugin?.PluginId == "Prompts")
    {
        // Return to first plugin when no prompts remain
        await InvokeAsync(() => {
            var plugins = AvailablePlugins.ToList();
            SelectedPlugin = plugins.FirstOrDefault();
            ActivePluginIndex = 0;
            StateHasChanged();
        });
    }
}
```

## Account Creation Components

### MnemonicAccountCreation

Multi-step wizard for creating or importing mnemonic (HD wallet) accounts.

**Location**: `WalletAccounts/Mnemonic/MnemonicAccountCreation.razor:1-*`

**Steps:**

1. **Setup** (Lines 38-56):
   - Wallet name input
   - Mode selection: Generate / Import
   - Info card explaining the selected mode

2. **Mnemonic** (Lines 58-*):
   - **Generate Mode**:
     - 12 or 24 word generation buttons
     - Word chips with show/hide toggle
     - Optional clipboard copy
     - Optional BIP-39 passphrase entry
   - **Import Mode**:
     - Manual word entry with autocomplete
     - Word validation against BIP-39 wordlist
     - Optional BIP-39 passphrase entry

3. **Security** (Final step):
   - Account confirmation
   - Security warnings
   - Create account button

**Component Usage:**
```razor
<WalletFormLayout Title="Create Mnemonic Account"
                  Steps="@formSteps"
                  CurrentStepIndex="@((int)CurrentStep)"
                  ShowBack="@(CurrentStep > FormStep.Setup)"
                  ShowContinue="@(CurrentStep != FormStep.Security)"
                  ShowPrimary="@(CurrentStep == FormStep.Security)"
                  OnBack="@GoToPreviousStep"
                  OnContinue="@HandleContinue"
                  OnPrimary="@CreateAccount">
    <!-- Step content -->
</WalletFormLayout>
```

**ViewModel**: `MnemonicAccountCreationViewModel` (Nethereum.Wallet.UI.Components)

## Shared Components

### WalletPromptDialog

Generic dialog component used by `BlazorWalletDialogService`.

**Location**: `Shared/WalletPromptDialog.razor:1-110`

**Parameters:**
```csharp
[Parameter] public string Title { get; set; } = ""
[Parameter] public string Message { get; set; } = ""
[Parameter] public string Icon { get; set; } = ""
[Parameter] public Color IconColor { get; set; } = Color.Primary
[Parameter] public string ConfirmText { get; set; } = "Confirm"
[Parameter] public string CancelText { get; set; } = "Cancel"
[Parameter] public Color ConfirmColor { get; set; } = Color.Primary
[Parameter] public bool ShowCancel { get; set; } = true
[Parameter] public bool IsLoading { get; set; } = false
[Parameter] public EventCallback<bool> OnResult { get; set; }
[Parameter] public RenderFragment? ChildContent { get; set; }
```

**Structure** (Lines 8-70):
```razor
<MudDialog>
    <DialogContent>
        <div class="wallet-prompt-dialog">
            <!-- Icon (if provided) -->
            <div class="wallet-prompt-icon">
                <MudIcon Icon="@Icon" Size="Size.Large" Color="@IconColor" />
            </div>

            <!-- Title and Message -->
            <div class="wallet-prompt-content">
                <MudText Typo="Typo.h6">@Title</MudText>
                <MudText Typo="Typo.body1">@Message</MudText>

                <!-- Optional custom content -->
                @ChildContent
            </div>
        </div>
    </DialogContent>
    <DialogActions>
        <!-- Cancel button (optional) -->
        <MudButton OnClick="HandleCancel">@CancelText</MudButton>

        <!-- Confirm button with loading spinner -->
        <MudButton Color="@ConfirmColor" OnClick="HandleConfirm" Disabled="@IsLoading">
            @if (IsLoading)
            {
                <MudProgressCircular Size="Size.Small" Indeterminate="true" />
            }
            else
            {
                @ConfirmText
            }
        </MudButton>
    </DialogActions>
</MudDialog>
```

### WalletFormLayout

Reusable form layout with step progression, navigation buttons, and consistent styling.

**Location**: `Shared/WalletFormLayout.razor` (referenced in MnemonicAccountCreation)

Used by account creation wizards (Mnemonic, PrivateKey, ViewOnly, Trezor).

### WalletHeader

Comprehensive header component displaying application info, network, account, and actions.

**Location**: `Shared/WalletHeader.razor` (referenced in WalletDashboard)

**Features:**
- Application logo and name
- Current network chip with logo
- Current account chip with address
- Menu button for mobile
- Custom actions slot (e.g., notification badge)
- Click handlers for account and network navigation

## DApp Prompts

### DAppTransactionPromptView

Transaction approval prompt for DApp-initiated transactions.

**Location**: `Prompts/DAppTransactionPromptView.razor:1-80+`

**Steps:**

1. **Request Details** (Lines 33-66):
   - DApp name and origin
   - Transaction details (recipient, amount, data)
   - Data decoding (if available)
   - Warning messages (if applicable)
   - Validation errors

2. **Gas Configuration** (Lines 68-79):
   - Gas settings (EIP-1559 or legacy)
   - Gas strategy selection
   - Cost summary

**Component Usage:**
```razor
<WalletFormLayout Title="@GetTitle()"
                  Subtitle="@GetSubtitle()"
                  Steps="@_steps"
                  CurrentStepIndex="@ViewModel.CurrentStep"
                  ShowBack="@(ViewModel.CurrentStep > 0 && ViewModel.CurrentStep < 2)"
                  ShowContinue="@(ViewModel.CurrentStep == 0)"
                  ShowPrimary="@(ViewModel.CurrentStep == 1)"
                  OnBack="HandleBack"
                  OnContinue="HandleContinue"
                  OnPrimary="HandlePrimary">
    <!-- Step content with TransactionInput component -->
</WalletFormLayout>
```

**Reuses**: `TransactionInput` component for transaction details and gas configuration.

### Other DApp Prompts

- **DAppPermissionPromptView** - Account connection approval
- **DAppSignaturePromptView** - Message signing (`eth_sign`, `personal_sign`)
- **DAppChainSwitchPromptView** - Network switch approval (`wallet_switchEthereumChain`)
- **DAppChainAdditionPromptView** - Custom network addition (`wallet_addEthereumChain`)

## Localization

Localization is registered in `ServiceCollectionExtensions.cs:234-264`:

```csharp
private static void RegisterWalletUILocalization(IServiceCollection services)
{
    services.TryAddSingleton<ILocalizationStorageProvider, BrowserLocalizationStorageProvider>();
    services.TryAddSingleton<IWalletLocalizationService, WalletLocalizationService>();

    // Component localizers
    services.TryAddSingleton<IComponentLocalizer<NethereumWalletViewModel>, NethereumWalletLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<MnemonicAccountCreationViewModel>, MnemonicAccountEditorLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<PrivateKeyAccountCreationViewModel>, PrivateKeyAccountEditorLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<ViewOnlyAccountCreationViewModel>, ViewOnlyAccountEditorLocalizer>();
    // ... additional localizers
}
```

**Supported Languages:** English (en-US), Spanish (es-ES)

**Storage:** Browser localStorage via `BrowserLocalizationStorageProvider`

## Configuration

### NethereumWalletConfiguration

Main wallet configuration (registered as singleton).

**Usage in Component** (NethereumWallet.razor:24):
```razor
@inject NethereumWalletConfiguration Config
```

**Properties:**
- `ShowProgressIndicators` - Show loading spinners
- `AllowPasswordVisibilityToggle` - Password visibility toggle button
- `EnableKeyboardShortcuts` - Enter key to submit forms
- `Behavior.EnableWalletReset` - Show "Reset Wallet" button
- `Behavior.AutoFocusPasswordField` - Auto-focus password on mount

### INethereumWalletUIConfiguration

Global UI configuration (registered as singleton).

**Usage in Component** (NethereumWallet.razor:26, WalletDashboard.razor:17):
```razor
@inject INethereumWalletUIConfiguration GlobalConfig
```

**Properties:**
- `ApplicationName` - Application name displayed in header
- `LogoPath` / `WelcomeLogoPath` - Logo image paths
- `ShowLogo` / `ShowApplicationName` - Display toggles
- `ShowNetworkInHeader` / `ShowAccountDetailsInHeader` - Header display options
- `DrawerBehavior` - Sidebar behavior (AlwaysShow, AlwaysHidden, Responsive)
- `ResponsiveBreakpoint` - Pixel width for mobile/desktop switch (default: 960px)
- `SidebarWidth` - Sidebar width in pixels (default: 280px)

## Extension Methods

### Network Management

**Location**: Referenced in `ServiceCollectionExtensions.cs:125`

```csharp
services.AddNetworkManagement();
```

Registers:
- `NetworkManagementPluginViewModel`
- `IChainManagementService`
- `INetworkIconProvider` (default implementation)

### Token Transfer

**Location**: Referenced in `ServiceCollectionExtensions.cs:127`

```csharp
services.AddTokenTransferServices();
```

Registers:
- `SendNativeTokenViewModel`
- `TokenNativeTransferModel`
- Gas configuration services

### Transaction Services

**Location**: Referenced in `ServiceCollectionExtensions.cs:129`

```csharp
services.AddTransactionServices();
```

Registers:
- `TransactionMonitoringService` (IHostedService)
- `ITransactionService`
- Pending transaction notifications

### DApp Prompts

**Location**: Referenced in `ServiceCollectionExtensions.cs:131`

```csharp
services.AddPromptsServices();
```

Registers:
- `PromptsPluginViewModel`
- `DAppTransactionPromptViewModel`
- `DAppSignaturePromptViewModel`
- `DAppChainSwitchPromptViewModel`
- `DAppChainAdditionPromptViewModel`
- `IPromptQueueService`
- `IChainAdditionPromptService`

## Browser Compatibility

**Supported Browsers:**
- Chrome/Edge (Chromium) 90+
- Firefox 88+
- Safari 14+

**Browser Storage:**
- localStorage for vault and settings
- Encrypted vault using AES-256
- ~5MB typical storage limit (varies by browser)

## Example: Full Blazor WebAssembly Setup

### Program.cs

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.Blazor.Services;
using Nethereum.Wallet.UI.Components.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register wallet services
builder.Services.AddNethereumWalletUI();

// Register storage services
builder.Services.AddSingleton<IWalletVaultService, LocalStorageWalletVaultService>();
builder.Services.AddSingleton<IWalletStorageService, LocalStorageWalletStorageService>();
builder.Services.AddSingleton<IEncryptionStrategy, AesEncryptionStrategy>();

// Configure wallet UI
builder.Services.AddSingleton<INethereumWalletUIConfiguration>(sp =>
    new DefaultNethereumWalletUIConfiguration
    {
        ApplicationName = "My DApp Wallet",
        LogoPath = "logo.png",
        WelcomeLogoPath = "welcome-logo.png",
        ShowLogo = true,
        ShowApplicationName = true,
        DrawerBehavior = DrawerBehavior.Responsive,
        ResponsiveBreakpoint = 960,
        SidebarWidth = 280
    });

var app = builder.Build();

// Initialize component registries
var scope = app.Services.CreateScope();
scope.ServiceProvider.InitializeAccountTypes();

await app.RunAsync();
```

### App.razor

```razor
@using Nethereum.Wallet.UI.Components.Blazor.NethereumWallet

<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

### Pages/Index.razor

```razor
@page "/"
@using Nethereum.Wallet.UI.Components.Blazor.NethereumWallet

<PageTitle>Wallet</PageTitle>

<div style="height: 100vh; width: 100vw;">
    <NethereumWallet OnConnected="@HandleWalletConnected"
                     Width="100%"
                     Height="100%"
                     DrawerBehavior="DrawerBehavior.Responsive"
                     ResponsiveBreakpoint="960" />
</div>

@code {
    private void HandleWalletConnected()
    {
        Console.WriteLine("Wallet connected and ready!");
    }
}
```

### wwwroot/index.html

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My DApp Wallet</title>
    <base href="/" />

    <!-- MudBlazor CSS -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
</head>
<body>
    <div id="app">Loading...</div>

    <!-- Blazor -->
    <script src="_framework/blazor.webassembly.js"></script>

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

## Integration with Hardware Wallets

To add hardware wallet support (e.g., Trezor), install the additional package:

```bash
dotnet add package Nethereum.Wallet.UI.Components.Blazor.Trezor
```

Register Trezor services:

```csharp
using Nethereum.Wallet.UI.Components.Blazor.Trezor.Extensions;

builder.Services.AddNethereumWalletUI();
builder.Services.AddTrezorWalletBlazorComponents();

// Initialize registries (includes Trezor components)
var scope = app.Services.CreateScope();
scope.ServiceProvider.InitializeAccountTypes();
```

The Trezor registry contributor automatically registers Trezor account types into the wallet UI.

## Related Packages

- **Nethereum.Wallet.UI.Components** - Platform-agnostic ViewModels and abstractions
- **Nethereum.Wallet.UI.Components.Blazor.Trezor** - Trezor hardware wallet support
- **Nethereum.Wallet.UI.Components.Maui** - MAUI platform implementation
- **Nethereum.Wallet** - Core wallet services (accounts, vault, transaction management)
- **Nethereum.Blazor** - EIP-6963 wallet discovery and authentication
- **Nethereum.Web3** - Ethereum RPC client

## Source Files

**Extensions:**
- `Extensions/ServiceCollectionExtensions.cs:1-323` - Service registration
- `Extensions/NetworkServiceCollectionExtensions.cs` - Network service registration

**Platform Services:**
- `Services/BlazorWalletDialogService.cs:10-149` - MudBlazor dialogs
- `Services/BlazorWalletNotificationService.cs:7-138` - Snackbar notifications
- `Services/LocalStorageWalletVaultService.cs:10-45` - Encrypted vault storage
- `Services/LocalStorageWalletStorageService.cs:18-560` - Settings and data persistence
- `Services/WalletUIBootstrapper.cs:8-38` - Registry initialization
- `Services/WalletDialogAccessor.cs:5-14` - Dialog service accessor
- `Services/MudLoadingService.cs` - Loading indicator service

**Main Components:**
- `NethereumWallet/NethereumWallet.razor:1-390` - Root wallet component
- `Dashboard/WalletDashboard.razor:1-568` - Dashboard with plugin system

**Account Creation:**
- `WalletAccounts/Mnemonic/MnemonicAccountCreation.razor` - Mnemonic account wizard
- `WalletAccounts/PrivateKey/PrivateKeyAccountCreation.razor` - Private key import
- `WalletAccounts/ViewOnly/ViewOnlyAccountCreation.razor` - View-only account
- `CreateAccount/CreateAccount.razor` - Account type selection

**Account Details:**
- `WalletAccounts/Mnemonic/MnemonicAccountDetails.razor` - Mnemonic account details
- `WalletAccounts/PrivateKey/PrivateKeyAccountDetails.razor` - Private key account details
- `WalletAccounts/ViewOnly/ViewOnlyAccountDetails.razor` - View-only account details
- `AccountDetails/AccountDetails.razor` - Account details container
- `AccountDetails/GroupDetails.razor` - Group details container

**Shared Components:**
- `Shared/WalletPromptDialog.razor:1-110` - Generic dialog
- `Shared/WalletFormLayout.razor` - Form layout with steps
- `Shared/WalletHeader.razor` - Dashboard header
- `Shared/WalletTextField.razor` - Text input field
- `Shared/WalletAddressDisplay.razor` - Address display with copy
- `Shared/WalletMnemonicDisplay.razor` - Mnemonic display
- `Shared/WalletWordChips.razor` - Word chip collection
- `Shared/NotificationBadge.razor` - Notification counter badge
- `Shared/` - 60+ additional shared components

**DApp Prompts:**
- `Prompts/DAppTransactionPromptView.razor:1-80+` - Transaction approval
- `Prompts/DAppPermissionPromptView.razor` - Account connection
- `Prompts/DAppSignaturePromptView.razor` - Message signing
- `Prompts/DAppChainSwitchPromptView.razor` - Network switch
- `Prompts/DAppChainAdditionPromptView.razor` - Custom network addition
- `Prompts/PromptsPlugin.razor` - Prompts dashboard plugin

**Transactions:**
- `SendTransaction/TokenTransfer.razor` - Send token wizard
- `SendTransaction/TransactionInput.razor` - Transaction input form
- `SendTransaction/TransactionConfirmation.razor` - Confirmation screen
- `Transactions/TransactionHistory.razor` - Transaction history
- `Transactions/PendingTransactionsList.razor` - Pending transactions

**Networks:**
- `Networks/NetworkManagement.razor` - Network management hub
- `Networks/NetworkList.razor` - Network list
- `Networks/NetworkCard.razor` - Network card
- `Networks/AddCustomNetwork.razor` - Add custom network form

## License

MIT License - Part of the Nethereum project.
