using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugGetRawTransaction
    {
        RpcRequest BuildRequest(string transactionHash, object id = null);
        Task<string> SendRequestAsync(string transactionHash, object id = null);
    }
}