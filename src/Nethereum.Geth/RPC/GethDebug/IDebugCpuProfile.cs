using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugCpuProfile
    {
        RpcRequest BuildRequest(string filePath, int seconds, object id = null);
        Task<object> SendRequestAsync(string filePath, int seconds, object id = null);
    }
}