using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Signer
{
#if !DOTNET35
    public interface IEthExternalSigner
    {
        bool CalculatesV { get; }
        ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; }
        Task<string> GetAddressAsync();
        Task<byte[]> GetPublicKeyAsync();
        Task<EthECDSASignature> SignAsync(byte[] hash);
        Task<EthECDSASignature> SignAsync(byte[] rawBytes, BigInteger chainId);
        Task SignAsync(Transaction transaction);
        Task SignAsync(TransactionChainId transaction);
    }
#endif
}