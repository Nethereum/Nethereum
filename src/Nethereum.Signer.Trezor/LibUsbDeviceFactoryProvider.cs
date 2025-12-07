using Device.Net;
using Device.Net.LibUsb;
using Microsoft.Extensions.Logging;
using Nethereum.Signer.Trezor.Abstractions;
using Trezor.Net;

namespace Nethereum.Signer.Trezor
{
    /// <summary>
    /// Uses the Device.Net.LibUsb transport for Linux and macOS hosts.
    /// </summary>
    public class LibUsbDeviceFactoryProvider : ITrezorDeviceFactoryProvider
    {
        public IDeviceFactory CreateDeviceFactory(ILoggerFactory loggerFactory)
        {
            return TrezorManager.DeviceDefinitions.CreateLibUsbDeviceFactory(loggerFactory);
        }
    }
}
