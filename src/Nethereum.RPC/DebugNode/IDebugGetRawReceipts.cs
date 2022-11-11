using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugGetRawReceipts
    {
        RpcRequest BuildRequest(BlockParameter block, object id = null);
        Task<string[]> SendRequestAsync(object id = null);
        Task<string[]> SendRequestAsync(BlockParameter block, object id = null);
    }
}