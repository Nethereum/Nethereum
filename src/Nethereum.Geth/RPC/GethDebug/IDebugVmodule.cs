using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugVmodule
    {
        RpcRequest BuildRequest(string pattern, object id = null);
        Task<object> SendRequestAsync(string pattern, object id = null);
    }
}