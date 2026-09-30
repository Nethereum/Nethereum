# Nethereum.Maui.AndroidUsb

Android USB device support for .NET MAUI applications. Provides a `Device.Net` `IDeviceFactory` implementation for USB-connected devices on Android through the Android USB Host API. The verified integration wires this factory into Trezor hardware-wallet signing via `ITrezorDeviceFactoryProvider`.

## Key Components

| Class | Purpose |
|---|---|
| `MauiAndroidUsbDeviceFactory` | Creates USB device connections using the Android USB Host API |
| `MauiAndroidUsbDevice` | Wrapper around Android `UsbDeviceConnection` for HID communication |
| `UsbPermissionHelper` | Handles Android USB permission requests |

> `UsbAttachReceiver` is an internal implementation detail (`internal sealed`, a broadcast receiver for USB attach/detach events) and is not a consumable public type.

> **Android-only:** All types in this package are compiled only under the `net10.0-android` target framework (guarded by `#if ANDROID`). They are not available on the plain `net10.0` target. The `TargetFrameworks` are `net10.0;net10.0-android`.

## Usage

`MauiAndroidUsbDeviceFactory` implements `Device.Net`'s `IDeviceFactory`. Construct it with the Android `UsbManager`, a `Context`, an `ILoggerFactory`, an activity provider, and the device filters that describe which USB devices to expose:

```csharp
using Android.Content;
using Android.Hardware.Usb;
using Device.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Nethereum.Maui.AndroidUsb;
using Nethereum.Signer.Trezor.Internal;

// context: an Android Context (e.g. the current activity or application context)
// loggerFactory: an ILoggerFactory from your MAUI service provider
var usbManager = (UsbManager)context.GetSystemService(Context.UsbService)!;

// Device filters describe the USB vendor/product ids to expose (e.g. Trezor's definitions).
IEnumerable<FilterDeviceDefinition> deviceDefinitions = ExtendedTrezorManager.DeviceDefinitions;

IDeviceFactory deviceFactory = new MauiAndroidUsbDeviceFactory(
    usbManager,
    context,
    loggerFactory,
    () => Platform.CurrentActivity,
    deviceDefinitions);
```

The verified integration path exposes this factory to Trezor signing: `NetDapps.Platforms.Android.NetDappsTrezorDeviceFactoryProvider` (an `ITrezorDeviceFactoryProvider`) constructs a `MauiAndroidUsbDeviceFactory` in exactly this way and returns it as an `IDeviceFactory` from `CreateDeviceFactory(ILoggerFactory)`, which `Nethereum.Signer.Trezor` uses to communicate with the hardware wallet over USB.

## Relationship to Other Packages

- **[Nethereum.Signer.Trezor](../Nethereum.Signer.Trezor/README.md)** — Trezor hardware wallet signing (this factory is exposed to Trezor as an `IDeviceFactory` via `ITrezorDeviceFactoryProvider` for Android USB transport)
- **[Nethereum.Wallet.UI.Components.Maui](../Nethereum.Wallet.UI.Components.Maui/README.md)** — MAUI wallet UI (integrates hardware wallet support)
