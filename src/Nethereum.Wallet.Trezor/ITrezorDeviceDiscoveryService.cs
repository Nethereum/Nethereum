#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Trezor;

[Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.WalletUI, "trezor-wallet-accounts", "ITrezorDeviceDiscoveryService")]
public interface ITrezorDeviceDiscoveryService
{
    Task<IReadOnlyList<TrezorDerivationPreview>> DiscoverAsync(
        string deviceId,
        uint startIndex,
        uint count,
        CancellationToken cancellationToken = default);
}

[Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.WalletUI, "trezor-wallet-accounts", "TrezorDerivationPreview")]
public record TrezorDerivationPreview(uint Index, string Address);
