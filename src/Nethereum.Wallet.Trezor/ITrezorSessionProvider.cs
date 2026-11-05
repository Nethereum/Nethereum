#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Nethereum.Signer.Trezor;

namespace Nethereum.Wallet.Trezor;

[Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.WalletUI, "trezor-wallet-accounts", "ITrezorSessionProvider")]
public interface ITrezorSessionProvider
{
    Task<TrezorSessionExternalSigner> CreateSignerAsync(
        uint index,
        string deviceId,
        string? knownAddress = null,
        CancellationToken cancellationToken = default);
}
