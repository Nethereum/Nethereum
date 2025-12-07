using Device.Net;
using Microsoft.Extensions.Logging;

namespace Nethereum.Signer.Trezor.Abstractions
{
    /// <summary>
    /// Provides an underlying device factory for a specific platform or transport implementation.
    /// </summary>
    public interface ITrezorDeviceFactoryProvider
    {
        IDeviceFactory CreateDeviceFactory(ILoggerFactory loggerFactory);
    }
}
