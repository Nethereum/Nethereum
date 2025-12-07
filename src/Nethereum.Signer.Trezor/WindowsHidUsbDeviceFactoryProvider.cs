using Device.Net;
using Hid.Net.Windows;
using Microsoft.Extensions.Logging;
using Trezor.Net;
using Usb.Net.Windows;

namespace Nethereum.Signer.Trezor
{
    /// <summary>
    /// Default Windows implementation that scans both HID and WinUSB transports.
    /// </summary>
    public class WindowsHidUsbDeviceFactoryProvider : Abstractions.ITrezorDeviceFactoryProvider
    {
        public IDeviceFactory CreateDeviceFactory(ILoggerFactory loggerFactory)
        {
            var hidFactory = TrezorManager.DeviceDefinitions.CreateWindowsHidDeviceFactory();
            var usbFactory = TrezorManager.DeviceDefinitions.CreateWindowsUsbDeviceFactory();
            return usbFactory.Aggregate(hidFactory, loggerFactory);
        }
    }
}
