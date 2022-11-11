using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugVerbosity
    {
        RpcRequest BuildRequest(int level, object id = null);
        Task<object> SendRequestAsync(int level, object id = null);
    }
}