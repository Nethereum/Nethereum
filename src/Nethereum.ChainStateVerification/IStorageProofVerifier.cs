using System.Threading.Tasks;

namespace Nethereum.ChainStateVerification
{
    public interface IStorageProofVerifier
    {
        Task<byte[]> GetStorageValueAsync(string address, string storageSlotHex);
    }
}
