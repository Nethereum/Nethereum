using System.Threading.Tasks;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer
{
#if !DOTNET35
    public interface IEthExternalSigner
    {
        bool CalculatesV { get; }
        Task<byte[]> GetPublicKeyAsync();
        Task<ECDSASignature> SignAsync(byte[] hash);
    }
#endif
}