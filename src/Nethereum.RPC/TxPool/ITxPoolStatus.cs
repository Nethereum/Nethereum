using Nethereum.JsonRpc.Client;
using Nethereum.RPC.TxPool.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.TxPool
{
    public interface ITxPoolStatus
    {
        Task<TxPoolStatusResponse> SendRequestAsync(object id = null);
        RpcRequest BuildRequest(object id = null);
    }
}
