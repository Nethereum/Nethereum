using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.GSN
{
    public interface IGSNTransactionManager
    {
        Task<string> SendTransactionAsync(TransactionInput transactionInput);
    }
}