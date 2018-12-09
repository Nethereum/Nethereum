using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Geth.RPC.GethEth
{
    public interface IEthPendingTransactions
    {
        RpcRequest BuildRequest(object id = null);
        Task<Transaction[]> SendRequestAsync(object id = null);
    }
}