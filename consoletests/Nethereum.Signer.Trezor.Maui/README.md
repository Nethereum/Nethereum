## Nethereum.Signer.Trezor.Maui

A minimal .NET MAUI application that demonstrates using the reusable Trezor session infrastructure with a UI-friendly prompt handler. Targets Android, Windows and macOS (MacCatalyst).

### Features

- `MauiPromptHandler` bridges Trezor PIN/passphrase/button requests to MAUI dialogs.
- `TrezorSigningService` shows how to create a `TrezorSessionExternalSigner`, initialise the device, and sign an arbitrary UTF-8 message.
- Platform adapter selection is driven through `NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders`.
  - Windows uses the built-in HID/WinUSB combo.
  - macOS/Linux default to the new `LibUsbDeviceFactoryProvider` (install LibUsb on the host OS).
- Android uses `TrezorAndroidDeviceFactoryProvider`, which builds a Device.Net `IDeviceFactory` via `Usb.Net` (only `Usb.Net` is required—there is no `Device.Net.Android` package).

### Building / Running

1. Install the .NET MAUI workload for your platform (`dotnet workload install maui`).
2. Ensure you have the native prerequisites:
   - **macOS / Linux**: install LibUsb (`brew install libusb` on macOS, or your distro package). `Device.Net.LibUsb` expects the native library to be present.
   - **Android**: enable USB debugging and grant the app USB permissions when prompted. The Android manifest already requests `USB_PERMISSION`.
   - **Windows**: no extra setup; standard HID/WinUSB support is used.
3. Build/run for your target:

```bash
# Windows desktop
dotnet build -t:Run -f net9.0-windows10.0.19041.0 consoletests/Nethereum.Signer.Trezor.Maui/Nethereum.Signer.Trezor.Maui.csproj

# Android emulator/device
dotnet build -t:Run -f net9.0-android consoletests/Nethereum.Signer.Trezor.Maui/Nethereum.Signer.Trezor.Maui.csproj

# macOS
dotnet build -t:Run -f net9.0-maccatalyst consoletests/Nethereum.Signer.Trezor.Maui/Nethereum.Signer.Trezor.Maui.csproj
```

When you tap **Connect & Sign**, follow the on-screen prompts (PIN/passphrase) and confirm on your Trezor. The computed signature is displayed once both the device and service complete.
