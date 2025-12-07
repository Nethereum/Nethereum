using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Signer.Trezor.Abstractions
{
    public interface ITrezorTransport
    {
        Task OpenAsync();
        Task CloseAsync();
        Task WriteAsync(ReadOnlyMemory<byte> buffer);
        Task<int> ReadAsync(Memory<byte> buffer);
        TransportCapabilities Capabilities { get; }
    }

    public class TransportCapabilities
    {
        public bool SupportsHid { get; set; }
        public bool SupportsUsb { get; set; }
        public bool SupportsBle { get; set; }
        public IReadOnlyCollection<string> PlatformTags { get; set; }
    }

    public interface ITrezorTransportFactory
    {
        /// <summary>
        /// Returns available transports for the current platform.
        /// </summary>
        IEnumerable<ITrezorTransport> GetTransports();
    }
}
