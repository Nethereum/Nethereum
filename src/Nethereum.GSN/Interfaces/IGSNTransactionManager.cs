using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.GSN.Interfaces
{
    public interface IGSNTransactionManager
    {
        Task<string> SendTransactionAsync(TransactionInput transactionInput);
    }
}