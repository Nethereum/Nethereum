---
name: wallet-quickstart
description: Build a multi-platform wallet application using the Nethereum Wallet SDK with MVVM architecture (.NET/C#). Use this skill when the user asks about building a wallet app, Nethereum.Wallet, wallet UI components, MVVM wallet, WalletVault, account management in the wallet SDK, or creating wallet screens in Blazor or MAUI.
user-invocable: true
---

# Nethereum Wallet SDK

Build multi-platform self-custodial wallet applications using a layered MVVM architecture with shared ViewModels and platform-specific renderers.

## Architecture

```
Nethereum.Wallet                              Core services
  + Nethereum.Wallet.UI.Components            Shared MVVM ViewModels
  + Nethereum.Wallet.UI.Components.Blazor     Blazor/MudBlazor renderer
    or .Maui                                  .NET MAUI renderer
```

NuGet packages:

```bash
dotnet add package Nethereum.Wallet
dotnet add package Nethereum.Wallet.UI.Components.Blazor
```

## Quick Start

### Register Services (Program.cs)

```csharp
builder.Services.AddNethereumWallet();
builder.Services.AddNethereumWalletBlazorComponents();
```

### Add the Wallet Component

```razor
@using Nethereum.Wallet.UI.Components.Blazor
<NethereumWallet />
```

This gives you the full wallet UI: account creation, vault management, dashboard, and transaction screens.

## WalletVault — Encrypted Storage

The `WalletVault` encrypts all wallet data (private keys, mnemonic seeds) with a user password:

```csharp
var vault = new WalletVault();
vault.CreateNew("strong-password-here");
```

Lock and unlock:

```csharp
vault.Lock();
bool success = vault.Unlock("strong-password-here");
```

The vault uses AES encryption. The `IWalletVaultService` abstraction handles platform-specific secure storage.

## Account Types

### Mnemonic (BIP39/BIP44)

```csharp
var accountService = new CoreWalletAccountService(vault);
var mnemonicAccount = accountService.CreateMnemonicAccount(
    "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
    password: null,
    accountIndex: 0);
```

The `MnemonicWalletAccountFactory` creates accounts with standard derivation paths (`m/44'/60'/0'/0/x`).

### Private Key

```csharp
var account = accountService.CreatePrivateKeyAccount(
    "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80",
    name: "My Account");
```

### View-Only

```csharp
var viewOnly = accountService.CreateViewOnlyAccount(
    "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B",
    name: "Vitalik Watch");
```

## ViewModel Pattern

All wallet screens follow the same MVVM pattern using CommunityToolkit.Mvvm:

```csharp
public partial class MyScreenViewModel : ObservableObject
{
    [ObservableProperty] private string _fieldName = "";
    [ObservableProperty] private string? _fieldNameError;

    partial void OnFieldNameChanged(string value) => ValidateFieldName();

    private void ValidateFieldName()
    {
        FieldNameError = string.IsNullOrWhiteSpace(FieldName)
            ? _localizer.GetString(Keys.FieldRequired)
            : null;
    }

    public bool IsFormValid => string.IsNullOrEmpty(FieldNameError)
        && !string.IsNullOrWhiteSpace(FieldName);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsFormValid) return;
        // ...
    }
}
```

## Localisation

Every screen has a Localizer with EN/ES translations:

```csharp
public class MyScreenLocalizer : ComponentLocalizerBase<MyScreenViewModel>
{
    public static class Keys
    {
        public const string Title = "Title";
        public const string FieldRequired = "FieldRequired";
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.Title] = "My Screen",
            [Keys.FieldRequired] = "This field is required",
        });
        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.Title] = "Mi Pantalla",
            [Keys.FieldRequired] = "Este campo es obligatorio",
        });
    }
}
```

## Service Registration Pattern

Each feature area registers its services:

```csharp
public static IServiceCollection AddMyFeatureServices(this IServiceCollection services)
{
    services.AddTransient<MyScreenViewModel>();
    services.AddSingleton<MyScreenLocalizer>();
    services.AddTransient<IComponentLocalizer<MyScreenViewModel>>(provider =>
        provider.GetRequiredService<MyScreenLocalizer>());
    return services;
}
```

## Blazor Component Pattern

```razor
@using Nethereum.Wallet.UI.Components.Blazor.Shared
@inject MyScreenViewModel ViewModel
@inject IComponentLocalizer<MyScreenViewModel> Localizer

<WalletFormLayout Title="@Localizer.GetString(Keys.Title)"
                  PrimaryText="@Localizer.GetString(Keys.Save)"
                  OnPrimary="@HandleSave">
    <ChildContent>
        <WalletFormSection Title="@Localizer.GetString(Keys.SectionTitle)">
            <MudTextField @bind-Value="ViewModel.FieldName"
                          Label="@Localizer.GetString(Keys.FieldLabel)"
                          Error="@(!string.IsNullOrEmpty(ViewModel.FieldNameError))"
                          ErrorText="@ViewModel.FieldNameError"
                          Required="true" />
        </WalletFormSection>
    </ChildContent>
</WalletFormLayout>
```

## Hardware Wallet Support

For Trezor integration:

```bash
dotnet add package Nethereum.Wallet.UI.Components.Trezor
dotnet add package Nethereum.Wallet.UI.Components.Blazor.Trezor
```

For Ledger/Trezor on Android MAUI:

```bash
dotnet add package Nethereum.Maui.AndroidUsb
```

## Key Packages

| Package | Purpose |
|---------|---------|
| `Nethereum.Wallet` | Core: accounts, vaults, chains, transactions |
| `Nethereum.UI` | `IEthereumHostProvider`, SIWE authenticator |
| `Nethereum.Wallet.UI.Components` | Shared MVVM ViewModels |
| `Nethereum.Wallet.UI.Components.Blazor` | Blazor renderer |
| `Nethereum.Wallet.UI.Components.Maui` | MAUI renderer |
| `Nethereum.Wallet.RpcRequests` | EIP-1193 JSON-RPC handlers |

## Console Tests / Examples

Working examples in the Nethereum repo:
- `consoletests/NethereumWCBlazor/` — WalletConnect Blazor integration
- `consoletests/NethereumReownAppKitBlazor/` — Reown AppKit Blazor integration
- `src/demos/Nethereum.Wallet.Blazor.Demo/` — Full Blazor wallet demo

For full documentation, see: https://docs.nethereum.com/docs/wallet-sdk/overview
