using Nethereum.JsonRpc.Client;
using Nethereum.RPC.TxPool.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.TxPool
{
    public interface ITxPoolContent
    {
        Task<TxPoolContentResponse> SendRequestAsync(object id = null);
        RpcRequest BuildRequest(object id = null);
    }
}
