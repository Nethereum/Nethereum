using Nethereum.JsonRpc.Client;
using Nethereum.RPC.TxPool.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.TxPool
{
    public interface ITxPoolContentFrom
    {
        Task<TxPoolContentFromResponse> SendRequestAsync(string address, object id = null);
        RpcRequest BuildRequest(string address, object id = null);
    }
}
