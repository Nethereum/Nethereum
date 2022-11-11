using System.Threading.Tasks;

namespace Nethereum.Contracts.Services
{
    public interface IEthGetContractTransactionErrorReason
    {
#if !DOTNET35
        Task<string> SendRequestAsync(string transactionHash);
#endif
    }
}