using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugGoTrace
    {
        RpcRequest BuildRequest(string fileName, int seconds, object id = null);
        Task<object> SendRequestAsync(string fileName, int seconds, object id = null);
    }
}