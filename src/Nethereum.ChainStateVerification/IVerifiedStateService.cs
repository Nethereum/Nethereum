using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Model;

namespace Nethereum.ChainStateVerification
{
    public interface IVerifiedStateService
    {
        VerificationMode Mode { get; set; }

        Task<Account> GetAccountAsync(string address);
        Task<BigInteger> GetBalanceAsync(string address);
        Task<BigInteger> GetNonceAsync(string address);
        Task<byte[]> GetCodeAsync(string address);
        Task<byte[]> GetCodeHashAsync(string address);

        Task<byte[]> GetStorageAtAsync(string address, BigInteger position);
        Task<byte[]> GetStorageAtAsync(string address, string slotHex);

        byte[] GetBlockHash(ulong blockNumber);

        TrustedExecutionHeader GetCurrentHeader();
    }
}
