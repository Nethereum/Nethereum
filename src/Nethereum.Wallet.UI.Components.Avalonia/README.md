# Nethereum.Wallet.UI.Components.Avalonia

**Avalonia host controls, services and DI registration for the Nethereum wallet UI components**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Wallet.UI.Components.Avalonia.svg)](https://www.nuget.org/packages/Nethereum.Wallet.UI.Components.Avalonia/)

## Overview

`Nethereum.Wallet.UI.Components.Avalonia` is the Avalonia (desktop) front end for `Nethereum.Wallet.UI.Components`, the framework-neutral wallet view models. It gives an Avalonia 11 application a ready wallet: the `NethereumWallet` control (create or unlock the vault, account list, account creation, dashboard), platform services for vault, settings, dialogs and notifications, converters and templates, and one DI extension that registers all of it. Use it to put the Nethereum wallet into an Avalonia app. For Blazor use `Nethereum.Wallet.UI.Components.Blazor`; for the wallet engine itself (vault, accounts, RPC handlers) see `Nethereum.Wallet`.

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components.Avalonia
```

Targets `net10.0` (the build's `WalletUIFrameworks`). Package references: Avalonia 11.3.4 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Fonts.Inter`, `Avalonia.ReactiveUI`, `Avalonia.Themes.Fluent`; `Avalonia.Diagnostics` in Debug builds), `CommunityToolkit.Mvvm` 8.4.0 and `System.Security.Cryptography.ProtectedData` 9.0.0. Project references: `Nethereum.Wallet.RpcRequests`, `Nethereum.Wallet.UI.Components` and `Nethereum.Wallet`.

## Test coverage

`tests/Nethereum.Wallet.UI.Components.Avalonia.UnitTests` holds this package's README traceability tests and the registration tests shown under Registration. The controls themselves need a running Avalonia application and have no behaviour tests, so the control, service and converter blocks below are declaration-only, copied from the source.

## Registration

Namespace `Nethereum.Wallet.UI.Components.Avalonia.Extensions`.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNethereumWalletAvaloniaComponents(
        this IServiceCollection services, Action<AvaloniaWalletComponentOptions>? configure = null)
}

public class AvaloniaWalletComponentOptions
{
    public long DefaultChainId { get; set; } = 1;
}
```

`AddNethereumWalletAvaloniaComponents` uses `TryAdd` registrations, so services you register first win. It adds:

| Registration | Implementation |
|---|---|
| `IWalletVaultService` (singleton) | `AvaloniaWalletVaultService` |
| `IWalletStorageService` (singleton) | `AvaloniaWalletStorageService` |
| `IDappPermissionService` (singleton) | `DefaultDappPermissionService` |
| `IWalletDialogService` (scoped) | `AvaloniaWalletDialogService` |
| `IChainAdditionPromptService` (scoped) | `QueuedChainAdditionPromptService` |
| `ILoginSessionState` (singleton), `ILoginPromptService` (scoped) | `LoginSessionState`, `LoginPromptService` |
| `ILocalizationStorageProvider`, `IWalletLocalizationService` | `SystemLocalizationStorageProvider`, `WalletLocalizationService` |

It also calls `AddWalletPromptServices`, `AddNethereumWalletServicesSingleton(options.DefaultChainId)` and `AddPendingTransactionNotifications`, registers `NethereumWalletLocalizer`, the transient view models `NethereumWalletViewModel`, `AccountListViewModel`, `CreateAccountViewModel` and `WalletHostViewModel`, the transient `WalletHostControl`, and an internal hosted service that registers the wallet RPC handlers (`WalletRpcHandlerRegistration.RegisterAll`) on the `RpcHandlerRegistry` at host start. The `AvaloniaWalletComponentOptions` instance is registered as a singleton.

```csharp
var services = new ServiceCollection();
services.AddNethereumWalletAvaloniaComponents(options => options.DefaultChainId = 10);
using var provider = services.BuildServiceProvider();

var options = provider.GetRequiredService<AvaloniaWalletComponentOptions>();
var vaultService = provider.GetRequiredService<IWalletVaultService>();
var storageService = provider.GetRequiredService<IWalletStorageService>();
```

`options.DefaultChainId` is `10`, `vaultService` is an `AvaloniaWalletVaultService` and `storageService` an `AvaloniaWalletStorageService`. Backed by `AvaloniaWalletRegistrationTests.Given_TheAvaloniaWalletComponents_When_TheyAreRegistered_Then_TheOptionsAndPlatformServicesResolve` (`DocSection.WalletUI`, `avalonia-wallet-components`, Order 1) in `tests/Nethereum.Wallet.UI.Components.Avalonia.UnitTests`. Because the registrations use `TryAdd`, a service you add first is kept: with `IWalletVaultService` registered before the call, that registration is what resolves (`Given_AVaultServiceRegisteredFirst_When_TheAvaloniaComponentsAreRegistered_Then_TheExistingRegistrationWins`, Order 2).

## Controls

Namespace `Nethereum.Wallet.UI.Components.Avalonia.Views`.

- `NethereumWallet` : `UserControl` - the full wallet shell. Styled properties include `OnConnectedCommand` (`ICommand`), `Width` and `Height` (`string`), `DrawerBehavior`, `ResponsiveBreakpoint`, `SidebarWidth`, `ShowLogo`, `ShowApplicationName`, `ShowNetworkInHeader` and `ShowAccountDetailsInHeader`; it exposes the create-wallet and login texts, password visibility commands and `Config`.
- `WalletHostControl` : `UserControl` - a small host. Constructor `WalletHostControl(WalletHostViewModel viewModel)`; it initialises the view model, shows "Wallet unlocked" and "Accounts detected" status, and a "Pending request" overlay (dApp name, type, origin and a Dismiss button) while a prompt is active.
- `AccountListComponent`, `AccountCard`, `AccountTypeSelectionComponent`, `AccountTypeCard`, `CreateAccount`, `WalletFormLayout`, `LoadingOverlay`, and `Dashboard/WalletDashboard`.
- Account creation forms: `WalletAccounts/Mnemonic/MnemonicAccountCreation`, `WalletAccounts/PrivateKey/PrivateKeyAccountCreation`, `WalletAccounts/ViewOnly/ViewOnlyAccountCreation`.
- Shared building blocks (`Views/Shared`): `NotificationBadge`, `WalletAddressDisplay`, `WalletContentSection` (with `WalletSpacing`), `WalletFormSection`, `WalletHeader`, `WalletInfoCard` (with `WalletInfoSeverity`), `WalletSegmentedControl` (with `WalletSegmentOption`), `WalletTextField` (with `WalletTextFieldType`) and `WalletWordChips`.

```csharp
public partial class WalletHostViewModel : ObservableObject, IDisposable
{
    public NethereumWalletViewModel Wallet { get; }

    public WalletHostViewModel(NethereumWalletViewModel wallet, IPromptOverlayService overlayService)

    public async Task InitializeAsync()
    public void Dispose()
}
```

`WalletHostViewModel` also carries the observable properties `CurrentPrompt` (`PromptRequest?`) and `IsOverlayVisible` and the generated `DismissPromptCommand`, which hides the overlay.

Account-creation form types, with their nested step enums:

```csharp
public enum FormStep { Setup = 0, Mnemonic = 1, Security = 2 }

public enum FormStep { Setup = 0, PrivateKey = 1, Confirm = 2 }

public enum FormStep { Setup = 0, Address = 1, Confirm = 2 }

public class WalletFormStep
{
    public string LocalizationKey { get; set; }
    public string Icon { get; set; }
}

public class WalletSegmentedControlOption
{
    public bool Value { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
}
```

The three `FormStep` enums are nested in `MnemonicAccountCreation`, `PrivateKeyAccountCreation` and `ViewOnlyAccountCreation` respectively (the first for the mnemonic form, the second for the private-key form, the third for the view-only form). `WalletFormStep` is in `Views` and `WalletSegmentedControlOption` is declared in the `MnemonicAccountCreation` file.

The default theme is `Themes/DefaultTheme.axaml` (a `Styles` file defining the colour and brush resources the controls use).

## Services

Namespace `Nethereum.Wallet.UI.Components.Avalonia.Services`.

```csharp
public class AvaloniaWalletVaultService : WalletVaultServiceBase
{
    public AvaloniaWalletVaultService()
    public override Task<bool> VaultExistsAsync()
}

public class AvaloniaWalletStorageService : IWalletStorageService
{
    public AvaloniaWalletStorageService()
}

public class AvaloniaWalletDialogService : IWalletDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    public async Task ShowMessageAsync(string title, string message)
    public async Task<T?> ShowDialogAsync<T>(object? parameters = null) where T : class
    public async Task<bool> ShowWarningConfirmationAsync(string title, string message, string confirmText = "Remove", string cancelText = "Cancel")
    public async Task ShowErrorAsync(string title, string message)
    public async Task ShowSuccessAsync(string title, string message)
}

public class AvaloniaWalletNotificationService : IWalletNotificationService
{
    public void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
    public void ShowSuccess(string message)
    public void ShowError(string message)
    public void ShowWarning(string message)
    public void ShowInfo(string message)
    public void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action)
    public void ShowSuccessWithAction(string message, NotificationAction action)
    public void ShowInfoWithAction(string message, NotificationAction action)
}

public interface INotifyWalletLoadingService : IWalletLoadingService
```

Storage behaviour taken from the source:

- `AvaloniaWalletVaultService` keeps the encrypted vault in `vault.dat` under `{ApplicationData}/NethereumWallet`. The file content is protected again at rest: Windows uses `ProtectedData` with `DataProtectionScope.CurrentUser`; macOS and Linux use AES with a key derived from `SHA256(MachineName + UserName)` and a zero IV; other platforms store the bytes unprotected. This is a convenience layer on top of the vault's own password encryption, not a replacement for it.
- `AvaloniaWalletStorageService` implements `IWalletStorageService` (defined in `Nethereum.Wallet.Storage`; its member list is not repeated here) and stores JSON files in the same `NethereumWallet` folder (for example `networks.json` for user networks and `dapp-permissions-index.json` for dApp permissions).

Register your own `IWalletVaultService` or `IWalletStorageService` before calling `AddNethereumWalletAvaloniaComponents` to replace either.

## Converters, templates and markup extensions

- `Converters`: `AccountTypeToBrushConverter`, `AddressToBrushConverter`, `BoolAndBoolConverter`, `BoolAndObjectEqualsToBoolConverter`, `BoolNotConverter`, `BoolToBrushConverter`, `BoolToIconConverter`, `BoolToIconDataConverter`, `BoolToSeverityConverter`, `BoolToStringConverter`, `ColorLightenConverter`, `FormatAddressConverter`, `IconMappingConverter`, `IconToPathDataConverter`, `IsFirstItemConverter`, `IsLastItemConverter`, `LocalizationConverter`, `StringToBrushConverter`, plus the static helpers `ObjectConverters` and `StringConverters` and the generic `FuncValueConverter<TIn, TParam, TOut>` / `FuncValueConverter<TIn, TOut>`.
- `DataTemplates` (`IDataTemplate` selectors): `CreateAccountFormStepTemplateSelector`, `DashboardPluginTemplateSelector`, `MnemonicFormStepTemplateSelector`, `PrivateKeyFormStepTemplateSelector`, `ViewOnlyFormStepTemplateSelector`, `WalletStateTemplateSelector`.
- `Extensions`: `IconPathExtension` (a `MarkupExtension`) and `IconMappingExtensions`.
