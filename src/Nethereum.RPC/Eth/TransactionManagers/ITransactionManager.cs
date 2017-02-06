using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public interface ITransactionManager
    {
        Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput;
    }
}
