# Nethereum.Wallet.Trezor

**Trezor hardware-wallet accounts for `Nethereum.Wallet`: the `trezor` account type, its vault JSON factory, and the service that adds Trezor addresses to a `WalletVault`**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Wallet.Trezor.svg)](https://www.nuget.org/packages/Nethereum.Wallet.Trezor/)

## Overview

`Nethereum.Wallet.Trezor` plugs Trezor devices into the `Nethereum.Wallet` vault. It does not talk to hardware itself: it defines two small interfaces for that (`ITrezorSessionProvider` creates a `TrezorSessionExternalSigner` from [Nethereum.Signer.Trezor](../Nethereum.Signer.Trezor/README.md); `ITrezorDeviceDiscoveryService` lists the addresses at derivation indexes) and builds the wallet-side objects on top of them: `TrezorWalletAccount`, `TrezorWalletAccountFactory` and `TrezorWalletAccountService`. Your application, or the UI packages built on this one, supplies the two interface implementations that reach the device.

Use it when building a wallet on `Nethereum.Wallet` that lets users keep accounts on a Trezor. The vault stores only the address, index, device id and label (not keys), so the encrypted vault can be restored without the device attached; the device is contacted when `GetAccountAsync()` or `EnsureReadyAsync()` is called.

## Installation

```bash
dotnet add package Nethereum.Wallet.Trezor
```

Project references: `Nethereum.Signer.Trezor` and `Nethereum.Wallet`. Target frameworks: `net6.0`, `net8.0`, `net9.0`, `net10.0`.

## Interfaces you implement

Namespace `Nethereum.Wallet.Trezor`.

```csharp
public interface ITrezorSessionProvider
{
    Task<TrezorSessionExternalSigner> CreateSignerAsync(
        uint index,
        string deviceId,
        string? knownAddress = null,
        CancellationToken cancellationToken = default);
}

public interface ITrezorDeviceDiscoveryService
{
    Task<IReadOnlyList<TrezorDerivationPreview>> DiscoverAsync(
        string deviceId,
        uint startIndex,
        uint count,
        CancellationToken cancellationToken = default);
}

public record TrezorDerivationPreview(uint Index, string Address);
```

## TrezorWalletAccount

```csharp
public class TrezorWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "trezor";

    public override string Type { get; }
    public uint Index { get; }
    public string DeviceId { get; }
    public override string Name { get; }
    public override object? Settings { get; }
    public override string? GroupId { get; }

    public TrezorWalletAccount(
        string address,
        string label,
        uint index,
        string deviceId,
        ITrezorSessionProvider sessionProvider,
        ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)

    public override async Task<IAccount> GetAccountAsync()
    public override async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    public override JsonObject ToJson()
    public static TrezorWalletAccount FromJson(JsonElement json, ITrezorSessionProvider provider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)
}
```

- `Type` is `"trezor"`; `GroupId` is the `DeviceId`; `Name` is the `Label`, or `"Trezor #{Index}"` when there is no label.
- `GetAccountAsync()` asks the session provider for a signer (`CreateSignerAsync(Index, DeviceId, Address, ...)`), initialises it and returns an `ExternalAccount` wrapping it. Signing therefore needs the device.
- `EnsureReadyAsync()` calls `DiscoverAsync(DeviceId, Index, 1, cancellationToken)` on the discovery service.
- `ToJson()` writes `type`, `address`, `label`, `index`, `deviceId` and `selected`; `FromJson` reads `address`, `label`, `index` and `deviceId` (all required) and optionally `selected`.

`WalletAccountBase` (in `Nethereum.Wallet.WalletAccounts`) supplies `Address`, `Label`, `IsSelected` and `CreateWeb3Async(IClient client)`.

## TrezorWalletAccountService

```csharp
public class TrezorWalletAccountService
{
    public TrezorWalletAccountService(WalletVault vault, ITrezorSessionProvider sessionProvider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)

    public async Task<TrezorWalletAccount> CreateAsync(uint index, string deviceId, string? label = null, bool setAsSelected = false, string? deviceLabel = null)
    public TrezorWalletAccount CreateFromKnownAddress(uint index, string deviceId, string address, string? label = null, bool setAsSelected = false, bool addToVault = true, string? deviceLabel = null)
    public TrezorWalletAccount BuildAccount(uint index, string deviceId, string address, string? label = null)
    public void UpdateHardwareDeviceLabel(string deviceId, string label)
    public string GetDefaultHardwareDeviceLabel()
}
```

- `CreateAsync` creates a signer for the index, initialises it, reads its address from the device with `GetAddressAsync()`, builds the account and adds it to the vault. It requires the device.
- `CreateFromKnownAddress` does the same without the device, from an address you already have (for example one returned by `ITrezorDeviceDiscoveryService.DiscoverAsync`). Pass `addToVault: false` to get the account without adding it.
- `BuildAccount` only constructs the account; its label defaults to the address.
- Every creating method registers a `TrezorWalletAccountFactory` with the vault if none for `"trezor"` is registered, and records the device in the vault with `AddOrUpdateHardwareDevice(deviceId, "trezor", label)`. An empty `deviceLabel` becomes `"Trezor Wallet {n}"`, where `n` is the number of Trezor devices in the vault plus one (`GetDefaultHardwareDeviceLabel()`).
- `WalletVault.AddAccount` throws `InvalidOperationException("Duplicate account.")` for an address that is already stored under the same account type.

### Add a known address, persist the vault, restore it

```csharp
var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
var service = new TrezorWalletAccountService(vault, sessionProvider, discovery);

var account = service.CreateFromKnownAddress(
    index: 3, deviceId: DeviceId, address: Address, label: "Trezor cold wallet", setAsSelected: true);

var encrypted = vault.Encrypt("securepass123");

var restoredVault = new WalletVault(new Aes256GcmEncryptionStrategy());
restoredVault.RegisterFactory(new TrezorWalletAccountFactory(sessionProvider, discovery));
restoredVault.Decrypt(encrypted, "securepass123");

var restored = (TrezorWalletAccount)restoredVault.Accounts.Single();
var devices = restoredVault.GetHardwareDevicesByType("trezor");
```

`restored` has `Index` 3, the same `DeviceId`, `Name` `"Trezor cold wallet"`, `IsSelected` `true`, and the vault has one Trezor device labelled `"Trezor Wallet 1"`. A fresh `WalletVault` registers factories only for its built-in account types, so register `TrezorWalletAccountFactory` before `Decrypt`, otherwise `Decrypt` throws `NotSupportedException("No factory for type: trezor")`. Backed by `TrezorWalletAccountTests.ShouldAddAKnownTrezorAddressToTheVaultAndRestoreItFromTheEncryptedVault` (`DocSection.WalletUI`, `trezor-wallet-accounts`) in `tests/Nethereum.Wallet.UnitTests/TrezorWalletAccountTests.cs`; `sessionProvider` and `discovery` are the test's offline implementations of the two interfaces (the session provider throws if a signer is requested), so no device is needed.

### Labels and device names

```csharp
var firstDefaultLabel = service.GetDefaultHardwareDeviceLabel();
var account = service.BuildAccount(0, DeviceId, Address);
service.CreateFromKnownAddress(1, DeviceId, "0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47");
service.UpdateHardwareDeviceLabel(DeviceId, "  Office Trezor  ");
```

`firstDefaultLabel` is `"Trezor Wallet 1"`, `account.Label` is its address, and the stored device label is the trimmed `"Office Trezor"`. Backed by `ShouldLabelAnUnlabelledAccountWithItsAddressAndRenameTheDevice` (`trezor-wallet-accounts`, Order 2).

### Re-checking the account on the device

```csharp
var account = new TrezorWalletAccount(Address, "label", 5, DeviceId, sessionProvider, discovery);

await account.EnsureReadyAsync();
```

The discovery service receives one call with `(DeviceId, 5, 1)`. Backed by `ShouldRediscoverTheAccountOnTheDeviceWhenEnsuringItIsReady` (Order 3).

## Factory

```csharp
public class TrezorWalletAccountFactory : WalletAccountFactoryBase<TrezorWalletAccount>
{
    public override string Type { get; }

    public TrezorWalletAccountFactory(ITrezorSessionProvider sessionProvider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)

    public override IWalletAccount FromJson(JsonElement element, WalletVault vault)
}
```

## Not covered by a runnable example

`TrezorWalletAccount.GetAccountAsync()` and `TrezorWalletAccountService.CreateAsync` need a real `TrezorSessionExternalSigner` and therefore a connected device; they are documented as declarations only. `Nethereum.Wallet.UI.Components.Trezor` (view models for creating Trezor accounts) and `Nethereum.Wallet.UI.Components.Blazor.Trezor` build on these types. The interface implementations themselves are application code: in this repository, `src/NetDapps/Services/NetDappsTrezorSessionProvider.cs` and `TrezorDeviceDiscoveryService.cs` are the ones the NetDapps MAUI app uses.
