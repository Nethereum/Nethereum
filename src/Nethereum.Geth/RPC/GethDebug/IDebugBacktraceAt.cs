using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugBacktraceAt
    {
        RpcRequest BuildRequest(string fileAndLine, object id = null);
        Task<string> SendRequestAsync(string fileAndLine, object id = null);
    }
}