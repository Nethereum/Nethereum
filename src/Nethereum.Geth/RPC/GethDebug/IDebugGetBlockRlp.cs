using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugGetBlockRlp
    {
        RpcRequest BuildRequest(ulong blockNumber, object id = null);
        Task<string> SendRequestAsync(ulong blockNumber, object id = null);
    }
}