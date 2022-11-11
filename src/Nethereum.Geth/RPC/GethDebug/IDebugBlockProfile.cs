using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugBlockProfile
    {
        RpcRequest BuildRequest(string file, long seconds, object id = null);
        Task<object> SendRequestAsync(string file, long seconds, object id = null);
    }
}