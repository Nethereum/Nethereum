#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Nethereum.Signer.Trezor;

namespace Nethereum.Wallet.Trezor;

public interface ITrezorSessionProvider
{
    Task<TrezorSessionExternalSigner> CreateSignerAsync(
        uint index,
        string deviceId,
        string? knownAddress = null,
        CancellationToken cancellationToken = default);
}
