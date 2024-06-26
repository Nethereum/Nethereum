using System.Threading.Tasks;

namespace Nethereum.Contracts.Standards.ERC6492
{
    public interface IERC6492Service
    {
        byte[] BuildSignature(string create2FactoryAddress, byte[] factoryCallData, byte[] originalSignature);
        bool IsERC6492Signature(byte[] signature);
#if !DOTNET35
        Task<bool> IsValidSignatureAsync(string signer, byte[] hash, byte[] signature);
        Task<bool> IsValidSignatureAsync(string signer, string create2FactoryAddress, byte[] factoryCallData, byte[] hash, byte[] originalSignature);
        Task<bool> IsValidSignatureMessageAsync(string signer, string plainUTF8Message, byte[] signature);
        Task<bool> IsValidSignatureMessageAsync(string signer, byte[] plainMessage, byte[] signature);
#endif
    }
}