using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthGetTransactionByHash
    {
        RpcRequest BuildRequest(string hashTransaction, object id = null);
        Task<Transaction> SendRequestAsync(string hashTransaction, object id = null);
    }
}