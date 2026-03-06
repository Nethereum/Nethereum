using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Policy
{
    public interface IPolicyService
    {
        Task<PolicyInfo> GetCurrentPolicyAsync();

        Task<byte[]?> GetWritersRootAsync();

        Task<byte[]?> GetAdminsRootAsync();

        Task<byte[]?> GetBlacklistRootAsync();

        Task<BigInteger> GetEpochAsync();

        Task<bool> IsValidWriterAsync(string address, byte[][] writerProof, byte[]? blacklistProof = null);
    }
}
