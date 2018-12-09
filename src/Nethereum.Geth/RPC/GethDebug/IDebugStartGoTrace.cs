using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugStartGoTrace
    {
        RpcRequest BuildRequest(string filePath, object id = null);
        Task<object> SendRequestAsync(string filePath, object id = null);
    }
}