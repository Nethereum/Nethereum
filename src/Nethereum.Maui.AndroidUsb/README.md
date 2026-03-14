# Nethereum.Maui.AndroidUsb

Android USB device support for .NET MAUI applications. Enables communication with USB-connected hardware wallets (Ledger, Trezor) on Android devices through the Android USB Host API.

## Key Components

| Class | Purpose |
|---|---|
| `MauiAndroidUsbDeviceFactory` | Creates USB device connections using the Android USB Host API |
| `MauiAndroidUsbDevice` | Wrapper around Android `UsbDeviceConnection` for HID communication |
| `UsbPermissionHelper` | Handles Android USB permission requests |
| `UsbAttachReceiver` | Broadcast receiver for USB device attach/detach events |

## Usage

Register the USB device factory in your MAUI application:

```csharp
// In MauiProgram.cs
builder.Services.AddSingleton<IHidDeviceFactory, MauiAndroidUsbDeviceFactory>();
```

The factory is then used by `Nethereum.Signer.Ledger` or `Nethereum.Signer.Trezor` to communicate with the hardware wallet over USB.

## Relationship to Other Packages

- **[Nethereum.Signer.Ledger](../Nethereum.Signer.Ledger/README.md)** — Ledger hardware wallet signing (uses this for Android USB transport)
- **[Nethereum.Signer.Trezor](../Nethereum.Signer.Trezor/README.md)** — Trezor hardware wallet signing (uses this for Android USB transport)
- **[Nethereum.Wallet.UI.Components.Maui](../Nethereum.Wallet.UI.Components.Maui/README.md)** — MAUI wallet UI (integrates hardware wallet support)
