using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Trezor.Net;
using Trezor.Net.Manager;
using Device.Net;
using Nethereum.Signer.Trezor.Abstractions;


namespace Nethereum.Signer.Trezor
{
    public class NethereumTrezorManagerBrokerFactory
    {
        public static NethereumTrezorManagerBroker Create(ITrezorDeviceFactoryProvider deviceFactoryProvider, ITrezorPromptHandler promptHandler, ILoggerFactory loggerFactory, int? pollInterval = 2000)
        {
            if (deviceFactoryProvider == null) throw new ArgumentNullException(nameof(deviceFactoryProvider));
            if (promptHandler == null) throw new ArgumentNullException(nameof(promptHandler));

            return Create(
                deviceFactoryProvider.CreateDeviceFactory(loggerFactory),
                () => promptHandler.GetPinAsync(),
                () => promptHandler.GetPassphraseAsync(),
                loggerFactory,
                pollInterval);
        }

        public static NethereumTrezorManagerBroker CreateWindowsHidUsb(EnterPinArgs enterPinCallback, EnterPinArgs enterPassPhrase, ILoggerFactory loggerFactory, int? pollInterval = 2000)
        {
            return Create(new WindowsHidUsbDeviceFactoryProvider().CreateDeviceFactory(loggerFactory), enterPinCallback, enterPassPhrase, loggerFactory, pollInterval);
        }

        public static NethereumTrezorManagerBroker Create(ITrezorPromptHandler promptHandler, ILoggerFactory loggerFactory, int? pollInterval = 2000)
        {
            return Create(new WindowsHidUsbDeviceFactoryProvider(), promptHandler, loggerFactory, pollInterval);
        }

        public static NethereumTrezorManagerBroker Create(IDeviceFactory deviceFactory, EnterPinArgs enterPinCallback, EnterPinArgs enterPassPhrase, ILoggerFactory loggerFactory, int? pollInterval = 2000)
        {
            if (deviceFactory == null) throw new ArgumentNullException(nameof(deviceFactory));
            return new NethereumTrezorManagerBroker(enterPinCallback, enterPassPhrase, pollInterval, deviceFactory, new DefaultCoinUtility(), loggerFactory);
        }

        /// <summary>
        /// Creates a broker suitable for the current OS. Non-Windows platforms require passing custom providers via <paramref name="platformProviders"/>.
        /// </summary>
        public static NethereumTrezorManagerBroker CreateDefault(ITrezorPromptHandler promptHandler, ILoggerFactory loggerFactory, PlatformDeviceFactoryProviders platformProviders = null, int? pollInterval = 2000)
        {
            platformProviders ??= new PlatformDeviceFactoryProviders();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Create(platformProviders.WindowsProvider ?? new WindowsHidUsbDeviceFactoryProvider(), promptHandler, loggerFactory, pollInterval);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (platformProviders.MacProvider == null) throw new PlatformNotSupportedException("macOS requires a custom ITrezorDeviceFactoryProvider. Provide one via PlatformDeviceFactoryProviders.MacProvider.");
                return Create(platformProviders.MacProvider, promptHandler, loggerFactory, pollInterval);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (platformProviders.LinuxProvider == null) throw new PlatformNotSupportedException("Linux requires a custom ITrezorDeviceFactoryProvider. Provide one via PlatformDeviceFactoryProviders.LinuxProvider.");
                return Create(platformProviders.LinuxProvider, promptHandler, loggerFactory, pollInterval);
            }

            if (platformProviders.AndroidProvider != null && RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")))
            {
                return Create(platformProviders.AndroidProvider, promptHandler, loggerFactory, pollInterval);
            }

            throw new PlatformNotSupportedException("No ITrezorDeviceFactoryProvider registered for the current platform.");
        }
        public class PlatformDeviceFactoryProviders
        {
            public ITrezorDeviceFactoryProvider WindowsProvider { get; set; } = new WindowsHidUsbDeviceFactoryProvider();
            public ITrezorDeviceFactoryProvider LinuxProvider { get; set; } = new LibUsbDeviceFactoryProvider();
            public ITrezorDeviceFactoryProvider MacProvider { get; set; } = new LibUsbDeviceFactoryProvider();
            public ITrezorDeviceFactoryProvider AndroidProvider { get; set; }
        }
    }


}
