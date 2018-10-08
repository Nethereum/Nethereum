using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Signer
{
#if !DOTNET35
    public interface IEthECKeyExternalSigner
    {
        Task<string> GetPublicKeyAsync();
        ExternalSignerFormat ExternalSignerFormat { get; }
        Task<EthECDSASignature> SignAndCalculateVAsync(byte[] hash, BigInteger chainId);
        Task<EthECDSASignature> SignAndCalculateVAsync(byte[] hash);
        Task<string> GetAddressAsync();
    }
#endif
}