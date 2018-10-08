using System.Threading.Tasks;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer
{
#if !DOTNET35

    public enum ExternalSignerFormat
    {
        RLP,
        Hash
    }

    public interface IEthExternalSigner
    {
        ExternalSignerFormat ExternalSignerFormat { get; }
        bool CalculatesV { get; }
        Task<byte[]> GetPublicKeyAsync();
        Task<ECDSASignature> SignAsync(byte[] hash);
    }
#endif
}