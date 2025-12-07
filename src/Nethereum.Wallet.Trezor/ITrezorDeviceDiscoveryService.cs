#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Trezor;

public interface ITrezorDeviceDiscoveryService
{
    Task<IReadOnlyList<TrezorDerivationPreview>> DiscoverAsync(
        string deviceId,
        uint startIndex,
        uint count,
        CancellationToken cancellationToken = default);
}

public record TrezorDerivationPreview(uint Index, string Address);
