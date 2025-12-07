using Nethereum.Model;
using Nethereum.Signer.Crypto;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Signer.Trezor.Abstractions
{
    public interface ITrezorSession: IEthExternalSigner
    {
        Task InitializeAsync();
    }
}
