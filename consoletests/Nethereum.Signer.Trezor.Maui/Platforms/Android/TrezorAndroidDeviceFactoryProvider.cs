#if ANDROID || __ANDROID__
using Android.Content;
using Android.Hardware.Usb;
using Device.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Nethereum.Maui.AndroidUsb;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Signer.Trezor.Internal;

namespace Nethereum.Signer.Trezor.Maui.Platforms.Android
{
    internal class TrezorAndroidDeviceFactoryProvider : ITrezorDeviceFactoryProvider
    {
        private readonly UsbManager _usbManager;
        private readonly Context _context;

        public TrezorAndroidDeviceFactoryProvider(UsbManager usbManager, Context context)
        {
            _usbManager = usbManager;
            _context = context;
        }

        public IDeviceFactory CreateDeviceFactory(ILoggerFactory loggerFactory) =>
            new MauiAndroidUsbDeviceFactory(
                _usbManager,
                _context,
                loggerFactory,
                () => Platform.CurrentActivity,
                ExtendedTrezorManager.DeviceDefinitions);
    }
}
#endif
